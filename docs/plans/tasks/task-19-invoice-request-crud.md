# Task 19: Backend — Invoice request CRUD

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-012 | Richiesta fattura manuale | T-19, T-20 | 🔄 In Progress (domain entities done) |

### Dipendenze (da 'Depends on:')
**T-03: EF Core configuration and migration**
- Implementation Notes:
  - Followed existing `RefreshTokenConfiguration` pattern for structure and style (sealed class, file-scoped namespace)
  - Enum properties converted to string in DB via `HasConversion<string>()` for readability
  - Unique index on `StripeSubscriptionId` uses `HasFilter("\"StripeSubscriptionId\" IS NOT NULL")` to handle nullable correctly in PostgreSQL
  - `DeleteBehavior.Restrict` on `SubscriptionPlan → UserSubscription` to prevent deletion of plans with active subscriptions
  - `DeleteBehavior.Cascade` on `User → UserSubscription` and `User → InvoiceRequest`, consistent with existing RefreshToken pattern

**T-09: Subscription management — portal, view, cancel**
- Implementation Notes:
  - `GetMySubscription` returns `null` (not failure) when no subscription exists — "no data ≠ error" pattern, consistent with clean API semantics
  - `CancelSubscription` sets both `CanceledAt` and `UpdatedAt` locally after calling `IPaymentGateway.CancelSubscriptionAsync` (which sets cancel_at_period_end on Stripe); webhook will later sync final status
  - `CreatePortalSession` intentionally has no audit logging — it's a redirect to Stripe with no local state mutation
  - All 3 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
  - Unit tests use InMemory DB for `ApplicationDbContext` and NSubstitute for `IPaymentGateway`/`IAuditService`, following the pattern established in T-08

### Convenzioni da task Done correlati
- **T-07 (Public plans API):** Handler placed in `Seed.Infrastructure/Billing/Queries/` (not Application) because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application. Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block.
- **T-08 (Checkout flow):** BillingController created with `[Authorize]`, primary constructor pattern (ISender), and helper properties (CurrentUserId, IpAddress, UserAgent) matching AdminSettingsController. InMemoryDatabase used for DbContext in handler unit tests; NSubstitute for UserManager and IPaymentGateway.
- **T-10 (Admin CRUD plans):** Plans permissions (Read/Create/Update) are seeded automatically because `RolesAndPermissionsSeeder` reads `Permissions.GetAll()` — no manual seeder change required. All handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block.
- **T-11 (Admin subscriptions dashboard):** Query handlers placed in `Seed.Infrastructure/Billing/Queries/` (not Application) because `ApplicationDbContext` is only available in Infrastructure. `Subscriptions.Read` permission added to the `All` array in `Permissions.cs`, auto-picked up by `RolesAndPermissionsSeeder`.
- **T-02 (Domain entities):** Created `InvoiceRequest` entity with all fiscal fields, `InvoiceRequestStatus` enum (Requested/InProgress/Issued), `CustomerType` enum (Individual/Company). Added `InvoiceRequests` navigation property to `ApplicationUser`.
- **T-06 (Webhook handler):** 7 audit actions added to `AuditActions.cs`. `IWebhookEventHandler` interface kept Stripe-agnostic in `Seed.Application`, implementation in `Seed.Infrastructure`.

### Riferimenti
- `docs/requirements/FEAT-3.md` — RF-9: Richiesta fattura manuale. US-012 acceptance criteria.
- `docs/plans/PLAN-5.md` — Task T-19 definition.

## Stato attuale del codice

### Domain entities (already exist)
- `backend/src/Seed.Domain/Entities/InvoiceRequest.cs` — Entity with all fiscal fields: Id, UserId, StripePaymentIntentId, CustomerType, FullName, CompanyName, Address, City, PostalCode, Country, FiscalCode, VatNumber, SdiCode, PecEmail, Status, CreatedAt, UpdatedAt, ProcessedAt. Navigation: `User`.
- `backend/src/Seed.Domain/Enums/InvoiceRequestStatus.cs` — `Requested`, `InProgress`, `Issued`
- `backend/src/Seed.Domain/Enums/CustomerType.cs` — `Individual`, `Company`

### EF Core config (already exists)
- `backend/src/Seed.Infrastructure/Persistence/Configurations/InvoiceRequestConfiguration.cs` — Indexes on UserId and Status, cascade delete on User→InvoiceRequest.
- `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs` — `DbSet<InvoiceRequest> InvoiceRequests` already present.

### Existing patterns to follow
- **BillingController** (`backend/src/Seed.Api/Controllers/BillingController.cs`): `[Authorize]`, primary constructor `(ISender sender)`, helper properties `CurrentUserId`, `IpAddress`, `UserAgent`. User endpoints at `api/v1.0/billing/...`.
- **AdminSubscriptionsController** (`backend/src/Seed.Api/Controllers/AdminSubscriptionsController.cs`): `[Authorize]`, `HasPermission(Permissions.Subscriptions.Read)`, admin endpoints at `api/v1.0/admin/subscriptions`.
- **Command pattern**: `sealed record` with `[JsonIgnore]` on UserId/IpAddress/UserAgent, implements `IRequest<Result<T>>`.
- **Handler pattern**: Primary constructor, handler in `Seed.Infrastructure/Billing/Commands/` or `Queries/`, manual DI registration in `DependencyInjection.cs` inside `IsPaymentsModuleEnabled()` block.
- **Validator pattern**: `AbstractValidator<T>` in same folder as command in Application.
- **AuditActions**: Constants in `backend/src/Seed.Domain/Authorization/AuditActions.cs`.
- **Permissions**: Static class in `backend/src/Seed.Domain/Authorization/Permissions.cs` with `All` array for auto-seeding.
- **Result pattern**: `Result<T>.Success(data)` / `Result<T>.Failure(errors)`.
- **PagedResult**: `PagedResult<T>(Items, PageNumber, PageSize, TotalCount)` in `Seed.Application/Common/Models/`.
- **Unit tests**: InMemory DB for `ApplicationDbContext`, NSubstitute for interfaces, `IDisposable` pattern, `FluentAssertions`.

## Piano di esecuzione

### Step 1: Create DTOs in Application layer
**File: `backend/src/Seed.Application/Billing/Models/InvoiceRequestDto.cs`** (new)
- `sealed record InvoiceRequestDto(Guid Id, string CustomerType, string FullName, string? CompanyName, string Address, string City, string PostalCode, string Country, string? FiscalCode, string? VatNumber, string? SdiCode, string? PecEmail, string? StripePaymentIntentId, string Status, DateTime CreatedAt, DateTime? ProcessedAt)`

**File: `backend/src/Seed.Application/Admin/InvoiceRequests/Models/AdminInvoiceRequestDto.cs`** (new)
- `sealed record AdminInvoiceRequestDto(...)` — same fields as InvoiceRequestDto plus `UserEmail`, `UserFullName`

### Step 2: Add AuditActions for invoice requests
**File: `backend/src/Seed.Domain/Authorization/AuditActions.cs`** (modify)
- Add `InvoiceRequestCreated` and `InvoiceRequestStatusUpdated` constants

### Step 3: Add `Subscriptions.Manage` permission (or reuse `Subscriptions.Read` for listing + a new write permission)
Since the plan says admin endpoints require `Subscriptions.Read` for the GET and a manage permission for status update:
**File: `backend/src/Seed.Domain/Authorization/Permissions.cs`** (modify)
- Add `Manage` to `Subscriptions` class: `public const string Manage = "Subscriptions.Manage";`
- Add to `All` array

### Step 4: Create commands and queries

**CreateInvoiceRequestCommand** (new files):
- `backend/src/Seed.Application/Billing/Commands/CreateInvoiceRequest/CreateInvoiceRequestCommand.cs`
  - Fields: CustomerType, FullName, CompanyName?, Address, City, PostalCode, Country, FiscalCode?, VatNumber?, SdiCode?, PecEmail?, StripePaymentIntentId?
  - `[JsonIgnore]` UserId, IpAddress, UserAgent
  - Returns `Result<Guid>` (the new InvoiceRequest Id)
- `backend/src/Seed.Application/Billing/Commands/CreateInvoiceRequest/CreateInvoiceRequestCommandValidator.cs`
  - FullName NotEmpty, Address NotEmpty, City NotEmpty, PostalCode NotEmpty, Country NotEmpty
  - CustomerType IsInEnum
  - When CustomerType == Company: CompanyName NotEmpty, VatNumber NotEmpty

**GetMyInvoiceRequestsQuery** (new files):
- `backend/src/Seed.Application/Billing/Queries/GetMyInvoiceRequests/GetMyInvoiceRequestsQuery.cs`
  - `sealed record GetMyInvoiceRequestsQuery(Guid UserId) : IRequest<Result<IReadOnlyList<InvoiceRequestDto>>>`

**GetInvoiceRequestsQuery** (admin, new files):
- `backend/src/Seed.Application/Admin/InvoiceRequests/Queries/GetInvoiceRequests/GetInvoiceRequestsQuery.cs`
  - Paginated with optional StatusFilter
  - Returns `Result<PagedResult<AdminInvoiceRequestDto>>`

**UpdateInvoiceRequestStatusCommand** (admin, new files):
- `backend/src/Seed.Application/Admin/InvoiceRequests/Commands/UpdateInvoiceRequestStatus/UpdateInvoiceRequestStatusCommand.cs`
  - Fields: InvoiceRequestId (Guid), NewStatus (InvoiceRequestStatus)
  - `[JsonIgnore]` CurrentUserId, IpAddress, UserAgent
  - Returns `Result<bool>`
- `backend/src/Seed.Application/Admin/InvoiceRequests/Commands/UpdateInvoiceRequestStatus/UpdateInvoiceRequestStatusCommandValidator.cs`
  - InvoiceRequestId NotEmpty, NewStatus IsInEnum

### Step 5: Create handlers in Infrastructure layer

**File: `backend/src/Seed.Infrastructure/Billing/Commands/CreateInvoiceRequestCommandHandler.cs`** (new)
- Create InvoiceRequest entity from command fields
- Save to DB
- Audit log with AuditActions.InvoiceRequestCreated

**File: `backend/src/Seed.Infrastructure/Billing/Queries/GetMyInvoiceRequestsQueryHandler.cs`** (new)
- Query `dbContext.InvoiceRequests` where UserId matches, AsNoTracking, OrderByDescending CreatedAt
- Project to InvoiceRequestDto via LINQ Select

**File: `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminInvoiceRequestsQueryHandler.cs`** (new)
- Paginated query with optional status filter, Include User
- Project to AdminInvoiceRequestDto

**File: `backend/src/Seed.Infrastructure/Billing/Commands/UpdateInvoiceRequestStatusCommandHandler.cs`** (new)
- Find InvoiceRequest by Id, update Status and UpdatedAt
- If new status is Issued, set ProcessedAt
- Audit log with AuditActions.InvoiceRequestStatusUpdated

### Step 6: Register handlers in DI
**File: `backend/src/Seed.Infrastructure/DependencyInjection.cs`** (modify)
- Add 4 `AddScoped<IRequestHandler<...>>` lines inside the `IsPaymentsModuleEnabled()` block
- Add necessary using statements

### Step 7: Create API endpoints

**File: `backend/src/Seed.Api/Controllers/BillingController.cs`** (modify)
- Add `POST api/v1.0/billing/invoice-request` — create (user, requires auth)
- Add `GET api/v1.0/billing/invoice-requests` — my requests (user, requires auth)

**File: `backend/src/Seed.Api/Controllers/AdminInvoiceRequestsController.cs`** (new)
- `GET api/v1.0/admin/invoice-requests` — all requests with `HasPermission(Permissions.Subscriptions.Read)`
- `PUT api/v1.0/admin/invoice-requests/{id}/status` — update status with `HasPermission(Permissions.Subscriptions.Manage)`

### Step 8: Write unit tests

**File: `backend/tests/Seed.UnitTests/Billing/Commands/CreateInvoiceRequestCommandHandlerTests.cs`** (new)
- Test successful creation
- Test audit log is called
- Test all fields are saved correctly

**File: `backend/tests/Seed.UnitTests/Billing/Commands/CreateInvoiceRequestCommandValidatorTests.cs`** (new)
- Test required fields validation
- Test Company type requires CompanyName and VatNumber
- Test Individual type does not require CompanyName/VatNumber
- Test valid command passes

**File: `backend/tests/Seed.UnitTests/Billing/Commands/UpdateInvoiceRequestStatusCommandHandlerTests.cs`** (new)
- Test successful status update
- Test not found returns failure
- Test ProcessedAt set when status is Issued
- Test audit log is called

**File: `backend/tests/Seed.UnitTests/Billing/Queries/GetMyInvoiceRequestsQueryHandlerTests.cs`** (new)
- Test returns user's requests only
- Test ordered by CreatedAt descending
- Test empty list when no requests

**File: `backend/tests/Seed.UnitTests/Billing/Queries/GetAdminInvoiceRequestsQueryHandlerTests.cs`** (new)
- Test returns paginated results
- Test status filter
- Test includes user email

### Step 9: Build and run tests
```bash
cd backend && dotnet build Seed.slnx && dotnet test Seed.slnx
```

## Criteri di completamento
- [x] Create invoice request with all fiscal fields
- [x] User can view own invoice request history
- [x] Admin can list and update request status
- [x] Audit log on create and status change
- [x] Validator tests
- [x] Unit tests for handlers

## Risultato

### File modificati
- `backend/src/Seed.Domain/Authorization/AuditActions.cs` — aggiunti `InvoiceRequestCreated` e `InvoiceRequestStatusUpdated`
- `backend/src/Seed.Domain/Authorization/Permissions.cs` — aggiunto `Subscriptions.Manage` e inserito nell'array `All`
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrati 4 handler nel blocco `IsPaymentsModuleEnabled()`
- `backend/src/Seed.Api/Controllers/BillingController.cs` — aggiunti endpoint `POST /invoice-request` e `GET /invoice-requests`

### File creati
- `backend/src/Seed.Application/Billing/Models/InvoiceRequestDto.cs`
- `backend/src/Seed.Application/Admin/InvoiceRequests/Models/AdminInvoiceRequestDto.cs`
- `backend/src/Seed.Application/Billing/Commands/CreateInvoiceRequest/CreateInvoiceRequestCommand.cs`
- `backend/src/Seed.Application/Billing/Commands/CreateInvoiceRequest/CreateInvoiceRequestCommandValidator.cs`
- `backend/src/Seed.Application/Billing/Queries/GetMyInvoiceRequests/GetMyInvoiceRequestsQuery.cs`
- `backend/src/Seed.Application/Admin/InvoiceRequests/Queries/GetInvoiceRequests/GetInvoiceRequestsQuery.cs`
- `backend/src/Seed.Application/Admin/InvoiceRequests/Commands/UpdateInvoiceRequestStatus/UpdateInvoiceRequestStatusCommand.cs`
- `backend/src/Seed.Application/Admin/InvoiceRequests/Commands/UpdateInvoiceRequestStatus/UpdateInvoiceRequestStatusCommandValidator.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/CreateInvoiceRequestCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Commands/UpdateInvoiceRequestStatusCommandHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetMyInvoiceRequestsQueryHandler.cs`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminInvoiceRequestsQueryHandler.cs`
- `backend/src/Seed.Api/Controllers/AdminInvoiceRequestsController.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/CreateInvoiceRequestCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/CreateInvoiceRequestCommandValidatorTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Commands/UpdateInvoiceRequestStatusCommandHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetMyInvoiceRequestsQueryHandlerTests.cs`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetAdminInvoiceRequestsQueryHandlerTests.cs`

### Scelte chiave
- `UpdateInvoiceRequestStatusCommand`: `InvoiceRequestId` è `[JsonIgnore]` (viene dal path parameter URL), `NewStatus` viene dal body — stesso pattern di `UpdatePlanCommand`
- Handlers admin posizionati in `Seed.Infrastructure/Billing/Commands|Queries/` (non in Application) perché `ApplicationDbContext` è disponibile solo in Infrastructure
- `Subscriptions.Manage` riutilizza la classe esistente `Subscriptions` piuttosto che creare una nuova classe `InvoiceRequests` — coerente con l'approccio del piano

### Deviazioni dal piano
Nessuna deviazione significativa. Tutti gli step eseguiti come pianificato.
