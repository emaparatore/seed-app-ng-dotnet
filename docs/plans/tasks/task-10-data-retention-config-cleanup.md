# Task 10: Configurazione Data Retention e servizio di cleanup — Backend

## Contesto

### Stato attuale del codice rilevante

- **`ApplicationUser`** (`Domain/Entities/ApplicationUser.cs`): ha `IsDeleted` (bool) e `DeletedAt` (DateTime?) per soft-delete. Query filter globale `!u.IsDeleted` in `ApplicationUserConfiguration`.
- **`RefreshToken`** (`Domain/Entities/RefreshToken.cs`): ha `ExpiresAt`, `RevokedAt`, `CreatedAt`. Proprietà calcolate `IsExpired`, `IsRevoked`, `IsActive`.
- **`AuditLogEntry`** (`Domain/Entities/AuditLogEntry.cs`): ha `Timestamp` (DateTime, default `UtcNow`).
- **`IUserPurgeService`** (`Application/Common/Interfaces/IUserPurgeService.cs`): interfaccia con `PurgeUserAsync(Guid userId, CancellationToken)` — già implementata in T-06 da `UserPurgeService` in Infrastructure.
- **`UserPurgeService`** (`Infrastructure/Services/UserPurgeService.cs`): anonimizza audit log, elimina refresh token, elimina utente via `UserManager.DeleteAsync`. Usa `IgnoreQueryFilters()` per trovare utenti soft-deleted.
- **Pattern configurazione**: le settings usano classi in `Seed.Shared/Configuration/` con `SectionName` costante, registrate via `services.Configure<T>()` in `DependencyInjection.cs`.
- **`appsettings.json`** (`Seed.Api/appsettings.json`): sezioni esistenti: `JwtSettings`, `Privacy`, `Smtp`, `Client`, etc.
- **DI registration**: tutto in `Infrastructure/DependencyInjection.cs` (`AddInfrastructure` extension method).

### Dipendenze e vincoli

- Dipende da T-06 (completato): riutilizza `IUserPurgeService` per il purge degli utenti soft-deleted.
- Il metodo di purge utenti soft-deleted deve chiamare `IUserPurgeService.PurgeUserAsync` per ogni utente da eliminare.
- I metodi di cleanup token e audit log devono usare `ApplicationDbContext` direttamente con `ExecuteDeleteAsync` per bulk delete efficienti.
- Il servizio deve essere registrato come `Scoped` (verrà usato dentro uno scope dal background service in T-11).

## Piano di esecuzione

### Step 1: Creare `DataRetentionSettings` in Shared

**File da creare:** `backend/src/Seed.Shared/Configuration/DataRetentionSettings.cs`

```csharp
namespace Seed.Shared.Configuration;

public sealed class DataRetentionSettings
{
    public const string SectionName = "DataRetention";

    public int SoftDeletedUserRetentionDays { get; init; } = 30;
    public int RefreshTokenRetentionDays { get; init; } = 7;
    public int AuditLogRetentionDays { get; init; } = 365;
    public int CleanupIntervalHours { get; init; } = 24;
}
```

### Step 2: Aggiungere sezione `DataRetention` in appsettings.json

**File da modificare:** `backend/src/Seed.Api/appsettings.json`

Aggiungere dopo la sezione `Privacy`:
```json
"DataRetention": {
  "SoftDeletedUserRetentionDays": 30,
  "RefreshTokenRetentionDays": 7,
  "AuditLogRetentionDays": 365,
  "CleanupIntervalHours": 24
}
```

### Step 3: Creare interfaccia `IDataCleanupService`

**File da creare:** `backend/src/Seed.Application/Common/Interfaces/IDataCleanupService.cs`

Metodi:
- `Task<int> PurgeSoftDeletedUsersAsync(CancellationToken)` — trova utenti con `IsDeleted && DeletedAt` oltre il periodo di retention, chiama `IUserPurgeService.PurgeUserAsync` per ciascuno. Ritorna il count.
- `Task<int> CleanupExpiredRefreshTokensAsync(CancellationToken)` — elimina token scaduti/revocati oltre il periodo di retention con `ExecuteDeleteAsync`. Ritorna il count.
- `Task<int> CleanupOldAuditLogEntriesAsync(CancellationToken)` — elimina audit log entry con `Timestamp` oltre il periodo di retention con `ExecuteDeleteAsync`. Ritorna il count.

### Step 4: Implementare `DataCleanupService`

**File da creare:** `backend/src/Seed.Infrastructure/Services/DataCleanupService.cs`

- Iniettare: `ApplicationDbContext`, `IUserPurgeService`, `IOptions<DataRetentionSettings>`, `ILogger<DataCleanupService>`.
- **PurgeSoftDeletedUsersAsync**: query `dbContext.Users.IgnoreQueryFilters().Where(u => u.IsDeleted && u.DeletedAt != null && u.DeletedAt < cutoffDate)`, poi loop con `PurgeUserAsync` per ciascuno.
- **CleanupExpiredRefreshTokensAsync**: `dbContext.RefreshTokens.Where(r => (r.ExpiresAt < cutoffDate) || (r.RevokedAt != null && r.RevokedAt < cutoffDate)).ExecuteDeleteAsync()`. Il cutoff è `UtcNow - RefreshTokenRetentionDays`.
- **CleanupOldAuditLogEntriesAsync**: `dbContext.AuditLogEntries.Where(a => a.Timestamp < cutoffDate).ExecuteDeleteAsync()`. Il cutoff è `UtcNow - AuditLogRetentionDays`.

### Step 5: Registrare in DI

**File da modificare:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`

Aggiungere:
```csharp
services.Configure<DataRetentionSettings>(configuration.GetSection(DataRetentionSettings.SectionName));
services.AddScoped<IDataCleanupService, DataCleanupService>();
```

### Step 6: Unit test per `DataCleanupService`

**File da creare:** `backend/tests/Seed.IntegrationTests/Services/DataCleanupServiceTests.cs`

Nota: questi sono integration test (non unit test puri) perché `DataCleanupService` lavora direttamente con `ApplicationDbContext` e `ExecuteDeleteAsync`. Seguire il pattern di `UserPurgeServiceTests`.

Test cases:
1. **PurgeSoftDeletedUsersAsync_Purges_Users_Past_Retention_Period** — crea utente soft-deleted con `DeletedAt` = 31 giorni fa, verifica che venga eliminato.
2. **PurgeSoftDeletedUsersAsync_Does_Not_Purge_Recently_Deleted_Users** — crea utente soft-deleted con `DeletedAt` = 5 giorni fa, verifica che NON venga eliminato.
3. **CleanupExpiredRefreshTokensAsync_Deletes_Expired_Tokens_Past_Retention** — crea token scaduto da 8+ giorni, verifica eliminazione.
4. **CleanupExpiredRefreshTokensAsync_Keeps_Recent_Expired_Tokens** — crea token scaduto da 2 giorni, verifica che resti.
5. **CleanupOldAuditLogEntriesAsync_Deletes_Old_Entries** — crea audit log di 366+ giorni fa, verifica eliminazione.
6. **CleanupOldAuditLogEntriesAsync_Keeps_Recent_Entries** — crea audit log di 30 giorni fa, verifica che resti.

### Step 7: Build e test

```bash
cd backend && dotnet build Seed.slnx
dotnet test Seed.slnx
```

## Criteri di completamento

- [ ] `DataRetentionSettings` in `Shared/Configuration/` con i 4 campi e valori default
- [ ] Sezione `DataRetention` in `appsettings.json` con i 4 valori
- [ ] Interfaccia `IDataCleanupService` in `Application/Common/Interfaces/` con 3 metodi (ognuno ritorna `Task<int>`)
- [ ] Implementazione `DataCleanupService` in `Infrastructure/Services/` che riutilizza `IUserPurgeService` per il purge utenti
- [ ] Registrazione DI di settings e servizio in `DependencyInjection.cs`
- [ ] Integration test con almeno 6 test cases (2 per ciascun metodo di cleanup)
- [ ] `dotnet build Seed.slnx` — 0 errori
- [ ] `dotnet test Seed.slnx` — tutti i test passano

## Risultato

- File creati:
  - `backend/src/Seed.Shared/Configuration/DataRetentionSettings.cs` — classe settings con 4 proprietà e valori default
  - `backend/src/Seed.Application/Common/Interfaces/IDataCleanupService.cs` — interfaccia con 3 metodi `Task<int>`
  - `backend/src/Seed.Infrastructure/Services/DataCleanupService.cs` — implementazione che usa `IUserPurgeService` per purge utenti e `ExecuteDeleteAsync` per bulk delete token/audit
  - `backend/tests/Seed.IntegrationTests/Services/DataCleanupServiceTests.cs` — 6 integration test (2 per metodo)
- File modificati:
  - `backend/src/Seed.Api/appsettings.json` — aggiunta sezione `DataRetention` con i 4 valori configurabili
  - `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione `DataRetentionSettings` e `DataCleanupService` (Scoped)
- Scelte implementative:
  - Usato `Select(u => u.Id).ToListAsync()` per caricare solo gli ID degli utenti da purgare, poi loop con `PurgeUserAsync` — evita di tenere in memoria le entità utente e riutilizza la logica esistente di T-06
  - Integration test (non unit test) perché il servizio lavora direttamente con `ApplicationDbContext` e `ExecuteDeleteAsync`, coerente con il pattern di `UserPurgeServiceTests`
  - FluentAssertions: usato `BeGreaterThanOrEqualTo` (non `BeGreaterOrEqualTo`) per coerenza con la versione installata
- Nessuna deviazione dal piano
