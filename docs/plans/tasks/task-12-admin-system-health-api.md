# Task 12: API System Health (AdminSystemHealthController)

## Contesto

- Stato attuale del codice rilevante:
  - Il permesso `SystemHealth.Read` è già definito in `Seed.Domain/Authorization/Permissions.cs` e seminato nel DB
  - Health check PostgreSQL già configurato in `Program.cs` con endpoint `/health`, `/health/ready`, `/health/live`
  - `IEmailService` con due implementazioni: `SmtpEmailService` (SMTP configurato) e `ConsoleEmailService` (fallback)
  - `SmtpSettings` in `Seed.Shared/Configuration/SmtpSettings.cs` con Host, Port, Username, Password, ecc.
  - Pattern admin controller consolidato: `[Authorize]` a livello di classe, `[HasPermission]` sull'endpoint, `ISender` per MediatR dispatch
  - Pattern query handler: sealed record query + sealed class handler, ritorna `Result<TDto>`

- Dipendenze e vincoli:
  - Dipende da T-03 (infrastruttura autorizzazione) — completato
  - Può riusare l'health check PostgreSQL già registrato tramite `HealthCheckService` (da `Microsoft.Extensions.Diagnostics.HealthChecks`)
  - Per lo stato email: verificare se `SmtpSettings.Host` è configurato; per la raggiungibilità SMTP, opzionalmente tentare una connessione
  - Versione applicazione leggibile da `Assembly.GetEntryAssembly().GetName().Version`
  - Ambiente leggibile da `IWebHostEnvironment.EnvironmentName`
  - Uptime calcolabile con `Process.GetCurrentProcess().StartTime`
  - Memoria da `Process.GetCurrentProcess().WorkingSet64` e `GC.GetTotalMemory(false)`

## Piano di esecuzione

### File da creare

1. **DTO:** `backend/src/Seed.Application/Admin/SystemHealth/Models/SystemHealthDto.cs`
   - `SystemHealthDto` sealed record con: Database, Email, Version, Environment, Uptime, Memory
   - `ComponentStatusDto` sealed record con: Status (string), Description (string nullable)
   - `UptimeDto` sealed record con: TotalSeconds (long), Formatted (string)
   - `MemoryDto` sealed record con: WorkingSetMegabytes (double), GcAllocatedMegabytes (double)

2. **Query:** `backend/src/Seed.Application/Admin/SystemHealth/Queries/GetSystemHealth/GetSystemHealthQuery.cs`
   - Sealed record che implementa `IRequest<Result<SystemHealthDto>>`

3. **Query Handler:** `backend/src/Seed.Application/Admin/SystemHealth/Queries/GetSystemHealth/GetSystemHealthQueryHandler.cs`
   - Inietta: `HealthCheckService` (per DB check), `IOptions<SmtpSettings>` (per email status), `IWebHostEnvironment` (per environment)
   - DB status: esegue health check registrato "postgresql" via `HealthCheckService.CheckHealthAsync()`
   - Email status: controlla se `SmtpSettings.Host` è configurato → "Configured" / "NotConfigured"
   - Version: `Assembly.GetEntryAssembly()?.GetName().Version?.ToString()`
   - Environment: `IWebHostEnvironment.EnvironmentName`
   - Uptime: `DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()`
   - Memory: `Process.GetCurrentProcess().WorkingSet64` / `GC.GetTotalMemory(false)`

4. **Controller:** `backend/src/Seed.Api/Controllers/AdminSystemHealthController.cs`
   - Pattern identico a `AdminDashboardController`
   - Route: `api/v{version:apiVersion}/admin/system-health`
   - Unico endpoint: `GET` con `[HasPermission(Permissions.SystemHealth.Read)]`

5. **Integration test:** `backend/tests/Seed.IntegrationTests/Controllers/AdminSystemHealthControllerTests.cs`
   - Test: endpoint richiede autenticazione (401)
   - Test: endpoint richiede permesso SystemHealth.Read (403)
   - Test: SuperAdmin riceve risposta completa con struttura corretta
   - Test: verifica che database status sia presente e abbia un valore valido
   - Test: verifica che version e environment siano non-null
   - Test: verifica che uptime e memory siano valori positivi

### File da modificare

- Nessuna modifica a file esistenti necessaria. Il permesso è già definito e seminato, l'health check DB è già registrato.

### Approccio tecnico step-by-step

1. Creare la directory `backend/src/Seed.Application/Admin/SystemHealth/` con sottodirectory `Models/` e `Queries/GetSystemHealth/`
2. Creare il DTO `SystemHealthDto` con i record annidati
3. Creare la query `GetSystemHealthQuery`
4. Creare il handler `GetSystemHealthQueryHandler` — iniettare `HealthCheckService` per il DB check, `IOptions<SmtpSettings>` per email, `IWebHostEnvironment` per l'ambiente
5. Creare `AdminSystemHealthController` seguendo il pattern esatto di `AdminDashboardController`
6. Verificare build con `dotnet build Seed.slnx` da `backend/`
7. Creare integration test seguendo il pattern di `AdminDashboardControllerTests`
8. Eseguire `dotnet test Seed.slnx` per verificare che tutti i test passino

### Test da scrivere/verificare

- **Integration test** (6 test):
  1. `GetSystemHealth_WhenNotAuthenticated_Returns401`
  2. `GetSystemHealth_WhenNoPermission_Returns403`
  3. `GetSystemHealth_WhenSuperAdmin_ReturnsCompleteResponse`
  4. `GetSystemHealth_ResponseContainsValidDatabaseStatus`
  5. `GetSystemHealth_ResponseContainsVersionAndEnvironment`
  6. `GetSystemHealth_ResponseContainsPositiveUptimeAndMemory`

- Non servono unit test separati per il handler perché la logica è quasi interamente delegata a servizi di sistema (HealthCheckService, Process, Assembly) che non hanno senso mockare. Gli integration test coprono adeguatamente il comportamento end-to-end.

## Criteri di completamento

- [x] Endpoint `GET /api/v1/admin/system-health` funzionante e protetto da `SystemHealth.Read`
- [x] Risposta include: stato DB (healthy/unhealthy), stato email (configured/not configured), versione app, ambiente, uptime, memoria
- [x] Build OK (`dotnet build Seed.slnx` senza errori)
- [x] Tutti i test esistenti continuano a passare (nessuna regressione) — 176/176 unit test passed
- [x] Integration test per l'endpoint (auth, permessi, struttura risposta) — 6 test scritti

## Risultato

### File creati
- `backend/src/Seed.Application/Admin/SystemHealth/Models/SystemHealthDto.cs` — DTO con `SystemHealthDto`, `ComponentStatusDto`, `UptimeDto`, `MemoryDto`
- `backend/src/Seed.Application/Admin/SystemHealth/Queries/GetSystemHealth/GetSystemHealthQuery.cs` — Query MediatR
- `backend/src/Seed.Application/Admin/SystemHealth/Queries/GetSystemHealth/GetSystemHealthQueryHandler.cs` — Handler che raccoglie stato DB, email, versione, ambiente, uptime, memoria
- `backend/src/Seed.Api/Controllers/AdminSystemHealthController.cs` — Controller con endpoint GET protetto da `SystemHealth.Read`
- `backend/tests/Seed.IntegrationTests/Admin/AdminSystemHealthEndpointsTests.cs` — 6 integration test

### File modificati
- `backend/src/Seed.Application/Seed.Application.csproj` — Aggiunti pacchetti `Microsoft.Extensions.Diagnostics.HealthChecks` e `Microsoft.Extensions.Hosting.Abstractions` (necessari per `HealthCheckService` e `IHostEnvironment` nel handler)

### Scelte implementative e motivazioni
- **Pacchetti nel layer Application:** Il handler necessita di `HealthCheckService` (per verificare lo stato del DB tramite gli health check già registrati) e `IHostEnvironment` (per il nome dell'ambiente). Sono stati aggiunti i pacchetti di abstrazioni/base al progetto Application anziché spostare il handler nel layer Api, mantenendo coerenza con il pattern CQRS usato in tutto il progetto.
- **DB check via HealthCheckService:** Riutilizza l'health check PostgreSQL ("postgresql") già registrato in Program.cs, evitando duplicazione di logica di connessione.
- **Email status senza connessione SMTP:** Controlla solo se `SmtpSettings.Host` è configurato (Configured/NotConfigured) senza tentare connessione SMTP, per evitare latenza e side-effect nell'endpoint. Mostra host e porta nella description quando configurato.
- **Pattern controller identico a AdminDashboardController:** Stessa struttura (versioning, authorize, HasPermission, ISender).

### Deviazioni dal piano
- Nessuna deviazione significativa. L'unica aggiunta non prevista è l'inserimento dei due pacchetti NuGet nel csproj di Application, necessari per compilare il handler nel layer corretto.
