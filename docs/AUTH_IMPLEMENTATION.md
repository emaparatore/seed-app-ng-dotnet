# Autenticazione JWT — Documentazione Implementazione

## Panoramica

E' stato implementato un sistema completo di autenticazione JWT con refresh token rotation, che copre sia il backend ASP.NET Core che il frontend Angular. Il sistema segue l'architettura Clean Architecture con pattern CQRS gia' presente nel progetto.

---

## Come funziona

### Flusso di autenticazione

```
1. REGISTRAZIONE / LOGIN
   Client → POST /api/v1/auth/register (o /login)
   Server → crea utente (o verifica credenziali)
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

### Application Layer (`Seed.Application/`)

| Cartella | Contenuto |
|----------|-----------|
| `Common/Result.cs` | Tipo generico `Result<T>` con Succeeded, Data, Errors |
| `Common/Models/` | AuthResponse, UserDto, TokenResult — record immutabili per le risposte |
| `Common/Interfaces/ITokenService.cs` | Interfaccia per generazione/refresh/revoca token |
| `DependencyInjection.cs` | Registra MediatR e FluentValidation |
| `Auth/Commands/Register/` | RegisterCommand + Validator + Handler |
| `Auth/Commands/Login/` | LoginCommand + Validator + Handler |
| `Auth/Commands/RefreshToken/` | RefreshTokenCommand + Handler |
| `Auth/Commands/Logout/` | LogoutCommand + Handler |
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
| `DependencyInjection.cs` | Registra DbContext, Identity, JwtSettings, TokenService |

### API Layer (`Seed.Api/`)

| File | Descrizione |
|------|-------------|
| `Controllers/AuthController.cs` | 5 endpoint sotto `/api/v1/auth/` |
| `Program.cs` | Configura JWT authentication, CORS, DI |
| `appsettings.json` | ConnectionStrings e JwtSettings |

### Endpoint API

| Metodo | Path | Auth | Descrizione |
|--------|------|------|-------------|
| POST | `/api/v1/auth/register` | No | Registrazione nuovo utente |
| POST | `/api/v1/auth/login` | No | Login con email/password |
| POST | `/api/v1/auth/refresh` | No | Rinnovo token con refresh token |
| POST | `/api/v1/auth/logout` | Si | Revoca del refresh token |
| GET | `/api/v1/auth/me` | Si | Dati dell'utente corrente |

---

## Struttura Frontend

### Libreria `shared-auth` (`frontend/web/projects/shared-auth/`)

| File | Descrizione |
|------|-------------|
| `models/auth.models.ts` | Interfacce TypeScript: User, LoginRequest, RegisterRequest, AuthResponse |
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
- `register(request)` → come login ma per registrazione
- `refreshToken()` → rinnova i token usando il refresh token (con protezione chiamate concorrenti)
- `logout()` → revoca il token lato server, pulisce localStorage e signals, redirect a /login
- `getProfile()` → carica dati utente dall'endpoint /me
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
| Register | `/register` | guestGuard | Form nome, cognome, email, password |
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

### CORS

Il backend e' configurato per accettare richieste da `http://localhost:4200` (Angular dev server).

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

### 5. Test del flusso

1. Aprire `http://localhost:4200` → redirect automatico a `/login`
2. Cliccare "Don't have an account? Register"
3. Compilare il form di registrazione e inviare
4. Redirect automatico alla home page con nome utente nella toolbar
5. Cliccare "Logout" → redirect a `/login`
6. Effettuare login con le credenziali create
7. Testare anche da Swagger: copiare l'access token e usarlo nel pulsante "Authorize"

---

## Cosa NON e' stato implementato (Fase 4 — opzionale)

- Ruoli e autorizzazione basata su ruoli
- Cambio password
- Reset password (richiede servizio email)
- Conferma email
- Rate limiting
- Account lockout

Queste feature sono descritte nel file `AUTH_PROPOSAL.md` e possono essere aggiunte in seguito.
