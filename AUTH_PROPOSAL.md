# Proposta di Autenticazione e Autorizzazione

## Ha senso aggiungere l'autenticazione?

**Si, assolutamente.** Ci sono diversi motivi:

1. **Il progetto e' gia' predisposto.** I pacchetti NuGet per JWT Bearer e ASP.NET Identity sono gia' installati in `Seed.Api` e `Seed.Infrastructure`. La libreria Angular `shared-auth` esiste come placeholder vuoto. L'intenzione progettuale e' chiara.

2. **E' un seed/starter app.** Un template applicativo senza autenticazione ha un valore limitato: qualsiasi progetto reale che nasca da questo seed dovra' implementarla. Averla gia' pronta riduce drasticamente il tempo di bootstrap.

3. **L'architettura Clean e CQRS la supporta naturalmente.** MediatR + FluentValidation + Identity si integrano bene: i command handler gestiscono login/register, i validator controllano l'input, le pipeline behaviors possono gestire autorizzazione trasversale.

---

## Feature da implementare

### Fase 1 — Fondamenta (Backend)

#### 1.1 Entita' di dominio (`Seed.Domain`)
- `ApplicationUser` che estende `IdentityUser<Guid>` con campi aggiuntivi:
  - `FirstName`, `LastName`
  - `CreatedAt`, `UpdatedAt`
  - `IsActive` (soft-disable degli account)
- `ApplicationRole` che estende `IdentityRole<Guid>`

#### 1.2 DbContext e Identity (`Seed.Infrastructure`)
- `ApplicationDbContext` che estende `IdentityDbContext<ApplicationUser, ApplicationRole, Guid>`
- Configurazioni EF Core per le entita' (EntityTypeConfiguration)
- Migration iniziale con tabelle Identity

#### 1.3 Configurazione JWT (`Seed.Api`)
- Sezione `JwtSettings` in `appsettings.json`:
  - `Secret`, `Issuer`, `Audience`, `AccessTokenExpirationMinutes`, `RefreshTokenExpirationDays`
- Registrazione di `AddAuthentication()` + `AddJwtBearer()` in `Program.cs`
- Middleware `UseAuthentication()` + `UseAuthorization()`

#### 1.4 Generazione Token (`Seed.Infrastructure`)
- Servizio `ITokenService` / `TokenService`:
  - Generazione access token JWT con claims (userId, email, ruoli)
  - Generazione refresh token (stringa random opaca salvata su DB)
  - Validazione refresh token

---

### Fase 2 — Funzionalita' Core (Backend)

#### 2.1 Registrazione utente
- **Command:** `RegisterCommand` (Email, Password, FirstName, LastName)
- **Validator:** password minima 8 caratteri, email valida, email non duplicata
- **Handler:** crea utente via `UserManager`, assegna ruolo default, ritorna token pair
- **Endpoint:** `POST /api/v1/auth/register`

#### 2.2 Login
- **Command:** `LoginCommand` (Email, Password)
- **Validator:** campi obbligatori
- **Handler:** verifica credenziali via `SignInManager`, genera access + refresh token
- **Endpoint:** `POST /api/v1/auth/login`

#### 2.3 Refresh Token
- **Command:** `RefreshTokenCommand` (AccessToken, RefreshToken)
- **Handler:** valida refresh token, genera nuova coppia di token, invalida il vecchio refresh token (rotation)
- **Endpoint:** `POST /api/v1/auth/refresh`

#### 2.4 Logout
- **Command:** `LogoutCommand`
- **Handler:** invalida il refresh token corrente
- **Endpoint:** `POST /api/v1/auth/logout`

#### 2.5 Profilo utente
- **Query:** `GetCurrentUserQuery` — ritorna dati profilo dell'utente autenticato
- **Command:** `UpdateProfileCommand` — aggiorna FirstName, LastName
- **Endpoint:** `GET /api/v1/auth/me`, `PUT /api/v1/auth/me`

---

### Fase 3 — Frontend (`shared-auth` + `app`)

#### 3.1 Auth Service (`shared-auth`)
- `AuthService` con signals:
  - `currentUser: Signal<User | null>`
  - `isAuthenticated: Signal<boolean>`
  - `accessToken: Signal<string | null>`
- Metodi: `login()`, `register()`, `logout()`, `refreshToken()`, `getProfile()`
- Storage del token in `localStorage` (access token) e `httpOnly cookie` o `localStorage` (refresh token)

#### 3.2 HTTP Interceptor (`shared-auth`)
- `authInterceptor` (functional interceptor):
  - Aggiunge header `Authorization: Bearer <token>` a ogni richiesta API
  - Intercetta risposte 401, tenta refresh automatico, ripete la richiesta originale
  - Se il refresh fallisce, redirect al login

#### 3.3 Route Guard (`shared-auth`)
- `authGuard` — protegge rotte che richiedono autenticazione
- `guestGuard` — impedisce accesso a login/register se gia' autenticati
- Entrambi come functional guards (Angular 21 style)

#### 3.4 Pagine UI (`app`)
- **Login page** — form con email/password, link a registrazione, gestione errori
- **Register page** — form con nome, cognome, email, password, conferma password
- **Layout autenticato** — toolbar con nome utente e bottone logout

---

### Fase 4 — Sicurezza avanzata (opzionale ma consigliata)

#### 4.1 Autorizzazione basata su ruoli
- Ruoli predefiniti: `Admin`, `User`
- Seeding del ruolo Admin + utente admin in fase di migration/startup
- `[Authorize(Roles = "Admin")]` su endpoint protetti
- `RoleGuard` lato frontend

#### 4.2 Cambio e reset password
- `POST /api/v1/auth/change-password` — richiede password corrente + nuova
- `POST /api/v1/auth/forgot-password` — invia email con token di reset (richiede servizio email)
- `POST /api/v1/auth/reset-password` — valida token e imposta nuova password

#### 4.3 Conferma email
- Invio email di conferma alla registrazione
- `GET /api/v1/auth/confirm-email?token=...&email=...`
- Blocco login se email non confermata

#### 4.4 Rate limiting
- Limitare tentativi di login (es. 5 tentativi per IP in 15 minuti)
- Usare `Microsoft.AspNetCore.RateLimiting` integrato in .NET 10

#### 4.5 Account lockout
- Sfruttare il lockout integrato di ASP.NET Identity
- Dopo N tentativi falliti, blocco temporaneo dell'account

---

## Struttura file risultante

```
backend/src/
├── Seed.Domain/
│   └── Entities/
│       ├── ApplicationUser.cs
│       └── ApplicationRole.cs
├── Seed.Application/
│   ├── Common/
│   │   └── Interfaces/
│   │       └── ITokenService.cs
│   └── Features/
│       └── Auth/
│           ├── Commands/
│           │   ├── Register/
│           │   │   ├── RegisterCommand.cs
│           │   │   ├── RegisterCommandHandler.cs
│           │   │   └── RegisterCommandValidator.cs
│           │   ├── Login/
│           │   │   ├── LoginCommand.cs
│           │   │   ├── LoginCommandHandler.cs
│           │   │   └── LoginCommandValidator.cs
│           │   ├── RefreshToken/
│           │   ├── Logout/
│           │   └── UpdateProfile/
│           ├── Queries/
│           │   └── GetCurrentUser/
│           └── DTOs/
│               ├── AuthResponse.cs
│               └── UserDto.cs
├── Seed.Infrastructure/
│   ├── Persistence/
│   │   ├── ApplicationDbContext.cs
│   │   └── Configurations/
│   │       └── ApplicationUserConfiguration.cs
│   ├── Services/
│   │   └── TokenService.cs
│   └── Identity/
│       └── IdentityServiceExtensions.cs
└── Seed.Api/
    ├── Controllers/
    │   └── AuthController.cs
    └── Extensions/
        └── AuthExtensions.cs

frontend/web/projects/
├── shared-auth/src/lib/
│   ├── services/
│   │   └── auth.service.ts
│   ├── interceptors/
│   │   └── auth.interceptor.ts
│   ├── guards/
│   │   ├── auth.guard.ts
│   │   └── guest.guard.ts
│   └── models/
│       └── auth.models.ts
└── app/src/app/
    └── features/
        └── auth/
            ├── login/
            └── register/
```

## Priorita' consigliata

| Priorita' | Feature | Effort |
|-----------|---------|--------|
| P0 | Entita', DbContext, JWT config, Token service | Medio |
| P0 | Register, Login, Refresh, Logout (backend) | Medio |
| P0 | Auth service, Interceptor, Guards (frontend) | Medio |
| P0 | Login e Register pages (frontend) | Basso |
| P1 | Profilo utente (GET/PUT me) | Basso |
| P1 | Ruoli e autorizzazione | Basso |
| P2 | Cambio password | Basso |
| P2 | Rate limiting e account lockout | Basso |
| P3 | Forgot/reset password (richiede email service) | Medio |
| P3 | Conferma email | Medio |

## Note tecniche

- **Non servono pacchetti aggiuntivi**: tutto il necessario e' gia' referenziato nei `.csproj`.
- **Refresh token rotation**: ogni volta che si usa un refresh token, quello vecchio viene invalidato e ne viene generato uno nuovo. Questo limita i danni in caso di furto del token.
- **Signals Angular**: l'auth state deve usare `signal()` e `computed()` anziche' `BehaviorSubject`, come da convenzione del progetto.
- **Functional API Angular 21**: interceptor e guards devono usare le API funzionali (`HttpInterceptorFn`, `CanActivateFn`), non le classi.
