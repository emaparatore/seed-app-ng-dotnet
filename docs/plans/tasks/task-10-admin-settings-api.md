# Task 10: API Impostazioni di Sistema (AdminSettingsController)

## Contesto

- Stato attuale: nessun file relativo a System Settings esiste nel codebase
- Le costanti `Permissions.Settings.Read` e `Permissions.Settings.Manage` sono già definite in `Seed.Domain/Authorization/Permissions.cs`
- L'azione audit `AuditActions.SettingsChanged` è già definita in `Seed.Domain/Authorization/AuditActions.cs`
- L'infrastruttura di caching (`IDistributedCache`) è già registrata in `DependencyInjection.cs` con `AddDistributedMemoryCache()`
- Pattern di riferimento: AdminRolesController, PermissionService (cache), RolesAndPermissionsSeeder (seeding idempotente)
- Dipendenze completate: T-03 (autorizzazione), T-06 (audit log)

## Piano di esecuzione

### Step 1: Entità Domain — `SystemSetting`

**File da creare:** `backend/src/Seed.Domain/Entities/SystemSetting.cs`

```
Proprietà:
- Key (string, PK) — es. "Security.MaxLoginAttempts"
- Value (string) — valore serializzato come stringa
- Type (string) — "bool", "int", "string"
- Category (string) — raggruppamento logico es. "Security", "Email", "AuditLog"
- Description (string?) — descrizione leggibile
- ModifiedBy (Guid?) — ultimo utente che ha modificato
- ModifiedAt (DateTime?) — data ultima modifica
```

### Step 2: Configurazione EF Core

**File da creare:** `backend/src/Seed.Infrastructure/Persistence/Configurations/SystemSettingConfiguration.cs`
- PK su `Key` (max 128 char)
- `Value` required, max 1024 char
- `Type` required, max 20 char
- `Category` required, max 64 char
- Indice su `Category`

### Step 3: DbSet e Migration

**File da modificare:** `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs`
- Aggiungere `DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();`

**Migration da creare:** `AddSystemSettings` — tabella `SystemSettings` con indice su `Category`

### Step 4: Defaults e Seeder

**File da creare:** `backend/src/Seed.Domain/Authorization/SystemSettingsDefaults.cs`
- Classe statica con tutti i default iniziali:
  - `Security.MaxLoginAttempts` = "5" (int)
  - `Security.LockoutDurationMinutes` = "15" (int)
  - `Security.PasswordMinLength` = "8" (int)
  - `Email.SendWelcomeEmail` = "true" (bool)
  - `Email.SendPasswordResetNotification` = "true" (bool)
  - `AuditLog.RetentionMonths` = "0" (int) — 0 = nessuna retention
  - `General.MaintenanceMode` = "false" (bool)
  - `General.AppName` = "Seed App" (string)
- Metodo `GetAll()` che restituisce `IReadOnlyList<SystemSettingDefault>` con Key, Value, Type, Category, Description

**File da creare:** `backend/src/Seed.Infrastructure/Persistence/Seeders/SystemSettingsSeeder.cs`
- Pattern identico a `RolesAndPermissionsSeeder`: controlla chiavi esistenti, aggiunge solo le mancanti
- Registrare in `DependencyInjection.cs` come scoped service
- Chiamare in `Program.cs` dopo gli altri seeder

### Step 5: Interface Application layer — `ISystemSettingsService`

**File da creare:** `backend/src/Seed.Application/Common/Interfaces/ISystemSettingsService.cs`
```
Task<IReadOnlyList<SystemSettingDto>> GetAllAsync(CancellationToken)
Task<Result<Unit>> UpdateAsync(IReadOnlyList<UpdateSettingItem> changes, Guid modifiedBy, string? ipAddress, string? userAgent, CancellationToken)
Task<string?> GetValueAsync(string key, CancellationToken)
```

### Step 6: DTOs e modelli

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Models/SystemSettingDto.cs`
```
Key, Value, Type, Category, Description, ModifiedBy (Guid?), ModifiedAt (DateTime?)
```

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Models/UpdateSettingItem.cs`
```
Key (string), Value (string)
```

### Step 7: Query — GetSystemSettings

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Queries/GetSystemSettings/GetSystemSettingsQuery.cs`
- `IRequest<Result<IReadOnlyList<SystemSettingDto>>>`
- Nessun parametro

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Queries/GetSystemSettings/GetSystemSettingsQueryHandler.cs`
- Usa `ISystemSettingsService.GetAllAsync()`

### Step 8: Command — UpdateSystemSettings

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Commands/UpdateSystemSettings/UpdateSystemSettingsCommand.cs`
```
Items: List<UpdateSettingItem>
CurrentUserId, IpAddress, UserAgent (audit context, [JsonIgnore])
Returns: Result<Unit>
```

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Commands/UpdateSystemSettings/UpdateSystemSettingsCommandValidator.cs`
- Items non vuoto
- Ogni item: Key non vuoto, Value non null

**File da creare:** `backend/src/Seed.Application/Admin/Settings/Commands/UpdateSystemSettings/UpdateSystemSettingsCommandHandler.cs`
- Delega a `ISystemSettingsService.UpdateAsync()` che gestisce: validazione chiavi, audit log (prima/dopo), invalidazione cache

### Step 9: Implementazione Infrastructure — `SystemSettingsService`

**File da creare:** `backend/src/Seed.Infrastructure/Services/SystemSettingsService.cs`
- Implementa `ISystemSettingsService`
- Dipendenze: `ApplicationDbContext`, `IDistributedCache`, `IAuditService`
- `GetAllAsync()`: cache-first con chiave `"system_settings:all"`, TTL 5 min, fallback a DB query
- `UpdateAsync()`: aggiorna DB, logga audit con dettaglio prima/dopo per ogni setting modificato, invalida cache
- `GetValueAsync()`: carica da GetAll cached, cerca per chiave
- Registrare in `DependencyInjection.cs`

### Step 10: Controller

**File da creare:** `backend/src/Seed.Api/Controllers/AdminSettingsController.cs`
- `GET /api/v1/admin/settings` — protetto da `Settings.Read` — chiama `GetSystemSettingsQuery`
- `PUT /api/v1/admin/settings` — protetto da `Settings.Manage` — chiama `UpdateSystemSettingsCommand`
- Pattern identico a AdminRolesController (enrichment audit context)

### Step 11: Test

**File da creare:** `backend/tests/Seed.UnitTests/Admin/Settings/SystemSettingsServiceTests.cs`
- Test cache hit (ritorna da cache senza query DB)
- Test cache miss (query DB, popola cache)
- Test invalidazione cache dopo update
- Test update con audit log (verifica chiamata IAuditService con before/after)
- Test update chiave inesistente ritorna errore
- Test validazione tipo (value rispetta il type dichiarato)

**File da creare:** `backend/tests/Seed.UnitTests/Admin/Settings/UpdateSystemSettingsCommandValidatorTests.cs`
- Test Items vuoto → errore
- Test Key vuoto → errore
- Test Value null → errore

**File da creare:** `backend/tests/Seed.IntegrationTests/Admin/AdminSettingsEndpointsTests.cs`
- GET settings ritorna tutti i default dopo seeding
- PUT settings aggiorna valori correttamente
- PUT settings senza permesso → 403
- GET settings senza permesso → 403

## Criteri di completamento

- [ ] Entità `SystemSetting` creata con tutte le proprietà
- [ ] Migration creata e applicabile
- [ ] Seeder idempotente che popola i default iniziali
- [ ] `ISystemSettingsService` con cache in-memory e invalidazione
- [ ] Endpoint GET e PUT funzionanti e protetti dai permessi corretti
- [ ] Modifiche loggate nell'audit log con dettaglio prima/dopo
- [ ] Build senza errori
- [ ] Unit test passano (servizio, validatore)
- [ ] Integration test compilano (richiedono Docker per esecuzione)

## Risultato

### File creati
- `backend/src/Seed.Domain/Entities/SystemSetting.cs` — entità con Key (PK), Value, Type, Category, Description, ModifiedBy, ModifiedAt
- `backend/src/Seed.Domain/Authorization/SystemSettingsDefaults.cs` — record `SystemSettingDefault` + classe statica con 8 default iniziali e metodo `GetAll()`
- `backend/src/Seed.Infrastructure/Persistence/Configurations/SystemSettingConfiguration.cs` — PK su Key (128), Value required (1024), Type (20), Category (64), indice su Category
- `backend/src/Seed.Infrastructure/Persistence/Seeders/SystemSettingsSeeder.cs` — pattern idempotente identico a RolesAndPermissionsSeeder
- `backend/src/Seed.Infrastructure/Migrations/20260323103305_AddSystemSettings.cs` — migration per tabella SystemSettings
- `backend/src/Seed.Infrastructure/Services/SystemSettingsService.cs` — implementazione con cache IDistributedCache (TTL 5 min), invalidazione su update, audit log before/after, validazione tipo (bool/int)
- `backend/src/Seed.Application/Common/Interfaces/ISystemSettingsService.cs` — interfaccia con GetAllAsync, UpdateAsync, GetValueAsync
- `backend/src/Seed.Application/Admin/Settings/Models/SystemSettingDto.cs` — DTO con tutte le proprietà
- `backend/src/Seed.Application/Admin/Settings/Models/UpdateSettingItem.cs` — record con Key e Value
- `backend/src/Seed.Application/Admin/Settings/Queries/GetSystemSettings/GetSystemSettingsQuery.cs` — query MediatR
- `backend/src/Seed.Application/Admin/Settings/Queries/GetSystemSettings/GetSystemSettingsQueryHandler.cs` — handler che delega a ISystemSettingsService
- `backend/src/Seed.Application/Admin/Settings/Commands/UpdateSystemSettings/UpdateSystemSettingsCommand.cs` — command con Items, CurrentUserId, IpAddress, UserAgent (JsonIgnore)
- `backend/src/Seed.Application/Admin/Settings/Commands/UpdateSystemSettings/UpdateSystemSettingsCommandValidator.cs` — FluentValidation: Items non vuoto, Key non vuoto, Value non null
- `backend/src/Seed.Application/Admin/Settings/Commands/UpdateSystemSettings/UpdateSystemSettingsCommandHandler.cs` — handler che delega a ISystemSettingsService
- `backend/src/Seed.Api/Controllers/AdminSettingsController.cs` — GET e PUT protetti da Settings.Read e Settings.Manage, pattern enrichment audit context
- `backend/tests/Seed.UnitTests/Admin/Settings/SystemSettingsServiceTests.cs` — 11 test: cache hit/miss, popola cache, invalidazione, audit before/after, chiave inesistente, validazione bool/int, skip unchanged, GetValueAsync
- `backend/tests/Seed.UnitTests/Admin/Settings/UpdateSystemSettingsCommandValidatorTests.cs` — 4 test: Items vuoto, Key vuoto, Value null, valid items
- `backend/tests/Seed.IntegrationTests/Admin/AdminSettingsEndpointsTests.cs` — 6 test: auth 401, no permission 403 (GET/PUT), Admin GET OK con default, SuperAdmin PUT update, Admin PUT 403 (no Settings.Manage)

### File modificati
- `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs` — aggiunto `DbSet<SystemSetting> SystemSettings`
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrato `ISystemSettingsService` → `SystemSettingsService` e `SystemSettingsSeeder`
- `backend/src/Seed.Api/Program.cs` — chiamata `SystemSettingsSeeder.SeedAsync()` dopo gli altri seeder

### Scelte implementative
- **`Result<bool>` anziché `Result<Unit>`**: coerente con il pattern esistente (es. `UpdateRoleCommand`, `ForcePasswordChangeCommand`). Non serve dipendenza MediatR nell'interfaccia del servizio.
- **Validazione tipo nel service**: la validazione che il valore rispetti il tipo dichiarato (bool → "true"/"false", int → parsable) è nel `SystemSettingsService` perché richiede accesso ai metadati dell'entità (campo `Type`), non disponibili al livello del validator FluentValidation.
- **Cache invalidata anche su unchanged**: la cache viene invalidata dopo ogni UpdateAsync (anche se nessun valore è cambiato) per semplicità e sicurezza — il costo è trascurabile.
- **MemoryDistributedCache nei test**: i test del servizio usano `MemoryDistributedCache` reale (non mock) per testare il comportamento effettivo della cache, stesso approccio di `TokenBlacklistService`.

### Deviazioni dal piano
- Nessuna deviazione significativa. Tutti gli step del piano sono stati implementati come descritto.
