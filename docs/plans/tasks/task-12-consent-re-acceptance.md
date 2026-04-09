# Task 12: Re-accettazione consenso dopo aggiornamento Privacy Policy

## Contesto

### Stato attuale del codice rilevante

**Backend:**
- `LoginCommandHandler` (`backend/src/Seed.Application/Auth/Commands/Login/LoginCommandHandler.cs`) restituisce `AuthResponse` dopo verifica credenziali. NON controlla la versione del consenso.
- `AuthResponse` (in `Application/Auth/Common/`) include già `MustChangePassword` (bool) — pattern da replicare per il consenso.
- `ApplicationUser` ha già i campi `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` (aggiunti in T-03).
- `PrivacySettings` (`Shared/Configuration/PrivacySettings.cs`) ha `ConsentVersion` (default "1.0"), registrato in DI.
- `AuditActions.ConsentGiven` esiste già nel Domain layer.
- `AuthController` (`Api/Controllers/AuthController.cs`) ha tutti gli endpoint auth — non ha endpoint per aggiornamento consenso.

**Frontend:**
- `AuthResponse` interface (`shared-auth/src/lib/models/auth.models.ts`) include `mustChangePassword` — pattern da replicare.
- `AuthService` (`shared-auth/src/lib/services/auth.service.ts`) gestisce `_mustChangePassword` signal e `handleAuthResponse()`.
- Login component (`app/src/app/pages/login/login.ts`) fa routing condizionale: se `mustChangePassword` → `/change-password`, altrimenti → `/`.
- Login template (`login.html`) — form con email/password + error handling.

### Dipendenze e vincoli
- Dipende da T-04 (✅ Done) — campi consenso su registrazione
- Pattern consolidato: `MustChangePassword` flag su `AuthResponse` + routing condizionale nel login component
- Il dialog deve mostrare link a Privacy Policy e Terms of Service (route `/privacy-policy` e `/terms-of-service` esistono da T-01)
- Se l'utente rifiuta il dialog → logout automatico (chiamare `authService.logout()`)

## Piano di esecuzione

### Step 1: Backend — Aggiungere campi ad AuthResponse

**File:** `backend/src/Seed.Application/Auth/Common/AuthResponse.cs` (o dove è definito)
- Aggiungere `ConsentUpdateRequired` (bool) e `CurrentConsentVersion` (string?) alla risposta

### Step 2: Backend — Modificare LoginCommandHandler

**File:** `backend/src/Seed.Application/Auth/Commands/Login/LoginCommandHandler.cs`
- Iniettare `IOptions<PrivacySettings>` nel costruttore
- Dopo login riuscito, confrontare `user.ConsentVersion` con `_privacySettings.ConsentVersion`
- Se diversi (o user.ConsentVersion è null), impostare `ConsentUpdateRequired = true` e `CurrentConsentVersion = _privacySettings.ConsentVersion` nella risposta
- Se uguali, `ConsentUpdateRequired = false`

### Step 3: Backend — Creare AcceptUpdatedConsent command

**Directory:** `backend/src/Seed.Application/Auth/Commands/AcceptUpdatedConsent/`
**File da creare:**
- `AcceptUpdatedConsentCommand.cs` — record con `UserId` (Guid)
- `AcceptUpdatedConsentCommandHandler.cs` — handler che:
  1. Trova l'utente via `UserManager`
  2. Aggiorna `PrivacyPolicyAcceptedAt = DateTime.UtcNow`, `TermsAcceptedAt = DateTime.UtcNow`, `ConsentVersion = privacySettings.ConsentVersion`
  3. Salva via `UserManager.UpdateAsync`
  4. Logga audit event `AuditActions.ConsentGiven` con details "Consent re-accepted for version X"
  5. Restituisce `Result<bool>.Success(true)`
- `AcceptUpdatedConsentCommandValidator.cs` — valida UserId non vuoto

### Step 4: Backend — Aggiungere endpoint in AuthController

**File:** `backend/src/Seed.Api/Controllers/AuthController.cs`
- Aggiungere `POST /api/v1.0/auth/accept-updated-consent` con `[Authorize]`
- Estrae `userId` dal claim come negli altri endpoint autenticati
- Invia `AcceptUpdatedConsentCommand(userId)`
- Restituisce `Ok()` o `BadRequest(errors)`

### Step 5: Frontend — Aggiornare modelli e AuthService

**File:** `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts`
- Aggiungere `consentUpdateRequired?: boolean` e `currentConsentVersion?: string` a `AuthResponse`

**File:** `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts`
- Aggiungere signal `_consentUpdateRequired = signal(false)`
- Aggiungere computed/getter `consentUpdateRequired`
- In `handleAuthResponse()`, impostare `_consentUpdateRequired` dal response
- Aggiungere metodo `acceptUpdatedConsent(): Observable<void>` che chiama `POST /auth/accept-updated-consent`
- Nel metodo `acceptUpdatedConsent`, dopo successo, resettare `_consentUpdateRequired.set(false)`

### Step 6: Frontend — Creare ConsentUpdateDialog

**Directory:** `frontend/web/projects/app/src/app/pages/login/`
**File da creare:** `consent-update-dialog.ts`
- Componente standalone con template inline (pattern come `ConfirmDeleteDialog`)
- Mostra titolo "Privacy Policy Updated"
- Testo: informare che la privacy policy è stata aggiornata e bisogna ri-accettare
- Link a `/privacy-policy` e `/terms-of-service` (con `routerLink`)
- Pulsante "Accept" che chiude il dialog con `true`
- Pulsante "Decline" che chiude il dialog con `false`

**File da creare:** `consent-update-dialog.spec.ts`
- Test: componente si crea
- Test: Accept chiude con true
- Test: Decline chiude con false

### Step 7: Frontend — Integrare dialog nel login component

**File:** `frontend/web/projects/app/src/app/pages/login/login.ts`
- Iniettare `MatDialog`
- Dopo login riuscito, se `authService.consentUpdateRequired()`:
  1. Aprire `ConsentUpdateDialog`
  2. Se utente accetta → chiamare `authService.acceptUpdatedConsent()`, poi navigare normalmente
  3. Se utente rifiuta (o chiude dialog) → chiamare `authService.logout()`, mostrare messaggio
- Flusso di priorità: prima check `mustChangePassword`, poi check `consentUpdateRequired`

### Test da scrivere/verificare

**Unit test backend:**
1. `LoginCommandHandlerTests` — login con versione consenso diversa restituisce `ConsentUpdateRequired = true`
2. `LoginCommandHandlerTests` — login con versione consenso uguale restituisce `ConsentUpdateRequired = false`
3. `LoginCommandHandlerTests` — login con `ConsentVersion = null` restituisce `ConsentUpdateRequired = true`
4. `AcceptUpdatedConsentCommandHandlerTests` — aggiorna consenso correttamente (timestamp + versione)
5. `AcceptUpdatedConsentCommandHandlerTests` — utente non trovato → fallisce
6. `AcceptUpdatedConsentCommandHandlerTests` — audit log scritto

**Test frontend:**
7. `ConsentUpdateDialog` — 3 test (creazione, accept, decline)
8. `Login` — test che dialog appare quando `consentUpdateRequired` è true
9. `AuthService` — test per `acceptUpdatedConsent()` method

**Comandi di verifica:**
```bash
# Backend
cd backend && dotnet test Seed.slnx

# Frontend
cd frontend/web && ng test app && ng test shared-auth && ng build
```

## Criteri di completamento

- `AuthResponse` include `ConsentUpdateRequired` (bool) e `CurrentConsentVersion` (string)
- `LoginCommandHandler` confronta `user.ConsentVersion` con la versione da config e imposta il flag
- Endpoint `POST /api/v1.0/auth/accept-updated-consent` funzionante e protetto da `[Authorize]`
- Handler aggiorna timestamp di consenso e `ConsentVersion` sull'utente
- Audit log registra evento di ri-accettazione consenso
- Frontend: `AuthResponse` model aggiornato con i nuovi campi
- Frontend: dialog appare dopo login se `consentUpdateRequired === true`
- Frontend: Accept → chiama endpoint → naviga normalmente
- Frontend: Decline → logout automatico
- Tutti i test passano (`dotnet test Seed.slnx`, `ng test app`, `ng test shared-auth`, `ng build`)

## Risultato

- File modificati/creati:
  - `backend/src/Seed.Application/Common/Models/AuthResponse.cs` — aggiunto `ConsentUpdateRequired` (bool) e `CurrentConsentVersion` (string?) al record
  - `backend/src/Seed.Application/Auth/Commands/Login/LoginCommandHandler.cs` — iniettato `IOptions<PrivacySettings>`, confronto `user.ConsentVersion` con config, flag sulla risposta
  - `backend/src/Seed.Application/Auth/Commands/AcceptUpdatedConsent/AcceptUpdatedConsentCommand.cs` — nuovo command record
  - `backend/src/Seed.Application/Auth/Commands/AcceptUpdatedConsent/AcceptUpdatedConsentCommandValidator.cs` — validazione UserId non vuoto
  - `backend/src/Seed.Application/Auth/Commands/AcceptUpdatedConsent/AcceptUpdatedConsentCommandHandler.cs` — handler che aggiorna timestamp consenso, versione, e logga audit
  - `backend/src/Seed.Api/Controllers/AuthController.cs` — aggiunto endpoint `POST /api/v1.0/auth/accept-updated-consent` con `[Authorize]`
  - `backend/tests/Seed.UnitTests/Auth/Commands/LoginCommandHandlerTests.cs` — aggiornato costruttore con `PrivacySettings`, aggiunti 3 test per consent version (diversa, uguale, null)
  - `backend/tests/Seed.UnitTests/Auth/Commands/AcceptUpdatedConsentCommandHandlerTests.cs` — nuovo file con 3 test (aggiorna campi, utente non trovato, audit log)
  - `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts` — aggiunto `consentUpdateRequired?` e `currentConsentVersion?` a `AuthResponse`
  - `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts` — aggiunto signal `_consentUpdateRequired`, metodo `acceptUpdatedConsent()`, aggiornato `handleAuthResponse()` e `clearAuth()`
  - `frontend/web/projects/shared-auth/src/lib/services/auth.service.spec.ts` — aggiunto test per `acceptUpdatedConsent()`
  - `frontend/web/projects/app/src/app/pages/login/consent-update-dialog.ts` — nuovo componente dialog standalone
  - `frontend/web/projects/app/src/app/pages/login/consent-update-dialog.spec.ts` — 3 test (creazione, accept, decline)
  - `frontend/web/projects/app/src/app/pages/login/login.ts` — integrato `MatDialog` e `ConsentUpdateDialog`, logica condizionale dopo login
  - `frontend/web/projects/app/src/app/pages/login/login.spec.ts` — aggiornato mock AuthService con nuovi campi, aggiunto test per consent dialog

- Scelte implementative e motivazioni:
  - `AuthResponse` usa parametri con default (`ConsentUpdateRequired = false`, `CurrentConsentVersion = null`) per retrocompatibilita con gli handler esistenti (RefreshToken, ConfirmEmail) che non passano questi campi
  - `CurrentConsentVersion` viene restituito solo quando `ConsentUpdateRequired = true` per evitare di esporre informazioni non necessarie
  - Il dialog usa `disableClose: true` per impedire la chiusura cliccando fuori, forzando una scelta esplicita (accept/decline)
  - Priorita nel login: prima `mustChangePassword`, poi `consentUpdateRequired` — il cambio password e piu critico per la sicurezza

- Eventuali deviazioni dal piano e perche':
  - Nessuna deviazione significativa dal piano
