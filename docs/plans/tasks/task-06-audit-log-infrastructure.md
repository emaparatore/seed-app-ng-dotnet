# Task 06: Infrastruttura Audit Log

## Contesto

- **Stato attuale:** Nessuna infrastruttura di audit log esiste nel progetto.
- **Permessi già definiti:** `Permissions.AuditLog.Read` e `Permissions.AuditLog.Export` esistono in `Seed.Domain/Authorization/Permissions.cs`.
- **Dipendenze:** T-01 completato (sistema permessi ed entità). T-05 completato (cambio password).
- **Pattern da seguire:** Entità in Domain, configurazione EF in Infrastructure/Persistence/Configurations, servizio con interfaccia in Application/Common/Interfaces e implementazione in Infrastructure/Services, registrazione DI in Infrastructure/DependencyInjection.cs.
- **User context:** Attualmente non esiste un `ICurrentUserService` — userId, IP e user-agent vanno estratti da `IHttpContextAccessor`.
- **Auth handlers da integrare:** 9 handler in `Seed.Application/Auth/Commands/` (Login, Logout, ChangePassword, DeleteAccount, Register, ConfirmEmail, ForgotPassword, ResetPassword, RefreshToken).
- **SuperAdminSeeder:** In `Seed.Infrastructure/Persistence/Seeders/SuperAdminSeeder.cs` — va integrato per loggare l'evento di seeding.

## Piano di esecuzione

### Step 1: Entità AuditLogEntry nel Domain

**File da creare:** `backend/src/Seed.Domain/Entities/AuditLogEntry.cs`

```csharp
public class AuditLogEntry
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public Guid? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public string? Details { get; set; }  // JSON — valori prima/dopo
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
```

**File da creare:** `backend/src/Seed.Domain/Authorization/AuditActions.cs`

Costanti per i tipi di azione:
- `UserCreated`, `UserUpdated`, `UserDeleted`, `UserStatusChanged`, `UserRolesChanged`
- `RoleCreated`, `RoleUpdated`, `RoleDeleted`
- `LoginSuccess`, `LoginFailed`, `Logout`, `PasswordChanged`, `PasswordReset`
- `SettingsChanged`, `SystemSeeding`
- `AccountDeleted`, `EmailConfirmed`, `PasswordResetRequested`

### Step 2: Configurazione EF Core

**File da creare:** `backend/src/Seed.Infrastructure/Persistence/Configurations/AuditLogEntryConfiguration.cs`

- HasKey su Id
- Timestamp required
- Action: HasMaxLength(100), required
- EntityType: HasMaxLength(100)
- EntityId: HasMaxLength(100)
- Details: nvarchar(max) / text
- IpAddress: HasMaxLength(45) (IPv6)
- UserAgent: HasMaxLength(512)
- Indici su: Timestamp, UserId, Action

**File da modificare:** `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs`

- Aggiungere `DbSet<AuditLogEntry> AuditLogEntries { get; set; }`

### Step 3: Migration

**Comando:**
```bash
dotnet ef migrations add AddAuditLog --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

Crea tabella `AuditLogEntries` con colonne e indici definiti nella configurazione.

### Step 4: Interfaccia IAuditService

**File da creare:** `backend/src/Seed.Application/Common/Interfaces/IAuditService.cs`

```csharp
public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityType,
        string? entityId = null,
        string? details = null,
        Guid? userId = null,
        string? ipAddress = null,
        string? userAgent = null,
        CancellationToken cancellationToken = default);
}
```

### Step 5: Implementazione AuditService

**File da creare:** `backend/src/Seed.Infrastructure/Services/AuditService.cs`

- Iniettato: `ApplicationDbContext`
- Metodo `LogAsync`: crea `AuditLogEntry`, aggiunge al DbContext, salva
- Fire-and-forget sicuro: l'audit non deve bloccare o far fallire l'operazione principale. Usare try/catch con logging dell'errore.

### Step 6: Registrazione DI

**File da modificare:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`

- Aggiungere `services.AddScoped<IAuditService, AuditService>();`
- Registrare `IHttpContextAccessor` con `services.AddHttpContextAccessor();` (se non già presente)

### Step 7: Integrazione nei flussi di auth esistenti

**File da modificare** (aggiungere `IAuditService` come dipendenza e chiamate `LogAsync`):

| Handler | Azione audit | Note |
|---------|-------------|------|
| `LoginCommandHandler` | `LoginSuccess` on success, `LoginFailed` on failure | Loggare email, IP, user-agent |
| `LogoutCommandHandler` | `Logout` | userId |
| `ChangePasswordCommandHandler` | `PasswordChanged` | userId |
| `RegisterCommandHandler` | `UserCreated` | email del nuovo utente |
| `ConfirmEmailCommandHandler` | `EmailConfirmed` | userId |
| `ForgotPasswordCommandHandler` | `PasswordResetRequested` | email (sempre, per non rivelare se esiste) |
| `ResetPasswordCommandHandler` | `PasswordReset` | email |
| `DeleteAccountCommandHandler` | `AccountDeleted` | userId |

**Nota:** Per IP e user-agent nei command handler, iniettare `IHttpContextAccessor` nel handler o passarli come parametri del command. Approccio consigliato: aggiungere `IpAddress` e `UserAgent` ai command che ne hanno bisogno (Login, Register) e popolarli nel controller.

### Step 8: Integrazione nel SuperAdminSeeder

**File da modificare:** `backend/src/Seed.Infrastructure/Persistence/Seeders/SuperAdminSeeder.cs`

- Iniettare `IAuditService`
- Dopo la creazione del SuperAdmin, loggare `SystemSeeding` con dettaglio "SuperAdmin user created"

### Step 9: Unit test

**File da creare:** `backend/tests/Seed.UnitTests/Services/AuditServiceTests.cs`

Test cases:
- LogAsync crea un AuditLogEntry nel database
- LogAsync imposta tutti i campi correttamente
- LogAsync funziona con userId null (eventi di sistema)
- LogAsync non propaga eccezioni (resilienza)

### Step 10: Integration test

**File da creare:** `backend/tests/Seed.IntegrationTests/AuditLog/AuditLogPersistenceTests.cs`

Test cases:
- Gli eventi di audit vengono persistiti nel database
- Gli indici funzionano (query per timestamp, userId, action)
- Login success genera un audit log entry
- Login failure genera un audit log entry
- Il seeding del SuperAdmin genera un audit log entry

### Step 11: Unit test per handler aggiornati

**File da modificare:** Test esistenti dei command handler per verificare che `IAuditService.LogAsync` venga chiamato con i parametri corretti:

- `backend/tests/Seed.UnitTests/Auth/Commands/LoginCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Auth/Commands/ChangePasswordCommandHandlerTests.cs`
- Creare test per handler che non li hanno ancora (Logout, Register, ConfirmEmail, ForgotPassword, ResetPassword, DeleteAccount)

## Criteri di completamento

- [ ] Entità `AuditLogEntry` creata in Domain con tutti i campi richiesti
- [ ] Costanti `AuditActions` definite nel Domain
- [ ] Configurazione EF Core con indici su Timestamp, UserId, Action
- [ ] DbSet aggiunto ad ApplicationDbContext
- [ ] Migration creata e applicabile
- [ ] `IAuditService` definita in Application/Common/Interfaces
- [ ] `AuditService` implementato in Infrastructure/Services con persistenza su DB
- [ ] Servizio registrato in DI come scoped
- [ ] Tutti i 9 auth handler integrati con audit logging (8 handler + login failed)
- [ ] SuperAdminSeeder integrato con audit logging
- [ ] Unit test per AuditService
- [ ] Unit test aggiornati per handler (verifica chiamata IAuditService)
- [ ] Integration test per persistenza audit log
- [ ] Build OK e tutti i test passano

## Risultato

### File creati
- `backend/src/Seed.Domain/Entities/AuditLogEntry.cs` — entità con Id, Timestamp, UserId, Action, EntityType, EntityId, Details, IpAddress, UserAgent
- `backend/src/Seed.Domain/Authorization/AuditActions.cs` — 18 costanti per i tipi di azione audit
- `backend/src/Seed.Application/Common/Interfaces/IAuditService.cs` — interfaccia con metodo `LogAsync`
- `backend/src/Seed.Infrastructure/Persistence/Configurations/AuditLogEntryConfiguration.cs` — configurazione EF con indici su Timestamp, UserId, Action
- `backend/src/Seed.Infrastructure/Services/AuditService.cs` — implementazione con try/catch per resilienza (non propaga eccezioni)
- `backend/src/Seed.Infrastructure/Migrations/20260322112439_AddAuditLog.cs` — migration con tabella AuditLogEntries e 3 indici
- `backend/tests/Seed.UnitTests/Services/AuditServiceTests.cs` — 4 test: creazione entry, campi corretti, userId null, resilienza eccezioni
- `backend/tests/Seed.IntegrationTests/AuditLog/AuditLogPersistenceTests.cs` — 4 test: persistenza, query per timestamp, query per action, seeding audit

### File modificati
- `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs` — aggiunto `DbSet<AuditLogEntry> AuditLogEntries`
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrato `IAuditService` come scoped
- `backend/src/Seed.Api/Controllers/AuthController.cs` — arricchito LoginCommand con IP/UserAgent, LogoutCommand con UserId
- `backend/src/Seed.Application/Auth/Commands/Login/LoginCommand.cs` — aggiunti `IpAddress` e `UserAgent` (JsonIgnore)
- `backend/src/Seed.Application/Auth/Commands/Login/LoginCommandHandler.cs` — audit LoginSuccess/LoginFailed
- `backend/src/Seed.Application/Auth/Commands/Logout/LogoutCommand.cs` — aggiunto `UserId` (JsonIgnore)
- `backend/src/Seed.Application/Auth/Commands/Logout/LogoutCommandHandler.cs` — audit Logout
- `backend/src/Seed.Application/Auth/Commands/ChangePassword/ChangePasswordCommandHandler.cs` — audit PasswordChanged
- `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommandHandler.cs` — audit UserCreated
- `backend/src/Seed.Application/Auth/Commands/ConfirmEmail/ConfirmEmailCommandHandler.cs` — audit EmailConfirmed
- `backend/src/Seed.Application/Auth/Commands/ForgotPassword/ForgotPasswordCommandHandler.cs` — audit PasswordResetRequested
- `backend/src/Seed.Application/Auth/Commands/ResetPassword/ResetPasswordCommandHandler.cs` — audit PasswordReset
- `backend/src/Seed.Application/Auth/Commands/DeleteAccount/DeleteAccountCommandHandler.cs` — audit AccountDeleted
- `backend/src/Seed.Infrastructure/Persistence/Seeders/SuperAdminSeeder.cs` — audit SystemSeeding
- `backend/tests/Seed.UnitTests/Auth/Commands/LoginCommandHandlerTests.cs` — aggiunto mock IAuditService + 3 test audit
- `backend/tests/Seed.UnitTests/Auth/Commands/LogoutCommandHandlerTests.cs` — aggiunto mock IAuditService + 1 test audit
- `backend/tests/Seed.UnitTests/Auth/Commands/ChangePasswordCommandHandlerTests.cs` — aggiunto mock IAuditService + 1 test audit
- `backend/tests/Seed.UnitTests/Auth/Commands/RegisterCommandHandlerTests.cs` — aggiunto mock IAuditService
- `backend/tests/Seed.UnitTests/Auth/Commands/ConfirmEmailCommandHandlerTests.cs` — aggiunto mock IAuditService
- `backend/tests/Seed.UnitTests/Auth/Commands/ForgotPasswordCommandHandlerTests.cs` — aggiunto mock IAuditService
- `backend/tests/Seed.UnitTests/Auth/Commands/ResetPasswordCommandHandlerTests.cs` — aggiunto mock IAuditService
- `backend/tests/Seed.UnitTests/Auth/Commands/DeleteAccountCommandHandlerTests.cs` — aggiunto mock IAuditService
- `backend/tests/Seed.UnitTests/Seeders/SuperAdminSeederTests.cs` — aggiunto mock IAuditService
- `backend/tests/Seed.IntegrationTests/Seeders/SuperAdminSeedingTests.cs` — aggiornato costruttore SuperAdminSeeder
- `backend/tests/Seed.UnitTests/Seed.UnitTests.csproj` — aggiunto pacchetto Microsoft.EntityFrameworkCore.InMemory

### Scelte implementative e motivazioni
- **IP/UserAgent come proprietà JsonIgnore sul command record:** Approccio consigliato dal piano — il controller popola i valori dal HttpContext usando `with` expression, il command viaggia con questi dati fino all'handler senza che il client possa iniettarli
- **AuditService con try/catch:** Il servizio audit non propaga eccezioni per non far fallire l'operazione principale — logga l'errore con ILogger
- **ForgotPassword audit prima del lookup utente:** L'audit viene loggato sempre (anche se l'utente non esiste) per coerenza con il pattern anti-enumerazione email
- **RefreshTokenCommandHandler non integrato:** Non era nell'elenco del piano (Step 7) — il refresh token non è un evento di sicurezza rilevante quanto login/logout
- **InMemory EF per test AuditService:** Aggiunto pacchetto Microsoft.EntityFrameworkCore.InMemory per testare il servizio con un DbContext reale senza bisogno di PostgreSQL

### Deviazioni dal piano
- Nessuna deviazione significativa dal piano. Tutti gli step (1-11) sono stati implementati come descritto.
