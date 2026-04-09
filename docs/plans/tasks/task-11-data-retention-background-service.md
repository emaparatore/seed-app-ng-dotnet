# Task 11: Background service per Data Retention

> **Nota:** T-06 e T-07 risultano "Not Started" nel piano ma sono **gia completamente implementati** nel codice (probabilmente implementati come dipendenza di T-10). Tutti i test passano. Il piano PLAN-4.md va aggiornato per marcarli Done prima di procedere.

## Contesto

- `IDataCleanupService` (T-10) e' gia implementato in `Infrastructure/Services/DataCleanupService.cs` con 3 metodi:
  - `PurgeSoftDeletedUsersAsync()` — purge utenti soft-deleted oltre retention period
  - `CleanupExpiredRefreshTokensAsync()` — elimina refresh token scaduti
  - `CleanupOldAuditLogEntriesAsync()` — elimina audit log vecchi
- `DataRetentionSettings` in `Shared/Configuration/` ha `CleanupIntervalHours` (default 24)
- La configurazione `DataRetention` e' gia in `appsettings.json`
- Non esiste ancora nessun `BackgroundService` nel progetto
- `IDataCleanupService` e' registrato come `Scoped` — il background service dovra creare un scope per ogni ciclo
- `Program.cs` non ha ancora nessun `AddHostedService` — la registrazione andra aggiunta in `DependencyInjection.cs` (coerente con gli altri servizi) o in `Program.cs`

## Piano di esecuzione

### File da creare

1. **`backend/src/Seed.Infrastructure/Services/DataRetentionBackgroundService.cs`**
   - Classe `DataRetentionBackgroundService : BackgroundService`
   - Iniettare `IServiceScopeFactory` e `IOptions<DataRetentionSettings>` e `ILogger<DataRetentionBackgroundService>`
   - Override `ExecuteAsync`:
     - Loop con `PeriodicTimer` (intervallo da `CleanupIntervalHours`)
     - Ad ogni tick: creare scope, risolvere `IDataCleanupService`, chiamare i 3 metodi
     - Ogni cleanup in try/catch separato (un fallimento non blocca gli altri)
     - Loggare risultati: "Purged {count} soft-deleted users, cleaned {count} expired tokens, removed {count} audit log entries"
     - Gestire `OperationCanceledException` per shutdown graceful

### File da modificare

2. **`backend/src/Seed.Infrastructure/DependencyInjection.cs`** (riga ~44)
   - Aggiungere: `services.AddHostedService<DataRetentionBackgroundService>();`

### Test da scrivere

3. **`backend/tests/Seed.UnitTests/Services/DataRetentionBackgroundServiceTests.cs`**
   - Test: verifica che il service chiami i 3 metodi di cleanup (mock `IDataCleanupService`, uso di `CancellationTokenSource` con timeout per terminare il loop)
   - Test: verifica che un errore in un cleanup non impedisca l'esecuzione degli altri
   - Pattern: usare NSubstitute per mockare `IServiceScopeFactory` → `IServiceScope` → `IServiceProvider` → `IDataCleanupService`

## Criteri di completamento

- `DataRetentionBackgroundService` creato e compila senza errori
- Registrato in DI come hosted service
- I 3 metodi di cleanup vengono chiamati ad ogni ciclo
- Intervallo configurabile da `DataRetentionSettings.CleanupIntervalHours`
- Un fallimento in un cleanup non blocca gli altri (isolamento try/catch)
- Log dei risultati via `ILogger`
- Unit test passa: verifica chiamata ai 3 metodi
- Unit test passa: verifica isolamento errori
- `dotnet build Seed.slnx` OK
- `dotnet test Seed.slnx` — tutti i test passano

## Risultato

- File creati:
  - `backend/src/Seed.Infrastructure/Services/DataRetentionBackgroundService.cs` — BackgroundService con PeriodicTimer, scope per ciclo, try/catch isolato per ogni cleanup
  - `backend/tests/Seed.UnitTests/Services/DataRetentionBackgroundServiceTests.cs` — 4 test: chiamata ai 3 metodi + 3 test di isolamento errori
- File modificati:
  - `backend/src/Seed.Infrastructure/DependencyInjection.cs` — aggiunto `AddHostedService<DataRetentionBackgroundService>()`
  - `backend/src/Seed.Infrastructure/Seed.Infrastructure.csproj` — aggiunto `InternalsVisibleTo` per Seed.UnitTests
- Scelte implementative:
  - `RunCleanupCycleAsync` esposto come `internal` per permettere il test diretto senza attendere il timer; `InternalsVisibleTo` aggiunto al csproj di Infrastructure
  - `PeriodicTimer` usato al posto di `Task.Delay` per semplicità e correttezza (gestisce drift)
  - `catch (Exception ex) when (ex is not OperationCanceledException)` per non catturare cancellation durante shutdown graceful
- Nessuna deviazione dal piano
