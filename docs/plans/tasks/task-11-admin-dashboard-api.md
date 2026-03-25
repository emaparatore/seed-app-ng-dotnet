# Task 11: API Dashboard (AdminDashboardController)

## Contesto

- **Stato attuale:** Le dipendenze T-07 (AdminUsersController) e T-09 (AdminAuditLogController) sono completate. L'infrastruttura di autorizzazione (T-03) e audit log (T-06) sono operative.
- **Pattern esistente:** Tutti gli admin controller seguono lo stesso pattern: `[ApiVersion("1.0")]`, route `api/v{version}/admin/<resource>`, `[Authorize]` a livello di classe, `[HasPermission(...)]` per endpoint, MediatR `ISender` per dispatch.
- **Permesso già definito:** `Permissions.Dashboard.ViewStats` esiste in `Seed.Domain/Authorization/Permissions.cs`.
- **Dati disponibili:** `ApplicationUser` ha `CreatedAt`, `IsActive`, `IsDeleted`; `AuditLogEntry` ha `Timestamp`, `Action`, `UserId`; i ruoli sono gestiti via ASP.NET Identity (`UserManager`).
- **Accesso dati:** Il handler usa `UserManager<ApplicationUser>` per utenti/ruoli e `IAuditLogReader.GetQueryable()` per audit log. Application non referenzia EF Core direttamente.

## Piano di esecuzione

### File da creare

1. **`backend/src/Seed.Application/Admin/Dashboard/Models/DashboardStatsDto.cs`**
   - Record con le statistiche aggregate:
     - `TotalUsers` (int), `ActiveUsers` (int), `InactiveUsers` (int)
     - `RegistrationsLast7Days` (int), `RegistrationsLast30Days` (int)
     - `RegistrationTrend` (IReadOnlyList<DailyRegistrationDto>) — ultimi 30 giorni, raggruppati per giorno
     - `UsersByRole` (IReadOnlyList<RoleDistributionDto>) — nome ruolo + conteggio
     - `RecentActivity` (IReadOnlyList<RecentActivityDto>) — ultime 5 entry audit log (compatte)

2. **`backend/src/Seed.Application/Admin/Dashboard/Models/DailyRegistrationDto.cs`**
   - Record: `Date` (DateOnly), `Count` (int)

3. **`backend/src/Seed.Application/Admin/Dashboard/Models/RoleDistributionDto.cs`**
   - Record: `RoleName` (string), `UserCount` (int)

4. **`backend/src/Seed.Application/Admin/Dashboard/Models/RecentActivityDto.cs`**
   - Record: `Id` (Guid), `Timestamp` (DateTime), `Action` (string), `EntityType` (string), `UserId` (Guid?)

5. **`backend/src/Seed.Application/Admin/Dashboard/Queries/GetDashboardStats/GetDashboardStatsQuery.cs`**
   - `public sealed record GetDashboardStatsQuery : IRequest<Result<DashboardStatsDto>>;`

6. **`backend/src/Seed.Application/Admin/Dashboard/Queries/GetDashboardStats/GetDashboardStatsQueryHandler.cs`**
   - Inietta `UserManager<ApplicationUser>`, `RoleManager<ApplicationRole>`, `IAuditLogReader`
   - Conteggi utenti: query su `userManager.Users` con filtri `IsActive`, `IsDeleted`, `CreatedAt`
   - Trend registrazioni: group by `CreatedAt.Date` ultimi 30 giorni, filling giorni mancanti con 0
   - Distribuzione ruoli: iterare sui ruoli da `RoleManager`, per ogni ruolo contare utenti con `GetUsersInRoleAsync`
   - Ultime 5 attività: `auditLogReader.GetQueryable().OrderByDescending(a => a.Timestamp).Take(5)`
   - Tutte le query aggregate, nessun caricamento massivo di record

7. **`backend/src/Seed.Api/Controllers/AdminDashboardController.cs`**
   - Pattern identico a `AdminSettingsController` (il più semplice, solo GET)
   - Route: `api/v{version:apiVersion}/admin/dashboard`
   - Singolo endpoint `[HttpGet]` con `[HasPermission(Permissions.Dashboard.ViewStats)]`
   - Restituisce `Ok(result.Data)` o `BadRequest`

8. **`backend/tests/Seed.IntegrationTests/Admin/AdminDashboardEndpointsTests.cs`**
   - Test: 401 senza auth, 403 senza permesso, 200 con permesso `Dashboard.ViewStats`
   - Verifica struttura risposta (campi presenti e tipi corretti)
   - Verifica coerenza dati (es. totalUsers = activeUsers + inactiveUsers)

### File da modificare

Nessuno — il permesso `Dashboard.ViewStats` è già definito e seminato. Il controller viene scoperto automaticamente da ASP.NET via assembly scanning. MediatR handlers vengono registrati via `AddApplication()` in DI.

### Approccio tecnico step-by-step

1. Creare i DTO models (DashboardStatsDto e sub-DTO)
2. Creare la query record `GetDashboardStatsQuery`
3. Implementare `GetDashboardStatsQueryHandler` con query aggregate
4. Creare `AdminDashboardController` con singolo endpoint GET
5. Scrivere integration test
6. Build e run test per verifica

### Test da scrivere/verificare

- **Integration test** (in `AdminDashboardEndpointsTests`):
  - `GetDashboard_WithoutAuth_ReturnsUnauthorized`
  - `GetDashboard_WithoutPermission_ReturnsForbidden`
  - `GetDashboard_WithPermission_ReturnsOk`
  - `GetDashboard_ReturnsCorrectStructure` — verifica che tutti i campi siano presenti
  - `GetDashboard_ReturnsConsistentCounts` — totalUsers == activeUsers + inactiveUsers
  - `GetDashboard_ReturnsRegistrationTrend` — array con 30 elementi (uno per giorno)

## Criteri di completamento

- [ ] Endpoint `GET /api/v1/admin/dashboard` risponde 200 con `DashboardStatsDto`
- [ ] Risposta include: totale utenti, utenti attivi, utenti disattivati
- [ ] Registrazioni ultimi 7 e 30 giorni calcolate correttamente
- [ ] Trend registrazioni: array di 30 giorni con conteggio per giorno (giorni senza registrazioni = 0)
- [ ] Distribuzione utenti per ruolo (nome + conteggio)
- [ ] Ultime 5 attività audit log (compatte: id, timestamp, action, entityType, userId)
- [ ] Endpoint protetto da `Dashboard.ViewStats`
- [ ] Query ottimizzate (conteggi aggregati, no caricamento massivo)
- [ ] Integration test passano (auth, permessi, struttura risposta, coerenza dati)
- [ ] Build OK, tutti i test esistenti continuano a passare

## Risultato

### File creati
- `backend/src/Seed.Application/Admin/Dashboard/Models/DashboardStatsDto.cs` — Record DTO principale con statistiche aggregate
- `backend/src/Seed.Application/Admin/Dashboard/Models/DailyRegistrationDto.cs` — Record per trend giornaliero (DateOnly, Count)
- `backend/src/Seed.Application/Admin/Dashboard/Models/RoleDistributionDto.cs` — Record per distribuzione ruoli (RoleName, UserCount)
- `backend/src/Seed.Application/Admin/Dashboard/Models/RecentActivityDto.cs` — Record per attività recenti (Id, Timestamp, Action, EntityType, UserId)
- `backend/src/Seed.Application/Admin/Dashboard/Queries/GetDashboardStats/GetDashboardStatsQuery.cs` — Query record MediatR
- `backend/src/Seed.Application/Admin/Dashboard/Queries/GetDashboardStats/GetDashboardStatsQueryHandler.cs` — Handler con query aggregate su UserManager, RoleManager, IAuditLogReader
- `backend/src/Seed.Api/Controllers/AdminDashboardController.cs` — Controller con singolo endpoint GET protetto da `Dashboard.ViewStats`
- `backend/tests/Seed.IntegrationTests/Admin/AdminDashboardEndpointsTests.cs` — 6 integration test (auth, permessi, struttura, coerenza, trend)

### File modificati
Nessuno.

### Scelte implementative e motivazioni
- **Pattern identico a AdminSettingsController**: controller minimale con solo GET, `[Authorize]` a livello di classe, `[HasPermission]` sull'endpoint, `ISender` per dispatch MediatR
- **Conteggio ruoli con filtro `!IsDeleted`**: nella distribuzione utenti per ruolo, vengono contati solo gli utenti non eliminati (soft delete), coerente con il conteggio `TotalUsers`
- **Trend 30 giorni con fill dei giorni mancanti**: il loop parte da 29 giorni fa fino a oggi, inserendo 0 per i giorni senza registrazioni, garantendo sempre 30 elementi nell'array
- **Query aggregate senza caricamento massivo**: i conteggi utenti sono calcolati su una lista filtrata in memoria (solo utenti non eliminati), i ruoli iterati con `GetUsersInRoleAsync` (accettabile per numero limitato di ruoli, stesso pattern di `GetRolesQueryHandler`)
- **Test DTO `DailyRegistrationResponseDto` con `Date` come string**: `DateOnly` viene serializzato come stringa ISO dal JSON serializer, quindi il DTO di test usa `string` per il campo Date

### Deviazioni dal piano
Nessuna deviazione. Tutti i file previsti sono stati creati con la struttura e il contenuto specificati nel mini-plan. Build OK (0 errori), 176 unit test passano (nessuna regressione). Integration test compilano correttamente (richiedono Docker/Testcontainers per l'esecuzione).
