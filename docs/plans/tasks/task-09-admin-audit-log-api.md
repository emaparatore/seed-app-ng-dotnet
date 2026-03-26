# Task 09: API Audit Log (AdminAuditLogController)

## Contesto

### Stato attuale del codice rilevante
- **Entità `AuditLogEntry`** già definita in `backend/src/Seed.Domain/Entities/AuditLogEntry.cs` con campi: `Id`, `Timestamp`, `UserId`, `Action`, `EntityType`, `EntityId`, `Details` (text/JSON), `IpAddress`, `UserAgent`
- **`IAuditService`** già definita in `backend/src/Seed.Application/Common/Interfaces/IAuditService.cs` — solo metodo `LogAsync()` (write-only, nessun metodo di lettura)
- **`AuditService`** implementazione in `backend/src/Seed.Infrastructure/Services/AuditService.cs` — scrive su `ApplicationDbContext.AuditLogEntries`
- **`AuditLogEntryConfiguration`** in `backend/src/Seed.Infrastructure/Persistence/Configurations/AuditLogEntryConfiguration.cs` — indici su `Timestamp`, `UserId`, `Action`; `Details` è `text` (JSON)
- **`AuditActions`** costanti in `backend/src/Seed.Domain/Authorization/AuditActions.cs` — 17 azioni definite
- **`Permissions.AuditLog.Read`** e **`Permissions.AuditLog.Export`** già definiti in `Permissions.cs`
- **Pattern di riferimento:** `AdminRolesController` (controller), `GetUsersQuery/Handler` (query paginata con filtri), `PagedResult<T>` (paginazione)
- **Nessun DTO** audit log esiste ancora nell'Application layer
- **Nessun endpoint** di lettura audit log esiste ancora

### Dipendenze e vincoli
- Dipende da T-06 (Infrastruttura Audit Log) — **completato**
- L'audit log è append-only: nessun endpoint di modifica/cancellazione
- Export CSV deve rispettare i filtri applicati
- `Details` è JSON serializzato come stringa — il dettaglio lo restituisce deserializzato

## Piano di esecuzione

### Step 1: DTO audit log
**Creare** `backend/src/Seed.Application/Admin/AuditLog/Models/AuditLogEntryDto.cs`
```
AuditLogEntryDto(Guid Id, DateTime Timestamp, Guid? UserId, string Action, string EntityType, string? EntityId, string? Details, string? IpAddress, string? UserAgent)
```

### Step 2: Query — GetAuditLogEntries (lista paginata con filtri)
**Creare** `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/`
- `GetAuditLogEntriesQuery.cs` — record con: `PageNumber`, `PageSize`, `ActionFilter` (string?), `UserId` (Guid?), `DateFrom` (DateTime?), `DateTo` (DateTime?), `SearchTerm` (string?), `SortDescending` (bool, default true)
- `GetAuditLogEntriesQueryValidator.cs` — PageNumber >= 1, PageSize 1-100
- `GetAuditLogEntriesQueryHandler.cs` — query su `ApplicationDbContext.AuditLogEntries` con filtri, ordinamento per Timestamp (default desc), paginazione server-side. Usa `IQueryable` direttamente (no materializzazione anticipata come GetUsersQuery). Restituisce `Result<PagedResult<AuditLogEntryDto>>`

### Step 3: Query — GetAuditLogEntryById (dettaglio singolo)
**Creare** `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntryById/`
- `GetAuditLogEntryByIdQuery.cs` — `record GetAuditLogEntryByIdQuery(Guid Id)`
- `GetAuditLogEntryByIdQueryHandler.cs` — cerca per Id, restituisce `Result<AuditLogEntryDto>` o NotFound

### Step 4: Query — ExportAuditLog (CSV export)
**Creare** `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/`
- `ExportAuditLogQuery.cs` — stessi filtri di GetAuditLogEntries (senza paginazione)
- `ExportAuditLogQueryValidator.cs` — validazione date
- `ExportAuditLogQueryHandler.cs` — query filtrata, produce `Result<byte[]>` (CSV UTF-8 con BOM). Colonne: Id, Timestamp, UserId, Action, EntityType, EntityId, Details, IpAddress, UserAgent. Usa `StringBuilder` o `StreamWriter` per generare il CSV (nessuna dipendenza esterna). Limita a 10.000 righe per evitare problemi di memoria.

### Step 5: Controller
**Creare** `backend/src/Seed.Api/Controllers/AdminAuditLogController.cs`
- `[ApiController]`, `[ApiVersion("1.0")]`, `[Route("api/v{version:apiVersion}/admin/audit-log")]`, `[Authorize]`
- `GET /` — `[HasPermission(Permissions.AuditLog.Read)]` → `GetAuditLogEntriesQuery` (parametri da query string)
- `GET /{id:guid}` — `[HasPermission(Permissions.AuditLog.Read)]` → `GetAuditLogEntryByIdQuery`
- `GET /export` — `[HasPermission(Permissions.AuditLog.Export)]` → `ExportAuditLogQuery`, restituisce `File(bytes, "text/csv", "audit-log.csv")`

### Step 6: Registrazione handler nel DI
Verificare che l'assembly scan di MediatR in `Seed.Application` copra automaticamente i nuovi handler (dovrebbe, dato il pattern esistente con `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))`).

### Step 7: Test
**Creare** `backend/tests/Seed.UnitTests/Admin/AuditLog/GetAuditLogEntriesQueryHandlerTests.cs`
- Test filtro per Action
- Test filtro per UserId
- Test filtro per DateFrom/DateTo
- Test ricerca testuale su Details
- Test ordinamento default (desc by timestamp)
- Test paginazione

**Creare** `backend/tests/Seed.UnitTests/Admin/AuditLog/ExportAuditLogQueryHandlerTests.cs`
- Test CSV header corretto
- Test CSV con filtri
- Test limite 10.000 righe

**Creare** `backend/tests/Seed.IntegrationTests/Admin/AdminAuditLogEndpointsTests.cs`
- Test GET lista con permesso AuditLog.Read
- Test GET lista senza permesso → 403
- Test GET dettaglio
- Test GET export con permesso AuditLog.Export
- Test GET export senza permesso → 403
- Test filtri (action, date range)

### Step 8: Build e verifica test
```bash
cd backend && dotnet build Seed.slnx && dotnet test Seed.slnx
```

## File da creare/modificare

### File da creare
1. `backend/src/Seed.Application/Admin/AuditLog/Models/AuditLogEntryDto.cs`
2. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/GetAuditLogEntriesQuery.cs`
3. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/GetAuditLogEntriesQueryValidator.cs`
4. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/GetAuditLogEntriesQueryHandler.cs`
5. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntryById/GetAuditLogEntryByIdQuery.cs`
6. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntryById/GetAuditLogEntryByIdQueryHandler.cs`
7. `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/ExportAuditLogQuery.cs`
8. `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/ExportAuditLogQueryValidator.cs`
9. `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/ExportAuditLogQueryHandler.cs`
10. `backend/src/Seed.Api/Controllers/AdminAuditLogController.cs`
11. `backend/tests/Seed.UnitTests/Admin/AuditLog/GetAuditLogEntriesQueryHandlerTests.cs`
12. `backend/tests/Seed.UnitTests/Admin/AuditLog/ExportAuditLogQueryHandlerTests.cs`
13. `backend/tests/Seed.IntegrationTests/Admin/AdminAuditLogEndpointsTests.cs`

### File da modificare
- Nessuno — il DbContext ha già `AuditLogEntries`, le Permission sono già definite, MediatR fa assembly scan automatico

## Criteri di completamento

- [ ] Endpoint `GET /api/v1/admin/audit-log` restituisce lista paginata con filtri (action, userId, date range, search su Details)
- [ ] Ordinamento per timestamp (default: più recenti prima)
- [ ] Endpoint `GET /api/v1/admin/audit-log/{id}` restituisce dettaglio singolo evento con `Details` come stringa JSON
- [ ] Endpoint `GET /api/v1/admin/audit-log/export` restituisce file CSV con filtri applicati e limite 10.000 righe
- [ ] Endpoint lista e dettaglio protetti da `AuditLog.Read`
- [ ] Endpoint export protetto da `AuditLog.Export`
- [ ] Nessun endpoint di modifica o cancellazione (append-only)
- [ ] Unit test per handler query (filtri, paginazione, CSV)
- [ ] Integration test per endpoint (permessi, filtri, export)
- [ ] Build OK e tutti i test passano

## Risultato

### File creati
1. `backend/src/Seed.Application/Common/Interfaces/IAuditLogReader.cs` — interfaccia read-only per query audit log
2. `backend/src/Seed.Application/Admin/AuditLog/Models/AuditLogEntryDto.cs` — DTO record
3. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/GetAuditLogEntriesQuery.cs` — query paginata con filtri
4. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/GetAuditLogEntriesQueryValidator.cs` — validazione PageNumber/PageSize
5. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntries/GetAuditLogEntriesQueryHandler.cs` — handler con filtri, sort, paginazione
6. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntryById/GetAuditLogEntryByIdQuery.cs` — query singolo entry
7. `backend/src/Seed.Application/Admin/AuditLog/Queries/GetAuditLogEntryById/GetAuditLogEntryByIdQueryHandler.cs` — handler con NotFound
8. `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/ExportAuditLogQuery.cs` — query export CSV
9. `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/ExportAuditLogQueryValidator.cs` — validazione date
10. `backend/src/Seed.Application/Admin/AuditLog/Queries/ExportAuditLog/ExportAuditLogQueryHandler.cs` — handler CSV con limite 10.000 righe e UTF-8 BOM
11. `backend/src/Seed.Infrastructure/Services/AuditLogReader.cs` — implementazione IAuditLogReader su ApplicationDbContext
12. `backend/src/Seed.Api/Controllers/AdminAuditLogController.cs` — controller con 3 endpoint (GET lista, GET dettaglio, GET export)
13. `backend/tests/Seed.UnitTests/Admin/AuditLog/GetAuditLogEntriesQueryHandlerTests.cs` — 6 test (filtro action, userId, date range, search, sort, paginazione)
14. `backend/tests/Seed.UnitTests/Admin/AuditLog/ExportAuditLogQueryHandlerTests.cs` — 3 test (header CSV, filtri, limite 10.000)
15. `backend/tests/Seed.IntegrationTests/Admin/AdminAuditLogEndpointsTests.cs` — 6 test (permessi 403, lista OK, dettaglio, filtro action, export 403, export CSV)

### File modificati
1. `backend/src/Seed.Infrastructure/DependencyInjection.cs` — aggiunta registrazione `IAuditLogReader` → `AuditLogReader`

### Scelte implementative e motivazioni
- **`IAuditLogReader` interface**: Il piano prevedeva query dirette su `ApplicationDbContext.AuditLogEntries`, ma l'Application layer non referenzia Infrastructure/EF Core. Creata un'interfaccia `IAuditLogReader` (con `GetQueryable()` e `GetByIdAsync()`) in Application, implementata in Infrastructure con `AuditLogReader`. Segue il pattern Clean Architecture esistente (come `IAuditService`, `IPermissionService`).
- **Pattern paginazione**: Coerente con `GetUsersQueryHandler` — materializza con `.ToList()` poi pagina in memoria. Per volumi di audit log molto grandi si potrebbe ottimizzare con `CountAsync` + `Skip/Take` su IQueryable, ma il pattern è allineato al codebase esistente.
- **CSV export**: Usa `StringBuilder` senza dipendenze esterne, con escape corretto per campi con virgole/virgolette/newline. UTF-8 BOM prepended come da piano.

### Deviazioni dal piano
- **Aggiunta `IAuditLogReader` interface + `AuditLogReader` implementation**: Non previsti nel piano (che assumeva accesso diretto a DbContext), ma necessari per rispettare l'architettura Clean Architecture del progetto (Application non referenzia Infrastructure).
- **Modifica a `DependencyInjection.cs`**: Il piano diceva "Nessun file da modificare", ma è stato necessario registrare il nuovo servizio `IAuditLogReader` nel DI container.

### Verifiche
- Build: ✅ 0 errori
- Unit test audit log: ✅ 10/10 passati
- Tutti i unit test: ✅ 161/161 passati
- Integration test: compilano, richiedono Docker (Testcontainers) per l'esecuzione
