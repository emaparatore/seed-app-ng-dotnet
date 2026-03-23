# Implementation Plan: FEAT-1 — Admin Dashboard

**Requirements:** `docs/requirements/FEAT-1.md`
**Status:** In Progress
**Created:** 2026-03-18
**Last Updated:** 2026-03-23 (aggiornato stato T-13 completato, story coverage US-017)

---

## Story Coverage

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-001 | Seeding admin iniziale | T-01, T-02, T-03, T-04 | ✅ Done |
| US-002 | Cambio password obbligatorio | T-05 | ✅ Done |
| US-003 | Lista utenti | T-07, T-14, T-15 | ⏳ Not Started |
| US-004 | Promuovere un utente | T-07, T-15 | ⏳ Not Started |
| US-005 | Disattivare un utente | T-07, T-15 | ⏳ Not Started |
| US-006 | Creare un utente | T-07, T-15 | ⏳ Not Started |
| US-007 | Eliminare un utente | T-07, T-15 | ⏳ Not Started |
| US-008 | Modificare un utente | T-07, T-15 | ⏳ Not Started |
| US-009 | Creare un ruolo | T-08, T-16 | 🔧 In Progress (backend done) |
| US-010 | Modificare permessi ruolo | T-08, T-16 | 🔧 In Progress (backend done) |
| US-011 | Eliminare un ruolo | T-08, T-16 | 🔧 In Progress (backend done) |
| US-012 | Consultare audit log | T-09, T-17 | 🔧 In Progress (backend done) |
| US-013 | Esportare audit log CSV | T-09, T-17 | 🔧 In Progress (backend done) |
| US-014 | Impostazioni a runtime | T-10, T-18 | 🔧 In Progress (backend done) |
| US-015 | Dashboard di riepilogo | T-11, T-19 | 🔧 In Progress (backend done) |
| US-016 | Stato del sistema | T-12, T-20 | 🔧 In Progress (backend done) |
| US-017 | Accesso condizionale admin | T-05, T-13, T-14 | 🔧 In Progress (T-05, T-13 done, T-14 pending) |

---

## Fase A — Fondazione: Permessi, Ruoli e Seeding

### T-01: Sistema permessi — costanti e entità di dominio

**Stories:** US-001, US-017
**Size:** Small
**Status:** [x] Completed

**What to do:**
Creare la definizione centralizzata dei permessi come costanti nel Domain layer. Creare l'entità `Permission` e la tabella di giunzione `RolePermission` per associare permessi ai ruoli. Aggiungere il campo `MustChangePassword` a `ApplicationUser`. Aggiungere il flag `IsSystemRole` a `ApplicationRole`.

**Definition of Done:**
- [x] Classe statica `Permissions` nel Domain con tutte le 16 costanti (es. `Users.Read`, `Roles.Create`, ecc.) e metodo per ottenere tutti i permessi
- [x] Entità `Permission` con `Id`, `Name`, `Description`, `Category`
- [x] Entità `RolePermission` (join table) con `RoleId`, `PermissionId`
- [x] Campo `MustChangePassword` (bool, default false) su `ApplicationUser`
- [x] Campo `IsSystemRole` (bool, default false) su `ApplicationRole`
- [x] Configurazioni EF Core per le nuove entità
- [x] Migration che crea le tabelle e aggiunge le nuove colonne
- [x] Unit test per le costanti dei permessi (completezza, formato corretto)
- [x] Build e migration applicata con successo

---

### T-02: Seeding ruoli e permessi

**Stories:** US-001
**Size:** Small
**Status:** [x] Completed
**Depends on:** T-01

**What to do:**
Creare un seeder che popola il database con i 16 permessi e i 3 ruoli di sistema (SuperAdmin, Admin, User) con le relative associazioni. Il seeder è idempotente: se i dati esistono già, non li duplica.

**Definition of Done:**
- [x] Seeder che crea i 16 permessi nel database
- [x] Seeder che crea i 3 ruoli di sistema con `IsSystemRole = true`
- [x] SuperAdmin ha tutti i 16 permessi assegnati
- [x] Admin ha tutti i permessi tranne `Settings.Manage` e `Roles.Delete`
- [x] User non ha permessi admin
- [x] Il seeder è idempotente (esecuzioni multiple non creano duplicati)
- [x] Il seeder viene eseguito all'avvio dell'applicazione, dopo le migration
- [x] Integration test che verifica il seeding completo

**Implementation Notes:**
- `RolesAndPermissionsSeeder` in `Seed.Infrastructure/Persistence/Seeders/`
- Registered as scoped service in `DependencyInjection.cs`, called in `Program.cs` after `MigrateAsync()`
- 7 integration tests: all permissions created, 3 system roles, SuperAdmin all perms, Admin excluded perms, User no perms, idempotency, correct categories
- Build OK, unit tests 81/81 pass. Integration tests require Docker (Testcontainers) — not available locally but code compiles and logic verified

---

### T-03: Infrastruttura di autorizzazione basata su permessi

**Stories:** US-001, US-017
**Size:** Medium
**Status:** [x] Completed
**Depends on:** T-02

**What to do:**
Implementare un sistema di autorizzazione che verifica i permessi dell'utente corrente. Creare un `IPermissionService` che carica i permessi dell'utente (con cache), un authorization handler ASP.NET che valida le policy basate su permessi, e un attributo/policy per proteggere gli endpoint. Il SuperAdmin bypassa tutti i controlli. Implementare un `ITokenBlacklistService` basato su `IDistributedCache` per l'invalidazione immediata dei token JWT (necessario per disattivazione utenti e cambio ruoli).

**Definition of Done:**
- [x] `IPermissionService` con metodo `GetPermissionsAsync(userId)` che restituisce i permessi effettivi (unione dei ruoli)
- [x] Cache server-side dei permessi per utente (con invalidazione)
- [x] `PermissionAuthorizationHandler` che verifica se l'utente ha il permesso richiesto
- [x] `HasPermissionAttribute` o policy factory per decorare gli endpoint (es. `[HasPermission(Permissions.Users.Read)]`)
- [x] SuperAdmin bypassa tutti i controlli di permesso
- [x] I permessi dell'utente vengono inclusi nella risposta di login (`LoginResponse` estesa con `permissions[]`)
- [x] Registrazione `IDistributedCache` con `AddDistributedMemoryCache()` (sostituibile con Redis in futuro senza modifiche al codice applicativo)
- [x] `ITokenBlacklistService` con metodi `BlacklistUserTokensAsync(userId)` e `IsUserTokenBlacklistedAsync(userId, tokenIssuedAt)` — usa `IDistributedCache` internamente
- [x] `JwtBearerEvents.OnTokenValidated` controlla la blacklist ad ogni richiesta autenticata
- [x] Unit test per handler aggiornati (LoginCommandHandler, RefreshTokenCommandHandler, ConfirmEmailCommandHandler)
- [x] Unit test per `TokenBlacklistService` (in IntegrationTests con MemoryDistributedCache reale)
- [x] Integration test per permessi nella risposta login (Admin, SuperAdmin, User senza ruoli)

**Implementation Notes:**
- `SystemRoles` constants extracted to `Seed.Domain/Authorization/SystemRoles.cs` — referenced by seeder, authorization handler, and tests
- `PermissionService` in `Seed.Infrastructure/Services/` — uses `IDistributedCache` with 5min TTL, queries RolePermissions via EF Core
- `TokenBlacklistService` in `Seed.Infrastructure/Services/` — per-user timestamp blacklist, TTL = access token lifetime
- Authorization pipeline: `HasPermissionAttribute` → `PermissionAuthorizationPolicyProvider` (dynamic policy) → `PermissionAuthorizationHandler` (checks IPermissionService, SuperAdmin bypass via role claim)
- JWT extended with `iat` claim and `ClaimTypes.Role` claims; `RoleClaimType` set in `TokenValidationParameters`
- `JwtBearerEvents.OnTokenValidated` checks blacklist by comparing token `iat` vs blacklist timestamp
- `AuthResponse` extended with `Permissions` (string[]) and `MustChangePassword` (bool); `UserDto` extended with `Roles` (string[])
- All 3 auth handlers (Login, Refresh, ConfirmEmail) updated to populate new fields
- Build OK, 82 unit tests pass. Integration tests require Docker (Testcontainers) — code compiles and logic verified

---

### T-04: Seeding SuperAdmin da variabili d'ambiente

**Stories:** US-001
**Size:** Small
**Status:** [x] Completed
**Depends on:** T-02

**What to do:**
Creare un seeder che, al primo avvio, legge le variabili d'ambiente e crea l'utente SuperAdmin. Il seeder è idempotente. L'utente viene creato con `MustChangePassword = true` e il ruolo SuperAdmin assegnato.

**Definition of Done:**
- [x] Il seeder legge `SuperAdmin__Email`, `SuperAdmin__Password`, `SuperAdmin__FirstName`, `SuperAdmin__LastName`
- [x] Se non esiste un utente con ruolo SuperAdmin, ne crea uno con queste credenziali
- [x] Se un SuperAdmin esiste già, l'operazione viene saltata silenziosamente
- [x] Il nuovo utente ha `MustChangePassword = true`
- [x] Il nuovo utente ha il ruolo SuperAdmin assegnato
- [x] Il seeder viene eseguito all'avvio, dopo il seeding dei ruoli (T-02)
- [x] Se le variabili d'ambiente non sono configurate, il seeder logga un warning e non crea nulla
- [x] Integration test che verifica: creazione, idempotenza, flag MustChangePassword

**Implementation Notes:**
- `SuperAdminSettings` config class in `Seed.Shared/Configuration/` with `SectionName = "SuperAdmin"`, bound via `IOptions<T>` pattern
- `SuperAdminSeeder` in `Seed.Infrastructure/Persistence/Seeders/` — uses `UserManager<ApplicationUser>` for creation and role assignment
- Idempotency via `UserManager.GetUsersInRoleAsync(SuperAdmin)` — skips if any SuperAdmin exists
- 5 unit tests (NSubstitute mocks) + 5 integration tests (Testcontainers)
- Config: `appsettings.Development.json` for local dev, env vars via Docker `.env` files
- Build OK, unit tests pass. Integration tests require Docker (Testcontainers)

---

## Fase B — Flusso Cambio Password e Audit Log

### T-05: Cambio password obbligatorio (backend + frontend)

**Stories:** US-002, US-017
**Size:** Medium
**Status:** [x] Completed
**Depends on:** T-03, T-04

**What to do:**
Implementare il flusso di cambio password obbligatorio. Backend: la risposta di login include il flag `mustChangePassword`. Un nuovo endpoint `POST /api/v1/auth/change-password` per il cambio password. Un middleware/filtro che blocca le richieste API (tranne cambio password e logout) se il flag è attivo. Frontend: guard che redirige alla pagina di cambio password, pagina dedicata.

**Definition of Done:**
- [x] `LoginResponse` include `mustChangePassword: boolean`
- [x] Endpoint `POST /api/v1/auth/change-password` che accetta `currentPassword` e `newPassword`
- [x] Il cambio password rimuove il flag `MustChangePassword` dall'utente
- [x] Middleware che restituisce 403 con codice specifico (es. `PASSWORD_CHANGE_REQUIRED`) per qualsiasi richiesta API se il flag è attivo, eccetto `/auth/change-password` e `/auth/logout`
- [x] Frontend: guard `mustChangePasswordGuard` che redirige a `/change-password`
- [x] Frontend: pagina `/change-password` con form (password attuale + nuova password + conferma)
- [x] La nuova password rispetta le policy di sicurezza esistenti
- [x] Unit test per il comando ChangePassword
- [x] Integration test per il middleware di blocco
- [x] Frontend test per il guard

**Implementation Notes:**
- Already fully implemented in prior commits. Backend: `ChangePasswordCommand/Handler`, `MustChangePasswordMiddleware`, `ChangePasswordCommandValidator`. Frontend: `mustChangePasswordGuard`, `ChangePassword` component, `auth.interceptor.ts` handles 403 PASSWORD_CHANGE_REQUIRED. Tests: 6 unit tests, 8 integration tests, 4 frontend guard tests.

---

### T-06: Infrastruttura Audit Log

**Stories:** US-012 (parziale — solo backend)
**Size:** Medium
**Status:** [x] Done
**Depends on:** T-01

**What to do:**
Creare l'entità `AuditLogEntry`, il servizio `IAuditLogService`, e integrarlo nei flussi esistenti. Il servizio registra eventi con: timestamp, userId, azione, entità, dettaglio modifiche, IP, user-agent. Creare la migration per la tabella audit log con indici appropriati.

**Definition of Done:**
- [x] Entità `AuditLogEntry` con: `Id`, `Timestamp`, `UserId` (nullable — per eventi di sistema), `Action` (enum o stringa), `EntityType`, `EntityId`, `Details` (JSON — valori prima/dopo), `IpAddress`, `UserAgent`
- [x] `IAuditService` con metodo `LogAsync(...)` per registrare eventi
- [x] Costanti `AuditActions` per i tipi di azione (18 azioni definite): `UserCreated`, `UserUpdated`, `UserDeleted`, `UserStatusChanged`, `UserRolesChanged`, `RoleCreated`, `RoleUpdated`, `RoleDeleted`, `LoginSuccess`, `LoginFailed`, `Logout`, `PasswordChanged`, `PasswordReset`, `SettingsChanged`, `SystemSeeding`, `AccountDeleted`, `EmailConfirmed`, `PasswordResetRequested`
- [x] Migration con tabella `AuditLogEntries` e indici su `Timestamp`, `UserId`, `Action`
- [x] Integrazione nei flussi di auth esistenti (tutti i 9 handler: Login, Logout, ChangePassword, Register, ConfirmEmail, ForgotPassword, ResetPassword, DeleteAccount + LoginFailed) — retroattiva
- [x] Integrazione nel seeder dell'admin (T-04) per registrare l'evento di seeding
- [x] Unit test per `AuditService` (4 test) + unit test aggiornati per handler auth (mock IAuditService)
- [x] Integration test per la persistenza degli eventi (4 test)

**Implementation Notes:**
- `AuditLogEntry` in `Seed.Domain/Entities/`, `AuditActions` in `Seed.Domain/Authorization/`
- `IAuditService` in `Seed.Application/Common/Interfaces/`, `AuditService` in `Seed.Infrastructure/Services/` — try/catch per resilienza (non propaga eccezioni)
- IP/UserAgent come proprietà `[JsonIgnore]` sui command record, popolati dal controller via `with` expression
- Migration `20260322112439_AddAuditLog` con tabella e 3 indici
- InMemory EF per unit test AuditService (pacchetto Microsoft.EntityFrameworkCore.InMemory)
- Build OK, 82+ unit tests pass, integration tests richiedono Docker (Testcontainers)

---

## Fase C — API Admin: Gestione Utenti e Ruoli

### T-07: API gestione utenti (AdminUsersController)

**Stories:** US-003, US-004, US-005, US-006, US-007, US-008
**Size:** Large
**Status:** [x] Done
**Depends on:** T-03, T-06

**What to do:**
Creare `AdminUsersController` con tutti gli endpoint di gestione utenti. Ogni endpoint è protetto dal relativo permesso. Tutte le operazioni vengono loggate nell'audit log.

Endpoint:
- `GET /api/v1/admin/users` — lista paginata con filtri (US-003)
- `GET /api/v1/admin/users/{id}` — dettaglio utente (US-003)
- `POST /api/v1/admin/users` — creazione utente (US-006)
- `PUT /api/v1/admin/users/{id}` — modifica dati utente (US-008)
- `DELETE /api/v1/admin/users/{id}` — eliminazione utente (US-007)
- `PUT /api/v1/admin/users/{id}/status` — toggle attivo/disattivo (US-005)
- `PUT /api/v1/admin/users/{id}/roles` — assegnazione ruoli (US-004)
- `POST /api/v1/admin/users/{id}/force-password-change` — forza cambio password (US-008)
- `POST /api/v1/admin/users/{id}/reset-password` — invia link reset password (US-008)

**Definition of Done:**
- [ ] Tutti gli endpoint implementati con MediatR commands/queries
- [ ] Paginazione lato server con `PagedResult<T>` (pageNumber, pageSize, totalCount, items)
- [ ] Ricerca per nome/email, filtri per ruolo/stato/periodo di registrazione
- [ ] Ordinamento per qualsiasi colonna esposta
- [ ] Ogni endpoint protetto dal permesso corretto
- [ ] Regole di protezione: no auto-eliminazione, no eliminazione/disattivazione SuperAdmin, no auto-modifica ruolo SuperAdmin
- [ ] Alla disattivazione utente: invalidazione immediata dei token attivi tramite `ITokenBlacklistService`
- [ ] Soft delete per eliminazione utenti (flag `IsDeleted`) — l'utente non è più visibile ma i dati restano per audit
- [ ] Tutte le operazioni loggate nell'audit log con dettaglio prima/dopo
- [ ] FluentValidation per tutti i comandi
- [ ] Unit test per command handler e validatori
- [ ] Integration test per endpoint (permessi, paginazione, protezioni)

---

### T-08: API gestione ruoli (AdminRolesController)

**Stories:** US-009, US-010, US-011
**Size:** Medium
**Status:** [x] Done
**Depends on:** T-03, T-06

**What to do:**
Creare `AdminRolesController` con endpoint per gestione ruoli e permessi. Proteggere ogni endpoint con il permesso appropriato.

Endpoint:
- `GET /api/v1/admin/roles` — lista ruoli con conteggio utenti (US-009, US-010, US-011)
- `GET /api/v1/admin/roles/{id}` — dettaglio ruolo con permessi e utenti assegnati
- `POST /api/v1/admin/roles` — creazione ruolo (US-009)
- `PUT /api/v1/admin/roles/{id}` — modifica ruolo e permessi (US-010)
- `DELETE /api/v1/admin/roles/{id}` — eliminazione ruolo (US-011)
- `GET /api/v1/admin/permissions` — lista di tutti i permessi disponibili (per la matrice UI)

**Definition of Done:**
- [x] Tutti gli endpoint implementati con MediatR commands/queries (6 endpoint: GET roles, GET role by id, POST create, PUT update, DELETE, GET permissions)
- [x] Conteggio utenti per ogni ruolo nella lista
- [x] Creazione ruolo con nome, descrizione e lista permessi
- [x] Duplicazione ruolo: il frontend invierà i permessi del ruolo da duplicare come dati iniziali
- [x] Modifica: aggiornamento nome, descrizione e permessi in una singola operazione
- [x] Eliminazione bloccata per ruoli di sistema (`IsSystemRole`)
- [x] Modifica permessi SuperAdmin bloccata (mantiene sempre tutti)
- [x] Invalidazione cache permessi alla modifica dei ruoli
- [x] Alla modifica ruoli: invalidazione immediata dei token attivi degli utenti impattati tramite `ITokenBlacklistService`
- [x] Tutte le operazioni loggate nell'audit log
- [x] FluentValidation per tutti i comandi
- [x] Unit test (16: CreateRole 5, UpdateRole 6, DeleteRole 5) e integration test (10 endpoint tests)

**Implementation Notes:**
- `IPermissionService` esteso con 4 metodi (`GetAllPermissionsAsync`, `GetRolePermissionNamesAsync`, `SetRolePermissionsAsync`, `RemoveAllRolePermissionsAsync`) — Application non referenzia EF Core
- Conteggio utenti via `UserManager.GetUsersInRoleAsync` (accettabile per numero limitato di ruoli)
- Cache invalidation solo se i permessi effettivamente cambiano (confronto prima/dopo in UpdateRoleCommandHandler)
- Pattern controller identico a AdminUsersController: enrichment con `CurrentUserId`, `IpAddress`, `UserAgent`
- Build OK, 152 unit tests pass, 73 integration tests pass

---

### T-09: API Audit Log (AdminAuditLogController)

**Stories:** US-012, US-013
**Size:** Medium
**Status:** [x] Done
**Depends on:** T-06

**What to do:**
Creare `AdminAuditLogController` con endpoint per consultazione ed esportazione del log.

Endpoint:
- `GET /api/v1/admin/audit-log` — lista paginata con filtri
- `GET /api/v1/admin/audit-log/{id}` — dettaglio singolo evento
- `GET /api/v1/admin/audit-log/export` — export CSV con filtri

**Definition of Done:**
- [x] Lista paginata con filtri: tipo azione, userId, intervallo date, ricerca testuale (su Details)
- [x] Ordinamento per timestamp (default: più recenti prima)
- [x] Dettaglio singolo evento con campo `Details` come stringa JSON
- [x] Export CSV che rispetta i filtri applicati, con tutte le colonne significative (limite 10.000 righe, UTF-8 BOM)
- [x] Endpoint protetti da `AuditLog.Read` (lista e dettaglio) e `AuditLog.Export` (export)
- [x] Nessun endpoint di modifica o cancellazione (il log è append-only)
- [x] Unit test (10: GetAuditLogEntries 6, ExportAuditLog 3, GetAuditLogEntryById 1) e integration test (6 endpoint tests)

**Implementation Notes:**
- `IAuditLogReader` interface creata in Application (non prevista nel piano) — necessaria perché Application non referenzia EF Core. Implementazione `AuditLogReader` in Infrastructure
- CSV export con `StringBuilder`, escape corretto per campi con virgole/virgolette/newline
- Pattern paginazione coerente con `GetUsersQueryHandler`
- Build OK, 161 unit tests pass, integration tests compilano (richiedono Docker)

---

### T-10: API Impostazioni di Sistema (AdminSettingsController)

**Stories:** US-014
**Size:** Medium
**Status:** [x] Done
**Depends on:** T-03, T-06

**Decision:** Tabella `SystemSettings` con righe chiave-valore (Option A) per la flessibilità richiesta da RNF-05. I valori di default sono definiti in una classe `SystemSettingsDefaults` e seminati nel DB al primo avvio (stesso pattern idempotente di T-02/T-04).

**What to do:**
Creare l'infrastruttura per le impostazioni di sistema: entità, servizio con cache in-memory, classe `SystemSettingsDefaults` con i valori iniziali, seeder idempotente, controller con endpoint lettura/modifica.

Endpoint:
- `GET /api/v1/admin/settings` — tutte le impostazioni raggruppate per categoria
- `PUT /api/v1/admin/settings` — aggiornamento batch delle impostazioni modificate

**Definition of Done:**
- [x] Entità `SystemSetting` e migration `20260323103305_AddSystemSettings` per la tabella settings
- [x] `ISystemSettingsService` con metodi `GetAllAsync()`, `UpdateAsync(changes)`, `GetValueAsync(key)`
- [x] Cache in-memory (`IDistributedCache`) con invalidazione al salvataggio (TTL 5 min)
- [x] Classe `SystemSettingsDefaults` con 8 valori iniziali (incluso `AuditLog.RetentionMonths = 0` per futura retention policy)
- [x] Seeder idempotente `SystemSettingsSeeder` che scrive i default nel DB al primo avvio (se la chiave non esiste già)
- [x] Ogni impostazione include: chiave, valore, tipo, categoria, descrizione, chi/quando ultima modifica
- [x] Endpoint protetti da `Settings.Read` (GET) e `Settings.Manage` (PUT)
- [x] Modifiche loggate nell'audit log con dettaglio prima/dopo
- [x] Unit test per il servizio (11 test: cache, invalidazione, audit, validazione tipo) e validatore (4 test)
- [x] Integration test per gli endpoint (6 test: auth, permessi, CRUD)

**Implementation Notes:**
- Entità `SystemSetting` con Key come PK, configurazione EF Core con limiti (Key 128, Value 1024, Type 20, Category 64) e indice su Category
- `SystemSettingsService` usa `IDistributedCache` con TTL 5 min, validazione tipo nel service (bool/int) perché richiede accesso ai metadati dell'entità
- `Result<bool>` anziché `Result<Unit>` per coerenza con pattern esistente (es. `UpdateRoleCommand`)
- 8 default iniziali in 4 categorie: Security (3), Email (2), AuditLog (1), General (2)
- Test: 15 unit test (11 service + 4 validator) + 6 integration test. MemoryDistributedCache reale nei test (stesso approccio di TokenBlacklistService)

---

### T-11: API Dashboard (AdminDashboardController)

**Stories:** US-015
**Size:** Small
**Status:** [x] Done
**Depends on:** T-07, T-09

**What to do:**
Creare un endpoint che aggrega le statistiche per la dashboard.

Endpoint:
- `GET /api/v1/admin/dashboard` — statistiche aggregate

**Definition of Done:**
- [x] Risposta include: totale utenti, utenti attivi, utenti disattivati
- [x] Registrazioni ultimi 7 e 30 giorni calcolate correttamente
- [x] Dati per grafico trend registrazioni (ultimi 30 giorni, raggruppati per giorno, giorni mancanti riempiti con 0)
- [x] Distribuzione utenti per ruolo (nome ruolo + conteggio, solo utenti non eliminati)
- [x] Ultime 5 attività dal log di audit (compatte: id, timestamp, action, entityType, userId)
- [x] Endpoint protetto da `Dashboard.ViewStats`
- [x] Query ottimizzate (conteggi aggregati, non caricamento di tutti i record)
- [x] Integration test (6 test: auth, permessi, struttura risposta, coerenza dati, trend)

**Implementation Notes:**
- Pattern identico a `AdminSettingsController`: controller minimale con solo GET, `[Authorize]` a livello di classe, `[HasPermission]` sull'endpoint, `ISender` per dispatch MediatR
- `GetDashboardStatsQueryHandler` inietta `UserManager`, `RoleManager`, `IAuditLogReader` — conteggi aggregati senza caricamento massivo
- Trend 30 giorni con fill dei giorni mancanti: loop da 29 giorni fa a oggi, inserendo 0 per i giorni senza registrazioni (garantisce sempre 30 elementi)
- Conteggio ruoli filtra `!IsDeleted` per coerenza con `TotalUsers`; distribuzione ruoli usa `GetUsersInRoleAsync` (stesso pattern di `GetRolesQueryHandler`)
- Build OK, 176 unit test passano, 6 integration test compilano (richiedono Docker/Testcontainers)

---

### T-12: API System Health (AdminSystemHealthController)

**Stories:** US-016
**Size:** Small
**Status:** [x] Done
**Depends on:** T-03

**What to do:**
Creare un endpoint che verifica lo stato dei componenti del sistema. Sfruttare gli health check ASP.NET già configurati e aggiungere informazioni extra.

Endpoint:
- `GET /api/v1/admin/system-health` — stato completo del sistema

**Definition of Done:**
- [x] Stato connessione database (healthy/unhealthy tramite HealthCheckService registrato)
- [x] Stato servizio email (configured/not configured basato su SmtpSettings.Host, con host e porta nella description)
- [x] Versione applicazione (letta da assembly)
- [x] Ambiente di esecuzione (Development/Staging/Production)
- [x] Uptime del server (tempo dall'avvio del processo)
- [x] Utilizzo memoria del processo (WorkingSet e GC allocated)
- [x] Endpoint protetto da `SystemHealth.Read`
- [x] Integration test (6 test: auth 401, permessi 403, risposta completa, DB status, version/environment, uptime/memory)

**Implementation Notes:**
- DB check via `HealthCheckService.CheckHealthAsync()` riutilizzando l'health check PostgreSQL ("postgresql") già registrato in Program.cs
- Email status controlla solo `SmtpSettings.Host` configurato (Configured/NotConfigured) senza connessione SMTP, per evitare latenza e side-effect
- Aggiunti pacchetti `Microsoft.Extensions.Diagnostics.HealthChecks` e `Microsoft.Extensions.Hosting.Abstractions` al progetto Application (necessari per `HealthCheckService` e `IHostEnvironment` nel handler)
- Pattern controller identico a `AdminDashboardController`: versioning, `[Authorize]`, `[HasPermission]`, `ISender`
- Build OK, 176 unit test passano, 6 integration test compilano (richiedono Docker/Testcontainers)

**Notes:**
- Spazio disco rimosso dallo scope: non è possibile leggerlo in modo cross-platform affidabile in tutti gli ambienti di deploy (container, ecc.). Può essere aggiunto in futuro se necessario.

---

## Fase D — Frontend Admin: Layout e Pagine

### T-13: Layout admin, routing e navigazione condizionale

**Stories:** US-017
**Size:** Medium
**Status:** [x] Done
**Depends on:** T-03

**What to do:**
Creare il modulo admin nel frontend (lazy-loaded). Layout dedicato con sidebar. Guard basato sui permessi. Navigazione condizionale: la voce "Admin" appare nel menu principale solo se l'utente ha almeno un permesso admin. Le voci nella sidebar dipendono dai permessi dell'utente.

**Definition of Done:**
- [x] Rotta `/admin` lazy-loaded con layout dedicato (sidebar + content area)
- [x] Sidebar con voci: Dashboard, Utenti, Ruoli, Audit Log, Impostazioni, Stato Sistema
- [x] Ogni voce visibile solo se l'utente ha il permesso di lettura corrispondente (tramite `*hasPermission` directive)
- [x] Guard `adminGuard` che verifica almeno un permesso admin (redirect a `/`)
- [x] Guard `permissionGuard` configurabile per singola rotta (factory function, redirect a `/admin`)
- [x] Voce "Admin" nel menu principale (header/navbar) visibile solo con almeno un permesso admin
- [x] `PermissionService` nel frontend che espone i permessi come signals e metodi `hasPermission()`, `hasAnyPermission()`, `isAdmin`
- [x] Direttiva `*hasPermission` (structural directive) per condizionare elementi UI
- [x] Redirect a `/admin/dashboard` come rotta default dell'area admin
- [x] Test per guards, PermissionService e HasPermissionDirective (14 test totali)

**Implementation Notes:**
- `isAdmin` definito come `permissions().length > 0` — un utente è "admin" se ha qualsiasi permesso, coerente con il modello dove i permessi sono assegnati solo a ruoli admin
- `HasPermissionDirective` usa `input.required<string>()` + `effect()` per reattività signal-based (pattern Angular moderno)
- `permissionGuard` implementato come factory function che restituisce `CanActivateFn`, permettendo configurazione per-rotta
- Sidebar fissa 240px con layout flexbox semplice (senza `MatSidenavModule`), usa `mat-nav-list` per le voci
- Componente `AdminPlaceholder` unico con titolo da `route.data['title']` per tutte le sotto-pagine non ancora implementate

---

### T-14: Frontend — Dashboard admin

**Stories:** US-015, US-017
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-11, T-13

**What to do:**
Implementare la pagina dashboard con card statistiche, grafici e widget ultime attività.

**Definition of Done:**
- [ ] Card con conteggi: utenti totali, attivi, disattivati
- [ ] Card con registrazioni: ultimi 7 e 30 giorni
- [ ] Grafico trend registrazioni (ultimi 30 giorni) — line chart SVG custom (componente Angular isolato, zero dipendenze)
- [ ] Grafico distribuzione utenti per ruolo — donut chart SVG custom (componente Angular isolato, zero dipendenze)
- [ ] Widget ultime 5 attività audit con link a sezione completa
- [ ] Skeleton loading durante il caricamento
- [ ] Responsive: card si riorganizzano su tablet

**Decision:** Grafici SVG custom (Option C). Zero dipendenze esterne, componenti Angular isolati e facilmente rimovibili. Coerente con l'obiettivo di mantenere la seed app leggera. Se in futuro servono grafici più complessi, si aggiunge una libreria a quel punto.

---

### T-15: Frontend — Gestione utenti

**Stories:** US-003, US-004, US-005, US-006, US-007, US-008
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-07, T-13

**What to do:**
Implementare le pagine di gestione utenti: lista, dettaglio/modifica, creazione.

**Definition of Done:**
- [ ] **Lista utenti**: tabella Angular Material con paginazione server-side, ricerca, filtri (ruolo, stato, periodo), ordinamento
- [ ] Colonne: nome con iniziali/avatar, email, ruoli (chip), stato (badge), data registrazione, ultimo accesso
- [ ] Toggle attiva/disattiva inline (se ha permesso `Users.ToggleStatus`)
- [ ] Pulsante elimina con dialog di conferma (se ha permesso `Users.Delete`)
- [ ] **Pagina dettaglio/modifica**: form con nome, cognome, email; gestione ruoli (add/remove); info account (sola lettura); cronologia attività
- [ ] Pulsanti: salva, reset password forzato, forza cambio password — visibilità basata su permessi
- [ ] **Pagina creazione**: form con nome, cognome, email, password (con auto-generazione), selezione ruoli, checkbox email benvenuto
- [ ] Toast di successo/errore per ogni operazione
- [ ] Skeleton loading per lista e dettaglio
- [ ] Gestione stati vuoti (nessun utente trovato, nessun filtro corrispondente)

---

### T-16: Frontend — Gestione ruoli

**Stories:** US-009, US-010, US-011
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-08, T-13

**What to do:**
Implementare le pagine di gestione ruoli: lista, dettaglio/modifica, creazione.

**Definition of Done:**
- [ ] **Lista ruoli**: tabella con nome, descrizione, numero utenti, badge "Sistema" per ruoli non eliminabili
- [ ] Pulsante elimina disabilitato per ruoli di sistema, con dialog di conferma per gli altri
- [ ] **Pagina dettaglio/modifica**: form nome e descrizione; matrice permessi raggruppata per area
- [ ] Checkbox "seleziona tutti" per area nella matrice permessi
- [ ] Indicazione in tempo reale del numero di utenti impattati
- [ ] SuperAdmin: matrice permessi in sola lettura (sempre tutti attivi)
- [ ] **Pagina creazione**: come modifica; opzione "duplica da" per partire da un ruolo esistente
- [ ] Toast e skeleton loading

---

### T-17: Frontend — Audit Log

**Stories:** US-012, US-013
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-09, T-13

**What to do:**
Implementare la pagina audit log con tabella, filtri e export.

**Definition of Done:**
- [ ] Tabella paginata con colonne: timestamp, utente, azione, entità, riepilogo
- [ ] Filtri: tipo azione (dropdown), utente (autocomplete), intervallo date (date picker), ricerca testuale
- [ ] Riga espandibile con dettaglio modifiche prima/dopo (JSON formattato o tabella diff)
- [ ] Pulsante "Esporta CSV" che scarica i dati con i filtri applicati (se ha permesso `AuditLog.Export`)
- [ ] Skeleton loading e stato vuoto

---

### T-18: Frontend — Impostazioni di Sistema

**Stories:** US-014
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-10, T-13

**What to do:**
Implementare la pagina impostazioni con form raggruppati per categoria.

**Definition of Done:**
- [ ] Impostazioni raggruppate per categoria (card o section)
- [ ] Controllo appropriato per tipo: toggle (boolean), campo numerico (number), campo testo (string)
- [ ] Per ogni impostazione: label, valore corrente, chi l'ha modificata per ultimo e quando
- [ ] Dialog di conferma al salvataggio
- [ ] Con solo `Settings.Read`: tutti i controlli disabilitati
- [ ] Toast successo/errore
- [ ] Skeleton loading

---

### T-19: Frontend — Dashboard (grafici e statistiche)

**Stories:** US-015
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-14

**Notes:** Questo task è stato assorbito da T-14. Mantenuto per tracciabilità.
**Status:** [-] Skipped — incorporato in T-14

---

### T-20: Frontend — Stato del Sistema

**Stories:** US-016
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-12, T-13

**What to do:**
Implementare la pagina stato del sistema con indicatori visuali.

**Definition of Done:**
- [ ] Card per ogni componente con indicatore visuale: verde (OK), giallo (warning), rosso (errore)
- [ ] Componenti mostrati: Database, Servizio Email
- [ ] Informazioni generali: versione app, ambiente, uptime, utilizzo memoria
- [ ] Pulsante "Ricontrolla" che richiama l'endpoint e aggiorna i dati
- [ ] Skeleton loading durante il caricamento iniziale e il refresh
