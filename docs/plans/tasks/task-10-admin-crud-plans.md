# Task 10: Admin CRUD plans

## Contesto ereditato dal piano
### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-007 | Admin — CRUD piani di abbonamento | T-10, T-17 | 🔄 In Progress (domain entities done) |

### Dipendenze (da 'Depends on:')

**T-02: Domain entities — Plan, Subscription, InvoiceRequest**

Implementation Notes:
- Created 5 enums in new `Seed.Domain/Enums/` namespace: PlanStatus, SubscriptionStatus, CustomerType, InvoiceRequestStatus, BillingInterval
- Created 4 entities: SubscriptionPlan, PlanFeature, UserSubscription, InvoiceRequest — all following existing POCO conventions
- Added `Subscriptions` and `InvoiceRequests` navigation properties to ApplicationUser
- Navigation properties to parent entities initialized with `= null!` (EF Core recommended pattern)
- Enum defaults set to sensible values (PlanStatus.Active, SubscriptionStatus.Active, InvoiceRequestStatus.Requested)

**T-03: EF Core configuration and migration**

Implementation Notes:
- Followed existing `RefreshTokenConfiguration` pattern for structure and style (sealed class, file-scoped namespace)
- Enum properties converted to string in DB via `HasConversion<string>()` for readability
- Unique index on `StripeSubscriptionId` uses `HasFilter("\"StripeSubscriptionId\" IS NOT NULL")` to handle nullable correctly in PostgreSQL
- `DeleteBehavior.Restrict` on `SubscriptionPlan → UserSubscription` to prevent deletion of plans with active subscriptions
- `DeleteBehavior.Cascade` on `User → UserSubscription` and `User → InvoiceRequest`, consistent with existing RefreshToken pattern

**T-05: StripePaymentGateway implementation**

Implementation Notes:
- Used `StripeClient` instance via constructor instead of global `StripeConfiguration.ApiKey` for thread-safety and testability (recommended Stripe SDK pattern)
- Graceful cancellation via `CancelAtPeriodEnd = true` instead of immediate delete
- `SyncPlanToProviderAsync` compares existing prices before creating new ones (Stripe Prices are immutable)
- DI wiring tests use `ServiceCollection` + `ConfigurationBuilder.AddInMemoryCollection` directly (no PostgreSQL/Testcontainers needed for pure wiring tests)
- Pinned `Stripe.net` to exact version 47.4.0 for build determinism

### Convenzioni da task Done correlati

From **T-07 (Public plans API)**:
- Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application.
- Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block, since MediatR only scans the Application assembly.
- Used LINQ projection (`Select`) instead of Mapster for mapping — simpler for a read-only query with no complex logic.

From **T-08 (Checkout flow)**:
- BillingController created with `[Authorize]`, primary constructor pattern (ISender), and helper properties (CurrentUserId, IpAddress, UserAgent) matching AdminSettingsController
- Handler registered manually in DI inside `IsPaymentsModuleEnabled()` block, consistent with GetPlansQueryHandler
- InMemoryDatabase used for DbContext in handler unit tests; NSubstitute for UserManager and IPaymentGateway to verify call patterns (Received/DidNotReceive)
- Metadata keys `"userId"` and `"planId"` passed in checkout request for webhook compatibility with StripeWebhookEventHandler

From **T-09 (Subscription management)**:
- All 3 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
- Unit tests use InMemory DB for `ApplicationDbContext` and NSubstitute for `IPaymentGateway`/`IAuditService`, following the pattern established in T-08

### Riferimenti

- **Requirements:** `docs/requirements/FEAT-3.md` — sezione RF-7 (Admin — gestione piani) e US-007 (Admin — CRUD piani di abbonamento)
- **Plan:** `docs/plans/PLAN-5.md` — Task T-10

## Stato attuale del codice

### Entità di dominio (già esistenti)
- `backend/src/Seed.Domain/Entities/SubscriptionPlan.cs` — Id, Name, Description, MonthlyPrice, YearlyPrice, StripePriceIdMonthly, StripePriceIdYearly, StripeProductId, TrialDays, IsFreeTier, IsDefault, IsPopular, Status (PlanStatus), SortOrder, CreatedAt, UpdatedAt, Features, Subscriptions
- `backend/src/Seed.Domain/Entities/PlanFeature.cs` — Id, PlanId, Key, Description, LimitValue, SortOrder, Plan
- `backend/src/Seed.Domain/Enums/PlanStatus.cs` — Active, Inactive, Archived

### Interfacce e modelli gateway (già esistenti)
- `backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs` — metodo `SyncPlanToProviderAsync(SyncPlanRequest, CancellationToken)` che ritorna `ProductSyncResult`
- `backend/src/Seed.Application/Common/Models/SyncPlanRequest.cs` — record con ProductId, Name, Description, MonthlyPriceInCents, YearlyPriceInCents, ExistingMonthlyPriceId, ExistingYearlyPriceId
- `backend/src/Seed.Application/Common/Models/ProductSyncResult.cs` — record con ProductId, MonthlyPriceId, YearlyPriceId

### DTOs billing esistenti
- `backend/src/Seed.Application/Billing/Models/PlanDto.cs` — sealed record per public API (non include Status, Stripe IDs)
- `backend/src/Seed.Application/Billing/Models/PlanFeatureDto.cs` — sealed record

### Permessi e audit
- `backend/src/Seed.Domain/Authorization/Permissions.cs` — 16 permessi in 6 categorie (Users, Roles, AuditLog, Settings, Dashboard, SystemHealth). Array `All` e metodo `GetAll()`
- `backend/src/Seed.Domain/Authorization/AuditActions.cs` — include già azioni Subscription/Payments (SubscriptionCreated, CheckoutSessionCreated, etc.)
- `backend/src/Seed.Api/Authorization/HasPermissionAttribute.cs` — `[HasPermission(Permissions.X.Y)]` → `AuthorizeAttribute` con policy `Permission:{name}`

### Pattern controller admin
- `backend/src/Seed.Api/Controllers/AdminRolesController.cs` — pattern di riferimento: `[Authorize]`, primary constructor `(ISender sender)`, helper CurrentUserId/IpAddress/UserAgent, `[HasPermission(...)]` su ogni action, route `api/v{version:apiVersion}/admin/roles`
- `backend/src/Seed.Api/Controllers/AdminSettingsController.cs` — stesso pattern

### DI registration
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — handlers billing registrati manualmente dentro `if (configuration.IsPaymentsModuleEnabled()) { ... }` (righe 87-91)

### Seeder permessi
- `backend/src/Seed.Infrastructure/Persistence/Seeders/RolesAndPermissionsSeeder.cs` — legge `Permissions.GetAll()` e semina automaticamente tutti i permessi nel DB. SuperAdmin riceve tutti, Admin riceve tutti meno `AdminExcludedPermissions`

### Test
- `backend/tests/Seed.UnitTests/Domain/PermissionsTests.cs` — test `GetAll_Should_Return_16_Permissions()` (va aggiornato a 19 dopo aggiunta Plans)
- `backend/tests/Seed.UnitTests/Billing/Queries/GetPlansQueryHandlerTests.cs` — pattern di riferimento per handler tests con InMemory DB

## Piano di esecuzione

### Step 1: Aggiungere permessi Plans a `Permissions.cs`

**File:** `backend/src/Seed.Domain/Authorization/Permissions.cs`

Aggiungere:
```csharp
public static class Plans
{
    public const string Read = "Plans.Read";
    public const string Create = "Plans.Create";
    public const string Update = "Plans.Update";
}
```

Aggiungere `Plans.Read, Plans.Create, Plans.Update` all'array `All` (totale diventa 19).

### Step 2: Aggiungere audit actions per plan CRUD

**File:** `backend/src/Seed.Domain/Authorization/AuditActions.cs`

Aggiungere:
```csharp
public const string PlanCreated = "PlanCreated";
public const string PlanUpdated = "PlanUpdated";
public const string PlanArchived = "PlanArchived";
```

### Step 3: Creare DTOs admin plans

**File da creare:** `backend/src/Seed.Application/Admin/Plans/Models/AdminPlanDto.cs`

DTO per la lista admin — include tutti i campi + subscriber count:
```csharp
public sealed record AdminPlanDto(
    Guid Id, string Name, string? Description,
    decimal MonthlyPrice, decimal YearlyPrice,
    string? StripePriceIdMonthly, string? StripePriceIdYearly, string? StripeProductId,
    int TrialDays, bool IsFreeTier, bool IsDefault, bool IsPopular,
    string Status, int SortOrder,
    DateTime CreatedAt, DateTime UpdatedAt,
    int SubscriberCount,
    IReadOnlyList<PlanFeatureDto> Features);
```

**File da creare:** `backend/src/Seed.Application/Admin/Plans/Models/AdminPlanDetailDto.cs`

DTO per il dettaglio (stessa struttura di AdminPlanDto, può essere lo stesso tipo o alias).

**File da creare:** `backend/src/Seed.Application/Admin/Plans/Models/CreatePlanRequest.cs`

Record per il body della request di creazione:
```csharp
public sealed record CreatePlanFeatureRequest(string Key, string Description, string? LimitValue, int SortOrder);
```

### Step 4: Creare i comandi e query in Application

**File da creare:**
- `backend/src/Seed.Application/Admin/Plans/Commands/CreatePlan/CreatePlanCommand.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/CreatePlan/CreatePlanCommandValidator.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/UpdatePlan/UpdatePlanCommand.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/UpdatePlan/UpdatePlanCommandValidator.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/ArchivePlan/ArchivePlanCommand.cs`
- `backend/src/Seed.Application/Admin/Plans/Queries/GetAdminPlans/GetAdminPlansQuery.cs`
- `backend/src/Seed.Application/Admin/Plans/Queries/GetAdminPlanById/GetAdminPlanByIdQuery.cs`

Commands devono includere `[JsonIgnore] CurrentUserId, IpAddress, UserAgent` come da pattern CreateRoleCommand.

Validators:
- CreatePlanCommandValidator: Name NotEmpty + MaximumLength(200), MonthlyPrice >= 0, YearlyPrice >= 0, Features not null
- UpdatePlanCommandValidator: stessi + PlanId NotEmpty

### Step 5: Creare gli handler in Infrastructure

**File da creare:**
- `backend/src/Seed.Infrastructure/Billing/Commands/CreatePlanCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/UpdatePlanCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/ArchivePlanCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminPlansQueryHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminPlanByIdQueryHandler.cs`

Pattern: handler in `Seed.Infrastructure/Billing/` con primary constructor, DI di `ApplicationDbContext`, `IPaymentGateway`, `IAuditService`.

**CreatePlanCommandHandler:**
1. Crea SubscriptionPlan entity + PlanFeature entities
2. SaveChanges per ottenere Id
3. Chiama `IPaymentGateway.SyncPlanToProviderAsync()` con MonthlyPrice*100 e YearlyPrice*100 (cents)
4. Salva StripeProductId, StripePriceIdMonthly, StripePriceIdYearly nel plan
5. SaveChanges di nuovo
6. Audit log con AuditActions.PlanCreated
7. Return plan Id

**UpdatePlanCommandHandler:**
1. Carica plan con features (tracking)
2. Aggiorna campi (Name, Description, TrialDays, IsPopular, IsDefault, IsFreeTier, SortOrder, MonthlyPrice, YearlyPrice)
3. Gestisci features: rimuovi quelle non più presenti, aggiorna quelle esistenti, aggiungi nuove
4. Chiama `SyncPlanToProviderAsync()` con ExistingMonthlyPriceId/ExistingYearlyPriceId (Stripe crea nuovi price se il prezzo cambia)
5. Aggiorna Stripe IDs
6. SaveChanges
7. Audit log con AuditActions.PlanUpdated

**ArchivePlanCommandHandler:**
1. Carica plan
2. Setta Status = PlanStatus.Archived e UpdatedAt
3. SaveChanges
4. Audit log con AuditActions.PlanArchived

**GetAdminPlansQueryHandler:**
1. Carica tutti i piani (qualsiasi status) ordinati per SortOrder
2. Per ogni piano conta i subscriber (UserSubscription con status Active o Trialing)
3. Usa LINQ projection

**GetAdminPlanByIdQueryHandler:**
1. Carica piano by Id con features
2. Conta subscriber
3. Return null/failure se non trovato

### Step 6: Registrare gli handler in DI

**File:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`

Aggiungere dentro il blocco `if (configuration.IsPaymentsModuleEnabled())`:
```csharp
services.AddScoped<IRequestHandler<CreatePlanCommand, Result<Guid>>, CreatePlanCommandHandler>();
services.AddScoped<IRequestHandler<UpdatePlanCommand, Result<bool>>, UpdatePlanCommandHandler>();
services.AddScoped<IRequestHandler<ArchivePlanCommand, Result<bool>>, ArchivePlanCommandHandler>();
services.AddScoped<IRequestHandler<GetAdminPlansQuery, Result<IReadOnlyList<AdminPlanDto>>>, GetAdminPlansQueryHandler>();
services.AddScoped<IRequestHandler<GetAdminPlanByIdQuery, Result<AdminPlanDto>>, GetAdminPlanByIdQueryHandler>();
```

### Step 7: Creare il controller

**File da creare:** `backend/src/Seed.Api/Controllers/AdminPlansController.cs`

Pattern identico a `AdminRolesController`:
- Route: `api/v{version:apiVersion}/admin/plans`
- `[Authorize]` a livello classe
- `[HasPermission(Permissions.Plans.Read)]` su GET
- `[HasPermission(Permissions.Plans.Create)]` su POST
- `[HasPermission(Permissions.Plans.Update)]` su PUT e POST archive
- Primary constructor `(ISender sender)`
- Helper properties CurrentUserId, IpAddress, UserAgent

Endpoints:
- `GET /` → GetAdminPlansQuery → Ok(data)
- `GET /{id:guid}` → GetAdminPlanByIdQuery → Ok(data) o NotFound
- `POST /` → CreatePlanCommand → CreatedAtAction
- `PUT /{id:guid}` → UpdatePlanCommand → NoContent
- `POST /{id:guid}/archive` → ArchivePlanCommand → NoContent

### Step 8: Scrivere unit tests

**File da creare:**
- `backend/tests/Seed.UnitTests/Billing/Commands/CreatePlanCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/UpdatePlanCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/ArchivePlanCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/CreatePlanCommandValidatorTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/UpdatePlanCommandValidatorTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetAdminPlansQueryHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetAdminPlanByIdQueryHandlerTests.cs`

Pattern: InMemory DB per ApplicationDbContext, NSubstitute per IPaymentGateway e IAuditService.

Test cases per CreatePlanCommandHandler:
- Should create plan and sync to Stripe
- Should create plan with features
- Should set correct Stripe IDs from sync result
- Should audit log on creation

Test cases per UpdatePlanCommandHandler:
- Should update plan metadata
- Should add new features
- Should remove deleted features
- Should sync to Stripe on update
- Should return failure if plan not found

Test cases per ArchivePlanCommandHandler:
- Should set status to Archived
- Should return failure if plan not found
- Should audit log on archive

Test cases per validators:
- CreatePlan: Name empty, Name too long, Price negative, valid command
- UpdatePlan: PlanId empty, Name empty, valid command

Test cases per GetAdminPlansQueryHandler:
- Should return all plans including inactive/archived
- Should include subscriber count
- Should order by SortOrder

Test cases per GetAdminPlanByIdQueryHandler:
- Should return plan with features
- Should return failure if not found

### Step 9: Aggiornare il test PermissionsTests

**File:** `backend/tests/Seed.UnitTests/Domain/PermissionsTests.cs`

Aggiornare `GetAll_Should_Return_16_Permissions()` → `GetAll_Should_Return_19_Permissions()` (16 + 3 Plans permissions).

### Step 10: Build e test

```bash
dotnet build backend/Seed.slnx
dotnet test backend/Seed.slnx
```

## Criteri di completamento

- [x] Create, Update, Archive commands with handlers and validators
- [x] Admin list query with subscriber counts
- [x] Admin detail query
- [x] Stripe sync on create/update
- [x] Plans permissions added and registered in seeder
- [x] Audit log for all mutations
- [x] Controller with proper permission attributes
- [x] Unit tests for all handlers
- [x] Validator tests

## Risultato

### File modificati
- `backend/src/Seed.Domain/Authorization/Permissions.cs` — aggiunta classe `Plans` con Read/Create/Update (totale 19 permessi)
- `backend/src/Seed.Domain/Authorization/AuditActions.cs` — aggiunte costanti PlanCreated/PlanUpdated/PlanArchived
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione 5 nuovi handler nel blocco `IsPaymentsModuleEnabled()`
- `backend/tests/Seed.UnitTests/Domain/PermissionsTests.cs` — aggiornato count a 19, aggiunta categoria Plans e costanti

### File creati
- `backend/src/Seed.Application/Admin/Plans/Models/AdminPlanDto.cs` — DTO admin con SubscriberCount e Stripe IDs
- `backend/src/Seed.Application/Admin/Plans/Models/CreatePlanFeatureRequest.cs` — request record per feature
- `backend/src/Seed.Application/Admin/Plans/Commands/CreatePlan/CreatePlanCommand.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/CreatePlan/CreatePlanCommandValidator.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/UpdatePlan/UpdatePlanCommand.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/UpdatePlan/UpdatePlanCommandValidator.cs`
- `backend/src/Seed.Application/Admin/Plans/Commands/ArchivePlan/ArchivePlanCommand.cs`
- `backend/src/Seed.Application/Admin/Plans/Queries/GetAdminPlans/GetAdminPlansQuery.cs`
- `backend/src/Seed.Application/Admin/Plans/Queries/GetAdminPlanById/GetAdminPlanByIdQuery.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/CreatePlanCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/UpdatePlanCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/ArchivePlanCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminPlansQueryHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminPlanByIdQueryHandler.cs`
- `backend/src/Seed.Api/Controllers/AdminPlansController.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/CreatePlanCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/UpdatePlanCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/ArchivePlanCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/CreatePlanCommandValidatorTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/UpdatePlanCommandValidatorTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetAdminPlansQueryHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetAdminPlanByIdQueryHandlerTests.cs`

### Scelte chiave
- **AdminPlanDetailDto non creato separatamente:** il piano specifica che può essere lo stesso tipo di AdminPlanDto, quindi riutilizzato per entrambi list e detail
- **UpdatePlan gestisce features tramite Key matching:** features con lo stesso Key vengono aggiornate, quelle mancanti rimosse, nuove aggiunte
- **ArchivePlan non richiede IPaymentGateway:** l'archiviazione è solo un cambio di status nel DB, non necessita sync Stripe
- **Permessi auto-registrati nel seeder:** il RolesAndPermissionsSeeder legge `Permissions.GetAll()`, quindi i 3 nuovi permessi Plans vengono seminati automaticamente

### Deviazioni dal mini-plan
- Nessuna deviazione significativa
