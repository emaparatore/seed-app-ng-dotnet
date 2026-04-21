# Task 22: Permissions seeder and Bootstrap update

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| (Trasversale) | Permissions seeder and Bootstrap update | T-22 | (Trasversale) |

### Dipendenze (da 'Depends on:')
T-10: Admin CRUD plans — Implementation Notes:
- `AdminPlanDetailDto` not created separately — `AdminPlanDto` reused for both list and detail endpoints, as it already contains full details including Stripe IDs and features
- `UpdatePlan` manages features via Key matching: features with the same Key are updated, missing ones removed, new ones added
- `ArchivePlanCommand` does not call `IPaymentGateway` — archiving is a DB-only status change, no Stripe sync needed
- Plans permissions (Read/Create/Update) are seeded automatically because `RolesAndPermissionsSeeder` reads `Permissions.GetAll()` — no manual seeder change required
- All 5 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`, consistent with prior billing tasks

T-11: Admin subscriptions dashboard API — Implementation Notes:
- MRR calculation detects yearly billing by period length > 35 days and uses `YearlyPrice/12`; churn rate guards against division by zero when no subscriptions exist
- Query handlers placed in `Seed.Infrastructure/Billing/Queries/` (not Application) because `ApplicationDbContext` is only available in Infrastructure — consistent with T-07/T-10 convention
- All 3 handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
- Integration tests seed a real user with `Subscriptions.Read` permission via `WebhookWebApplicationFactory`, which already enables the payments module
- `Subscriptions.Read` permission is added to the `All` array in `Permissions.cs`, so it is picked up automatically by `RolesAndPermissionsSeeder` without any seeder change

### Convenzioni da task Done correlati
- T-10: Plans permissions (Read/Create/Update) are seeded automatically because `RolesAndPermissionsSeeder` reads `Permissions.GetAll()` — no manual seeder change required
- T-11: `Subscriptions.Read` permission is added to the `All` array in `Permissions.cs`, so it is picked up automatically by `RolesAndPermissionsSeeder` without any seeder change
- T-12: `Subscriptions.Manage` permission was added during subscription guards implementation

### Riferimenti
- `docs/requirements/FEAT-3.md` — DA-1: Modulo attivabile via configurazione
- `docs/admin-dashboard.md` — RBAC permissions, SuperAdmin seeding

## Stato attuale del codice

**IMPORTANTE: Tutto il lavoro previsto da T-22 è già stato completato durante l'implementazione di T-10, T-11 e T-12.**

- `backend/src/Seed.Domain/Authorization/Permissions.cs` — GIÀ contiene:
  - `Plans.Read`, `Plans.Create`, `Plans.Update` (righe 47-49, aggiunti in T-10)
  - `Subscriptions.Read`, `Subscriptions.Manage` (righe 54-55, aggiunti in T-11/T-12)
  - Tutti presenti nell'array `All` (righe 66-67)
- `backend/src/Seed.Infrastructure/Persistence/Seeders/RolesAndPermissionsSeeder.cs` — legge da `Permissions.GetAll()` (riga 58), quindi i nuovi permessi vengono seedati automaticamente. Nessuna modifica necessaria.
- `backend/src/Seed.Bootstrap/Program.cs` — chiama `RolesAndPermissionsSeeder.SeedAsync()` (riga 53). Non esiste un `PermissionSeeder` separato. Nessuna modifica necessaria.
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — GIÀ contiene `Plans` (Read/Create/Update) e `Subscriptions` (Read/Manage) (righe 30-38)

## Piano di esecuzione

Nessuna modifica al codice è necessaria. Tutti gli item della Definition of Done sono già soddisfatti:

1. **Verifica permessi in `Permissions.cs`**: `Plans.Read`, `Plans.Create`, `Plans.Update`, `Subscriptions.Read` sono presenti e inclusi in `GetAll()` ✅
2. **Verifica seeder**: `RolesAndPermissionsSeeder` usa `Permissions.GetAll()` — i permessi vengono creati automaticamente ✅
3. **Verifica Bootstrap**: Non esiste un `PermissionSeeder` separato; `RolesAndPermissionsSeeder` gestisce tutto ✅
4. **Verifica frontend**: `PERMISSIONS` constant ha tutti i permessi necessari ✅
5. **Verifica build e test**:
   - Eseguire `dotnet build Seed.slnx` da `backend/`
   - Eseguire `dotnet test tests/Seed.UnitTests` da `backend/`
   - Verificare che i permessi esistenti non sono stati alterati

Il task va semplicemente marcato come Done in `docs/plans/PLAN-5.md`.

## Criteri di completamento
- [x] New permissions in `Permissions.cs` and `GetAll()` array
- [x] Seeder updated to create new permissions
- [x] Frontend permissions constant updated
- [x] Bootstrap runs without errors
- [x] Existing permissions unchanged

## Risultato

**Nessuna modifica al codice effettuata.** Tutto il lavoro previsto era già stato completato durante l'implementazione di T-10, T-11 e T-12.

**File verificati (nessuna modifica necessaria):**
- `backend/src/Seed.Domain/Authorization/Permissions.cs` — contiene già `Plans.Read`, `Plans.Create`, `Plans.Update`, `Subscriptions.Read`, `Subscriptions.Manage` nell'array `All` e in `GetAll()`
- `backend/src/Seed.Infrastructure/Persistence/Seeders/RolesAndPermissionsSeeder.cs` — usa `Permissions.GetAll()` (riga 58), quindi i nuovi permessi vengono seedati automaticamente
- `backend/src/Seed.Bootstrap/Program.cs` — chiama `RolesAndPermissionsSeeder.SeedAsync()`, nessun seeder separato per i permessi
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — già contiene `Plans` (Read/Create/Update) e `Subscriptions` (Read/Manage)

**File aggiornati:**
- `docs/plans/PLAN-5.md` — T-22 marcato come Done con tutti i criteri completati
- `docs/plans/tasks/task-22-permissions-seeder-bootstrap.md` — aggiunta sezione Risultato

**Scelte chiave:**
- Non esiste un `PermissionSeeder` separato nel Bootstrap: `RolesAndPermissionsSeeder` gestisce tutto leggendo da `Permissions.GetAll()`
- I permessi billing (`Plans.*`, `Subscriptions.*`) sono stati aggiunti all'array `All` nei task precedenti, quindi vengono assegnati automaticamente a SuperAdmin e Admin senza alcun intervento manuale

**Deviazioni dal mini-plan:** Nessuna.
