# Autenticazione JWT — Documentazione Implementazione

## Panoramica

E' stato implementato un sistema completo di autenticazione JWT con refresh token rotation, che copre sia il backend ASP.NET Core che il frontend Angular. Il sistema segue l'architettura Clean Architecture con pattern CQRS gia' presente nel progetto.

---

## Come funziona

### Flusso di autenticazione

```
1. REGISTRAZIONE
   Client → POST /api/v1/auth/register
   Server → crea utente (EmailConfirmed = false)
          → genera token di verifica email (ASP.NET Identity)
          → costruisce link: {Client:BaseUrl}/confirm-email?email=...&token=...
          → invia email con link via IEmailService
          → ritorna messaggio "controlla la tua email" (NO token JWT)

   Client riceve il link via email → naviga a /confirm-email?email=...&token=...
   Client → POST /api/v1/auth/confirm-email { email, token }
   Server → valida token via ConfirmEmailAsync
          → imposta EmailConfirmed = true
          → genera Access Token JWT (60 min)
          → genera Refresh Token opaco (30 giorni, salvato su DB)
          → ritorna token + dati utente (auto-login)

1b. LOGIN (utente non verificato)
   Client → POST /api/v1/auth/login
   Server → trova utente, verifica password
          → se EmailConfirmed == false → errore "verifica la tua email"

1c. LOGIN (utente verificato)
   Client → POST /api/v1/auth/login
   Server → verifica credenziali + EmailConfirmed
          → genera Access Token JWT (60 min)
          → genera Refresh Token opaco (30 giorni, salvato su DB)
          → ritorna entrambi i token + dati utente

2. RICHIESTE AUTENTICATE
   Client → GET /api/v1/auth/me (header: Authorization: Bearer <access_token>)
   Server → valida il JWT, estrae userId dai claims
          → ritorna dati utente

3. REFRESH (quando l'access token scade)
   Client → POST /api/v1/auth/refresh { refreshToken: "..." }
   Server → trova il refresh token nel DB
          → lo revoca (token rotation)
          → genera nuova coppia access + refresh token
          → ritorna i nuovi token

4. LOGOUT
   Client → POST /api/v1/auth/logout { refreshToken: "..." }
   Server → revoca il refresh token nel DB

5. RESET PASSWORD
   Client → POST /api/v1/auth/forgot-password { email: "..." }
   Server → cerca utente per email
          → se esiste e attivo: genera token di reset (ASP.NET Identity)
          → invia email con token via IEmailService
          → ritorna SEMPRE successo (protezione email enumeration)

   Client → POST /api/v1/auth/reset-password { email, token, newPassword }
   Server → valida utente (esiste e attivo)
          → valida token via Identity (ResetPasswordAsync)
          → resetta la password
          → ritorna successo o errore
```

### Token Rotation

Ogni refresh token e' **monouso**. Quando viene usato per ottenere nuovi token:
- Il vecchio refresh token viene revocato (`RevokedAt` viene impostato)
- Viene generato un nuovo refresh token
- Il campo `ReplacedByToken` traccia la catena per audit

Questo limita i danni in caso di furto del token.

---

## Struttura Backend

### Entita' di Dominio (`Seed.Domain/Entities/`)

| File | Descrizione |
|------|-------------|
| `ApplicationUser.cs` | Estende `IdentityUser<Guid>`. Campi aggiuntivi: FirstName, LastName, CreatedAt, UpdatedAt, IsActive |
| `ApplicationRole.cs` | Estende `IdentityRole<Guid>`. Campi aggiuntivi: Description, CreatedAt |
| `RefreshToken.cs` | Entita' per i refresh token. Campi: Token, ExpiresAt, RevokedAt, ReplacedByToken, UserId. Proprieta' computed: IsExpired, IsRevoked, IsActive |

### Configurazione (`Seed.Shared/Configuration/`)

| File | Descrizione |
|------|-------------|
| `JwtSettings.cs` | POCO con Secret, Issuer, Audience, AccessTokenExpirationMinutes (default 60), RefreshTokenExpirationDays (default 30) |
| `SmtpSettings.cs` | POCO con Host, Port (587), Username, Password, FromEmail, FromName ("Seed App"), Security ("StartTls") |
| `ClientSettings.cs` | POCO con BaseUrl (default "http://localhost:4200"). Usato per costruire i link nelle email |

### Application Layer (`Seed.Application/`)

| Cartella | Contenuto |
|----------|-----------|
| `Common/Result.cs` | Tipo generico `Result<T>` con Succeeded, Data, Errors |
| `Common/Models/` | AuthResponse, UserDto, TokenResult — record immutabili per le risposte |
| `Common/Interfaces/ITokenService.cs` | Interfaccia per generazione/refresh/revoca token |
| `Common/Interfaces/IEmailService.cs` | Interfaccia per invio email (`SendPasswordResetEmailAsync`, `SendEmailVerificationAsync`) |
| `DependencyInjection.cs` | Registra MediatR e FluentValidation |
| `Auth/Commands/Register/` | RegisterCommand + Validator + Handler. Genera token di verifica e invia email. Ritorna messaggio (non token JWT) |
| `Auth/Commands/ConfirmEmail/` | ConfirmEmailCommand + Validator + Handler. Valida token, imposta EmailConfirmed=true, ritorna AuthResponse (auto-login) |
| `Auth/Commands/Login/` | LoginCommand + Validator + Handler |
| `Auth/Commands/RefreshToken/` | RefreshTokenCommand + Handler |
| `Auth/Commands/Logout/` | LogoutCommand + Handler |
| `Auth/Commands/ForgotPassword/` | ForgotPasswordCommand + Validator + Handler. Genera token e invia email. Ritorna sempre successo (anti-enumeration) |
| `Auth/Commands/ResetPassword/` | ResetPasswordCommand + Validator + Handler. Valida token e resetta password via Identity |
| `Auth/Queries/GetCurrentUser/` | GetCurrentUserQuery + Handler |

Ogni command/query segue il pattern CQRS con MediatR:
- **Command** = record che implementa `IRequest<Result<T>>`
- **Validator** = classe FluentValidation con regole di validazione
- **Handler** = classe che implementa `IRequestHandler`, contiene la logica

### Infrastructure Layer (`Seed.Infrastructure/`)

| File | Descrizione |
|------|-------------|
| `Persistence/ApplicationDbContext.cs` | Estende `IdentityDbContext`, include `DbSet<RefreshToken>` |
| `Persistence/Configurations/` | EF Core fluent configurations per ApplicationUser e RefreshToken |
| `Services/TokenService.cs` | Implementa ITokenService: genera JWT con claims, gestisce refresh token su DB |
| `Services/SmtpEmailService.cs` | Implementa IEmailService via MailKit. Invia email HTML con token di reset |
| `Services/ConsoleEmailService.cs` | Implementa IEmailService come fallback: logga il token su console (per sviluppo) |
| `DependencyInjection.cs` | Registra DbContext, Identity, JwtSettings, TokenService. Auto-switch email service: se `Smtp:Host` e' configurato usa SmtpEmailService, altrimenti ConsoleEmailService |

### API Layer (`Seed.Api/`)

| File | Descrizione |
|------|-------------|
| `Controllers/AuthController.cs` | 7 endpoint sotto `/api/v1/auth/` |
| `Program.cs` | Configura JWT authentication, CORS, DI |
| `appsettings.json` | ConnectionStrings e JwtSettings |

### Endpoint API

| Metodo | Path | Auth | Descrizione |
|--------|------|------|-------------|
| POST | `/api/v1/auth/register` | No | Registrazione nuovo utente. Invia email di verifica. Ritorna messaggio (non token) |
| POST | `/api/v1/auth/confirm-email` | No | Conferma email con token ricevuto via link. Ritorna AuthResponse (auto-login) |
| POST | `/api/v1/auth/login` | No | Login con email/password. Blocca utenti non verificati |
| POST | `/api/v1/auth/refresh` | No | Rinnovo token con refresh token |
| POST | `/api/v1/auth/logout` | Si | Revoca del refresh token |
| GET | `/api/v1/auth/me` | Si | Dati dell'utente corrente |
| POST | `/api/v1/auth/forgot-password` | No | Richiesta reset password (invia email con token). Rate limited |
| POST | `/api/v1/auth/reset-password` | No | Reset password con token ricevuto via email. Rate limited |

---

## Struttura Frontend

### Libreria `shared-auth` (`frontend/web/projects/shared-auth/`)

| File | Descrizione |
|------|-------------|
| `models/auth.models.ts` | Interfacce TypeScript: User, LoginRequest, RegisterRequest, AuthResponse, ForgotPasswordRequest, ResetPasswordRequest, MessageResponse |
| `services/auth.service.ts` | Servizio singleton con Angular signals per lo stato auth |
| `interceptors/auth.interceptor.ts` | HTTP interceptor funzionale per Bearer token + auto-refresh |
| `guards/auth.guard.ts` | Protegge rotte che richiedono autenticazione |
| `guards/guest.guard.ts` | Impedisce accesso a login/register se gia' autenticati |
| `providers/auth-initializer.provider.ts` | `APP_INITIALIZER` che ripristina la sessione al boot dell'app |

### AuthService (dettaglio)

Stato reattivo basato su **Angular signals**:
- `currentUser: Signal<User | null>` — utente corrente
- `accessToken: Signal<string | null>` — JWT corrente
- `isAuthenticated: Signal<boolean>` — computed, true se currentUser non e' null

Metodi principali:
- `login(request)` → chiama API, salva token in localStorage, aggiorna signals
- `register(request)` → chiama API registrazione, ritorna messaggio (non salva token — serve verifica email)
- `confirmEmail(email, token)` → conferma email, salva token in localStorage, aggiorna signals (auto-login)
- `refreshToken()` → rinnova i token usando il refresh token (con protezione chiamate concorrenti)
- `logout()` → revoca il token lato server, pulisce localStorage e signals, redirect a /login
- `getProfile()` → carica dati utente dall'endpoint /me
- `forgotPassword(request)` → chiama API forgot-password, ritorna Observable con messaggio di conferma
- `resetPassword(request)` → chiama API reset-password con email, token e nuova password
- `initializeAuth()` → ripristina lo stato auth da localStorage al boot dell'app (usato da `APP_INITIALIZER`)

SSR-safe: tutti gli accessi a `localStorage` sono protetti da `typeof window !== 'undefined'`.

### Interceptor HTTP

L'interceptor funzionale (`HttpInterceptorFn`):
1. Aggiunge `Authorization: Bearer <token>` a ogni richiesta
2. Se riceve un 401 (e non e' una richiesta di refresh/login):
   - Tenta un refresh automatico del token
   - Se il refresh ha successo, ripete la richiesta originale con il nuovo token
   - Se il refresh fallisce, esegue logout

### Pagine App (`frontend/web/projects/app/`)

| Pagina | Path | Guard | Descrizione |
|--------|------|-------|-------------|
| Login | `/login` | guestGuard | Form email + password con Angular Material |
| Register | `/register` | guestGuard | Form nome, cognome, email, password. Dopo invio mostra "controlla email" |
| Confirm Email | `/confirm-email` | guestGuard | Auto-chiama API con email+token dai query param. Mostra spinner/successo/errore |
| Forgot Password | `/forgot-password` | guestGuard | Form email per richiedere reset password |
| Reset Password | `/reset-password` | guestGuard | Form email + token + nuova password |
| Home | `/` | authGuard | Pagina protetta con info utente |

Layout: toolbar Material in cima con nome utente + bottone Logout (visibili solo se autenticato).

Tutte le rotte usano **lazy loading** con `loadComponent`.

---

## Persistenza sessione

L'autenticazione persiste tra refresh della pagina e riapertura del browser grazie a due meccanismi:

### APP_INITIALIZER

Al boot dell'applicazione Angular, un `APP_INITIALIZER` (`provideAuthInitializer()`) esegue `AuthService.initializeAuth()` **prima** che il routing venga attivato. Questo garantisce che:

1. I token vengono letti da `localStorage`
2. Se l'access token esiste, viene chiamato `GET /auth/me` per caricare il profilo utente
3. Se l'access token e' scaduto, l'interceptor esegue automaticamente il refresh
4. Solo dopo che lo stato auth e' stato determinato, Angular attiva il routing e i guard

Senza questo meccanismo, i guard valuterebbero `isAuthenticated()` prima che la chiamata HTTP ritorni, risultando in un redirect a `/login` anche con token validi.

### Protezione refresh concorrente

Se piu' richieste HTTP ricevono 401 contemporaneamente, il metodo `refreshToken()` condivide un'unica chiamata di refresh tra tutti i subscriber (tramite `shareReplay`). Questo evita che vengano fatte N chiamate di refresh in parallelo, che fallirebbero a causa della token rotation (il primo refresh revoca il token, i successivi troverebbero un token gia' revocato).

### Personalizzazione durate token

Per applicazioni che richiedono maggiore sicurezza, e' sufficiente modificare i valori in `appsettings.json`:

```json
{
  "JwtSettings": {
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

Non e' necessario modificare il codice: l'interceptor e il meccanismo di refresh funzionano indipendentemente dalla durata dei token.

---

## Configurazione

### Backend (`appsettings.json`)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=seeddb;Username=seed;Password=seed_password"
  },
  "JwtSettings": {
    "Secret": "YourSuperSecretKeyThatIsAtLeast32CharactersLong_ForDevelopmentOnly!",
    "Issuer": "SeedApp",
    "Audience": "SeedApp",
    "AccessTokenExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  }
}
```

> **Importante**: In produzione, la `Secret` deve essere spostata in `dotnet user-secrets` o variabili d'ambiente. Non committare mai secret reali.

### URL frontend nelle email (Client:BaseUrl)

Il backend costruisce i link di conferma email e reset password usando `Client:BaseUrl`:

```json
{
  "Client": {
    "BaseUrl": "https://tuodominio.com"
  }
}
```

- **Sviluppo** (`appsettings.json`): default `http://localhost:4200`
- **Produzione**: `Client__BaseUrl` viene derivato automaticamente da `DOMAIN_NAME` nel `docker-compose.deploy.yml` (`https://${DOMAIN_NAME}`). Non è necessaria una variabile separata — basta che `DOMAIN_NAME` sia impostato in `.env`.

> **Importante**: Se `DOMAIN_NAME` non è configurato in `.env`, i link nelle email non punteranno al dominio corretto.

### CORS

Il backend e' configurato per accettare richieste da `http://localhost:4200` (Angular dev server).

### Configurazione SMTP (per reset password)

```json
{
  "Smtp": {
    "Host": "",
    "Port": 587,
    "Username": "",
    "Password": "",
    "FromEmail": "noreply@seedapp.com",
    "FromName": "Seed App",
    "UseSsl": true
  }
}
```

**Auto-switch del servizio email:**
- Se `Smtp:Host` e' configurato (non vuoto) → viene usato `SmtpEmailService` (invio reale via MailKit)
- Se `Smtp:Host` e' vuoto o assente → viene usato `ConsoleEmailService` (logga il token su console, utile in sviluppo)

Non e' necessario modificare il codice: basta compilare la sezione `Smtp` in `appsettings.json` (o variabili d'ambiente) per attivare l'invio reale delle email.

**Dettagli email di reset:**
- Formato HTML con token in evidenza (l'utente lo copia e incolla nel form di reset)
- Il token scade dopo 1 ora (default ASP.NET Identity)
- L'email include un disclaimer: "If you did not request this, please ignore this email"

### Password Policy

- Minimo 8 caratteri
- Almeno una lettera maiuscola
- Almeno una lettera minuscola
- Almeno un numero
- Caratteri speciali non obbligatori

---

## Come testare

### 1. Avviare PostgreSQL

```bash
cd docker
docker compose up postgres -d
```

### 2. Applicare la migration

```bash
cd backend
dotnet ef database update --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

### 3. Avviare il backend

```bash
cd backend
dotnet run --project src/Seed.Api
```

L'API sara' disponibile su `http://localhost:5035/swagger`.

### 4. Avviare il frontend

```bash
cd frontend/web
ng build shared-auth    # necessario la prima volta
npm start
```

Il frontend sara' su `http://localhost:4200`.

### 5. Test del flusso di registrazione con verifica email

1. Aprire `http://localhost:4200` → redirect automatico a `/login`
2. Cliccare "Don't have an account? Register"
3. Compilare il form di registrazione e inviare
4. La pagina mostra "Controlla la tua email" — NON vengono emessi token JWT
5. Aprire Mailpit su `http://localhost:8025` per trovare l'email di verifica
6. Cliccare il link nell'email → reindirizza a `/confirm-email?email=...&token=...`
7. La pagina mostra uno spinner, poi "Email verificata!" e naviga alla home con auto-login
8. Provare a fare login con un account non verificato → messaggio di errore appropriato

### 6. Test del flusso reset password

1. Dalla pagina di login, cliccare "Forgot password?"
2. Inserire l'email dell'utente registrato e inviare
3. Se SMTP non configurato: cercare il token nei log della console del backend
4. Se SMTP configurato: controllare l'email ricevuta con il token
5. Navigare a `/reset-password`
6. Inserire email, token copiato, e nuova password (min 8 caratteri, maiuscola, minuscola, numero)
7. Dopo il successo, cliccare "Go to Login" e accedere con la nuova password

---

## Cancellazione Account

### Flusso

```
1. L'utente naviga alla pagina Profilo (/profile)
2. Clicca "Delete Account"
3. Si apre un dialog di conferma con:
   - Messaggio di avvertimento sulla perdita permanente dei dati
   - Campo password per conferma identità
   - Checkbox "Ho capito che questa azione è permanente"
4. Dopo la conferma:
   Client → DELETE /api/v1/auth/account { password: "..." }
   Server → verifica password
          → disattiva account (IsActive = false)
          → revoca tutti i refresh token attivi
          → ritorna 204 No Content
5. Frontend pulisce auth state (token, localStorage)
6. Redirect a /login
```

### Dettagli tecnici

- **Soft-delete**: l'account viene disattivato (`IsActive = false`), non eliminato dal DB. L'utente non potrà più effettuare login, refresh token, o reset password.
- **Conferma password**: richiesta per prevenire cancellazioni da sessioni rubate.
- **Revoca token**: tutti i refresh token attivi vengono revocati tramite `RevokeAllUserTokensAsync`.
- **Rate limiting**: l'endpoint usa la policy `auth-sensitive`.

### File coinvolti

- Backend: `Seed.Application/Auth/Commands/DeleteAccount/` (Command, Handler, Validator, Request)
- Backend: `Seed.Api/Controllers/AuthController.cs` (`DELETE /account`)
- Frontend: `shared-auth/services/auth.service.ts` (`deleteAccount()`)
- Frontend: `app/pages/profile/confirm-delete-dialog.ts` (dialog di conferma)
- Frontend: `app/pages/profile/profile.ts` (integrazione nel profilo)

---

## Cosa NON e' stato implementato (opzionale)

- Ruoli e autorizzazione basata su ruoli
- Cambio password
- Account lockout
- Re-invio email di verifica (utile se il link scade)
