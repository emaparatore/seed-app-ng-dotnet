# Task 05: Cambio password obbligatorio (backend + frontend)

## Contesto

- `ApplicationUser` ha già il campo `MustChangePassword` (bool) — aggiunto in T-01
- `AuthResponse` include già `MustChangePassword` — aggiunto in T-03
- `LoginCommandHandler` popola già `MustChangePassword` nella risposta
- Il frontend `AuthResponse` TypeScript **non** include ancora `mustChangePassword` né `permissions`
- Non esiste ancora un endpoint `/auth/change-password`
- Non esiste middleware che blocchi le API quando il flag è attivo
- Non esiste guard frontend né pagina `/change-password`
- Pattern CQRS: command + handler + validator nella stessa cartella sotto `Application/Auth/Commands/`
- Pattern Result: `Result<T>` wrapper per success/failure
- Pattern guard Angular: funzioni che restituiscono `boolean | UrlTree`
- AuthInterceptor gestisce 401 con refresh automatico

## Piano di esecuzione

### Step 1: Backend — ChangePasswordCommand + Handler + Validator

**File da creare:**
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommand.cs`
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommandHandler.cs`
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommandValidator.cs`
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordRequest.cs`

**Approccio:**
- `ChangePasswordCommand`: record con `UserId` (string, dal ClaimsPrincipal), `CurrentPassword` (string), `NewPassword` (string)
- `ChangePasswordRequest`: record con `CurrentPassword`, `NewPassword` (DTO per il controller)
- Handler:
  1. Trova utente via `UserManager.FindByIdAsync(command.UserId)`
  2. Valida password attuale con `UserManager.CheckPasswordAsync()`
  3. Cambia password con `UserManager.ChangePasswordAsync()`
  4. Imposta `user.MustChangePassword = false` e salva con `UserManager.UpdateAsync()`
  5. Restituisce `Result.Success()`
- Validator FluentValidation:
  - `CurrentPassword`: non vuoto
  - `NewPassword`: non vuoto, min 8 caratteri, deve differire dalla password attuale
  - Allineato alle policy di sicurezza esistenti (vedi RegisterCommandValidator per pattern)

### Step 2: Backend — Endpoint in AuthController

**File da modificare:**
- `backend/src/Seed.Api/Controllers/AuthController.cs`

**Approccio:**
- Aggiungere `POST /api/v1/auth/change-password` endpoint
- `[Authorize]` — richiede autenticazione
- Rate limiting come altri endpoint sensibili
- Estrae `userId` da `User.FindFirstValue(ClaimTypes.NameIdentifier)`
- Crea `ChangePasswordCommand` e invia via MediatR
- Risponde 200 OK o errore

### Step 3: Backend — Middleware MustChangePassword

**File da creare:**
- `backend/src/Seed.Api/Middleware/MustChangePasswordMiddleware.cs`

**File da modificare:**
- `backend/src/Seed.Api/Program.cs` (registrazione middleware nel pipeline)

**Approccio:**
- Middleware che intercetta tutte le richieste autenticate
- Se l'utente ha claim `must_change_password` = `true` (aggiunto nel token JWT) OPPURE controlla il flag dal DB via un servizio leggero
- **Decisione:** usare un claim custom `must_change_password` nel JWT per evitare query DB ad ogni richiesta. Il claim è già disponibile perché LoginCommandHandler può includerlo. Al cambio password, il vecchio token viene invalidato (blacklist) e l'utente deve ri-loggarsi
- Endpoint esclusi dal blocco: path che contiene `/auth/change-password`, `/auth/logout`, `/auth/refresh-token`
- Se flag attivo: risponde 403 con body `{ "type": "PASSWORD_CHANGE_REQUIRED", "title": "Password change required", "status": 403 }`
- Posizionare il middleware **dopo** `UseAuthentication()` e `UseAuthorization()` in Program.cs

**Alternativa scelta (più semplice):** Invece di un claim JWT custom, il middleware legge `MustChangePassword` dal DB tramite `UserManager` solo per richieste autenticate a endpoint non-esclusi. Questo è più semplice e corretto perché:
  - Non richiede modifiche alla generazione del token
  - Il flag si aggiorna immediatamente dopo il cambio password (senza bisogno di nuovo token)
  - Il costo DB è accettabile (query per primary key, cached da EF)

### Step 4: Frontend — Aggiornare modelli TypeScript

**File da modificare:**
- `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts`

**Approccio:**
- Aggiungere `mustChangePassword: boolean` a `AuthResponse` interface
- Aggiungere `permissions: string[]` a `AuthResponse` interface (serve anche per T-13)
- Aggiungere `roles: string[]` a `User` interface se mancante

### Step 5: Frontend — Aggiornare AuthService per gestire mustChangePassword

**File da modificare:**
- `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts`

**Approccio:**
- Aggiungere signal `mustChangePassword = signal(false)`
- In `handleAuthResponse()`: settare `mustChangePassword` dal valore della risposta
- Aggiungere signal `permissions = signal<string[]>([])`
- In `handleAuthResponse()`: settare `permissions` dal valore della risposta
- Aggiungere metodo `changePassword(currentPassword: string, newPassword: string): Observable<void>`
- Persistere `mustChangePassword` in localStorage per sopravvivere al refresh
- In `initializeAuth()`: ripristinare il flag da localStorage
- In `logout()`: resettare il flag

### Step 6: Frontend — Guard mustChangePassword

**File da creare:**
- `frontend/web/projects/shared-auth/src/lib/guards/must-change-password.guard.ts`

**File da modificare:**
- `frontend/web/projects/shared-auth/src/public-api.ts` (export)

**Approccio:**
- Guard funzionale `mustChangePasswordGuard`
- Se `authService.mustChangePassword()` è `true` e la rotta corrente NON è `/change-password`: redirect a `/change-password`
- Se `mustChangePassword` è `false` e rotta è `/change-password`: redirect a `/` (non serve più)
- Applicare il guard alle rotte protette in `app.routes.ts`

### Step 7: Frontend — Pagina /change-password

**File da creare:**
- `frontend/web/projects/app/src/app/pages/change-password/change-password.ts`
- `frontend/web/projects/app/src/app/pages/change-password/change-password.html`
- `frontend/web/projects/app/src/app/pages/change-password/change-password.scss`

**File da modificare:**
- `frontend/web/projects/app/src/app/app.routes.ts` (aggiungere rotta)

**Approccio:**
- Standalone component con Angular Material (form fields, button)
- Form con: password attuale, nuova password, conferma nuova password
- Validazione client-side: campi obbligatori, min 8 char, nuova != attuale, conferma = nuova
- Submit chiama `authService.changePassword()`
- Successo: mostra toast, naviga a `/`
- Errore: mostra messaggio (password attuale errata, policy non rispettata, ecc.)
- Layout simile alle altre pagine auth (login, register)
- Rotta `/change-password` protetta da `authGuard` (deve essere autenticato)

### Step 8: Frontend — Gestione risposta 403 PASSWORD_CHANGE_REQUIRED

**File da modificare:**
- `frontend/web/projects/shared-auth/src/lib/interceptors/auth.interceptor.ts`

**Approccio:**
- Nell'interceptor, intercettare risposte 403 con body contenente `type: "PASSWORD_CHANGE_REQUIRED"`
- Settare `authService.mustChangePassword.set(true)`
- Redirect a `/change-password`

### Step 9: Frontend — Login redirect

**File da modificare:**
- `frontend/web/projects/app/src/app/pages/login/login.ts`

**Approccio:**
- Dopo login success, controllare `authService.mustChangePassword()`
- Se `true`, navigare a `/change-password` invece di `/`

### Step 10: Test

**File da creare:**
- `backend/tests/Seed.UnitTests/Auth/Commands/ChangePasswordCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Auth/Validators/ChangePasswordCommandValidatorTests.cs`
- `backend/tests/Seed.IntegrationTests/Auth/ChangePasswordTests.cs` (middleware + endpoint)
- `frontend/web/projects/shared-auth/src/lib/guards/must-change-password.guard.spec.ts`

**Test backend unitari (handler):**
- Cambio password con successo (flag rimosso)
- Password attuale errata → errore
- Utente non trovato → errore
- Verifica che MustChangePassword viene settato a false

**Test backend unitari (validator):**
- Password vuota → errore
- Nuova password troppo corta → errore
- Nuova password = password attuale → errore
- Dati validi → nessun errore

**Test backend integration (middleware):**
- Richiesta con MustChangePassword=true a endpoint generico → 403
- Richiesta con MustChangePassword=true a /auth/change-password → passa
- Richiesta con MustChangePassword=true a /auth/logout → passa
- Richiesta con MustChangePassword=false → passa

**Test frontend (guard):**
- mustChangePassword=true → redirect a /change-password
- mustChangePassword=false → passa

## Criteri di completamento

- [ ] Endpoint `POST /api/v1/auth/change-password` funzionante, cambia password e rimuove flag
- [ ] Middleware blocca tutte le API (tranne change-password, logout, refresh) se MustChangePassword=true, rispondendo 403 con tipo `PASSWORD_CHANGE_REQUIRED`
- [ ] Frontend: login con MustChangePassword=true redirige a /change-password
- [ ] Frontend: pagina /change-password con form funzionante (password attuale + nuova + conferma)
- [ ] Frontend: intercettore gestisce 403 PASSWORD_CHANGE_REQUIRED
- [ ] Frontend: guard impedisce navigazione ad altre pagine se flag attivo
- [ ] Unit test handler e validator passano
- [ ] Integration test middleware passano
- [ ] Frontend guard test passa
- [ ] Build backend OK (`dotnet build Seed.slnx`)
- [ ] Build frontend OK (`ng build`)
- [ ] Test backend OK (`dotnet test Seed.slnx`)
- [ ] Test frontend OK (`ng test`)

## Risultato

### File creati
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommand.cs` — record command con UserId, CurrentPassword, NewPassword
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommandHandler.cs` — handler che verifica password attuale, cambia password, rimuove flag MustChangePassword
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommandValidator.cs` — validazione: campi non vuoti, nuova password min 8 char con uppercase/lowercase/digit, diversa dalla attuale
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordRequest.cs` — DTO per il controller
- `backend/src/Seed.Api/Middleware/MustChangePasswordMiddleware.cs` — middleware che blocca richieste autenticate con 403 PASSWORD_CHANGE_REQUIRED se MustChangePassword=true, escludendo change-password, logout, refresh
- `backend/tests/Seed.UnitTests/Auth/Commands/ChangePasswordCommandHandlerTests.cs` — 5 test: successo con flag rimosso, utente non trovato, utente inattivo, password errata, errore Identity
- `backend/tests/Seed.UnitTests/Auth/Validators/ChangePasswordCommandValidatorTests.cs` — 8 test: validazione completa
- `backend/tests/Seed.IntegrationTests/Auth/ChangePasswordTests.cs` — 8 test: endpoint OK, flag rimosso, password errata, no auth, middleware blocca/permette
- `frontend/web/projects/shared-auth/src/lib/guards/must-change-password.guard.ts` — guard funzionale
- `frontend/web/projects/shared-auth/src/lib/guards/must-change-password.guard.spec.ts` — 4 test
- `frontend/web/projects/app/src/app/pages/change-password/change-password.ts` — componente standalone
- `frontend/web/projects/app/src/app/pages/change-password/change-password.html` — form con current/new/confirm password
- `frontend/web/projects/app/src/app/pages/change-password/change-password.scss` — stili coerenti con altre pagine auth

### File modificati
- `backend/src/Seed.Api/Controllers/AuthController.cs` — aggiunto endpoint POST change-password con rate limiting auth-sensitive
- `backend/src/Seed.Api/Program.cs` — registrato MustChangePasswordMiddleware dopo UseAuthorization()
- `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts` — aggiunto `roles` a User, `permissions` e `mustChangePassword` a AuthResponse, nuova interface ChangePasswordRequest
- `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts` — aggiunto signals mustChangePassword/permissions, metodi changePassword/setMustChangePassword, persistenza in localStorage
- `frontend/web/projects/shared-auth/src/lib/interceptors/auth.interceptor.ts` — intercettazione 403 PASSWORD_CHANGE_REQUIRED con redirect
- `frontend/web/projects/shared-auth/src/public-api.ts` — export del nuovo guard
- `frontend/web/projects/app/src/app/app.routes.ts` — aggiunta rotta /change-password, applicato mustChangePasswordGuard a rotte protette
- `frontend/web/projects/app/src/app/pages/login/login.ts` — redirect a /change-password dopo login se flag attivo
- `frontend/web/projects/app/src/app/pages/login/login.spec.ts` — aggiunto mustChangePassword al mock AuthService
- `frontend/web/projects/shared-auth/src/lib/services/auth.service.spec.ts` — aggiornato mockAuthResponse con nuovi campi

### Scelte implementative e motivazioni
- **Middleware DB-based (non claim JWT):** come indicato nel piano, il middleware legge `MustChangePassword` dal DB tramite `UserManager.FindByIdAsync()` invece di usare un claim JWT. Questo è più semplice e il flag si aggiorna immediatamente dopo il cambio password senza bisogno di un nuovo token.
- **`UserManager.ChangePasswordAsync()`:** usato al posto di rimuovere/aggiungere password manualmente, perché gestisce correttamente le policy di Identity (complessità, storia password, ecc.)
- **Validator allineato a RegisterCommandValidator:** stesse regole di complessità (uppercase, lowercase, digit, min 8 char) più il controllo che la nuova password sia diversa dalla attuale.
- **localStorage per mustChangePassword:** il flag viene persistito in localStorage per sopravvivere al refresh della pagina, come da piano.
- **Guard bidirezionale:** il mustChangePasswordGuard impedisce sia la navigazione ad altre pagine quando il flag è attivo, sia l'accesso a /change-password quando il flag è falso.

### Deviazioni dal piano
- Nessuna deviazione significativa dal piano.
