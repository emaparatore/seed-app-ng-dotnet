# Task 11: Admin subscriptions dashboard API

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-008 | Admin — dashboard abbonamenti | T-11, T-18 | ⏳ Not Started |

### Dipendenze (da 'Depends on:')
**T-03: EF Core configuration and migration**
- Implementation Notes:
  - Followed existing `RefreshTokenConfiguration` pattern for structure and style (sealed class, file-scoped namespace)
  - Enum properties converted to string in DB via `HasConversion<string>()` for readability
  - Unique index on `StripeSubscriptionId` uses `HasFilter("\"StripeSubscriptionId\" IS NOT NULL")` to handle nullable correctly in PostgreSQL
  - `DeleteBehavior.Restrict` on `SubscriptionPlan → UserSubscription` to prevent deletion of plans with active subscriptions
  - `DeleteBehavior.Cascade` on `User → UserSubscription` and `User → InvoiceRequest`, consistent with existing RefreshToken pattern

**T-06: Webhook handler endpoint and event processing**
- Implementation Notes:
  - Used `Stripe.EventTypes` constants instead of `Stripe.Events` (which doesn't exist in Stripe.net v47.4.0)
  - `WebhookWebApplicationFactory` extends `CustomWebApplicationFactory` with payments module enabled and a known webhook secret, avoiding modifications to the shared factory
  - Unit tests use InMemory database provider for `ApplicationDbContext` to test business logic without external dependencies
  - JSON test payloads include `livemode`, `pending_webhooks`, and `request` fields required by `EventUtility.ParseEvent()` in Stripe.net v47
  - `IWebhookEventHandler` interface kept Stripe-agnostic in `Seed.Application`, implementation in `Seed.Infrastructure`

### Convenzioni da task Done correlati
- **T-07 (Public plans API):** Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application. Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block, since MediatR only scans the Application assembly.
- **T-10 (Admin CRUD plans):** `AdminPlanDto` reused for both list and detail endpoints, as it already contains full details including Stripe IDs and features. All 5 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`, consistent with prior billing tasks. Plans permissions (Read/Create/Update) are seeded automatically because `RolesAndPermissionsSeeder` reads `Permissions.GetAll()` — no manual seeder change required.
- **T-09 (Subscription management):** `GetMySubscription` returns `null` (not failure) when no subscription exists — "no data ≠ error" pattern. All 3 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`. Unit tests use InMemory DB for `ApplicationDbContext` and NSubstitute for `IPaymentGateway`/`IAuditService`, following the pattern established in T-08.

### Riferimenti
- `docs/requirements/FEAT-3.md` — US-008 (RF-7): Admin dashboard abbonamenti
- `docs/plans/PLAN-5.md` — T-11 definition

## Stato attuale del codice

### File esistenti rilevanti
- **`backend/src/Seed.Domain/Entities/UserSubscription.cs`** — Entity con Id, UserId, PlanId, Status (SubscriptionStatus enum), StripeSubscriptionId, StripeCustomerId, CurrentPeriodStart, CurrentPeriodEnd, TrialEnd, CanceledAt, CreatedAt, UpdatedAt. Nav props: User, Plan.
- **`backend/src/Seed.Domain/Entities/SubscriptionPlan.cs`** — Entity con Id, Name, MonthlyPrice, YearlyPrice, IsFreeTier, Status (PlanStatus enum), etc. Nav props: Features, Subscriptions.
- **`backend/src/Seed.Domain/Enums/SubscriptionStatus.cs`** — Enum: Active, Trialing, PastDue, Canceled, Expired.
- **`backend/src/Seed.Domain/Authorization/Permissions.cs`** — Contains Plans.Read/Create/Update. **No `Subscriptions` section yet** — needs adding.
- **`backend/src/Seed.Domain/Authorization/AuditActions.cs`** — Existing audit actions.
- **`backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs`** — Has `DbSet<UserSubscription> UserSubscriptions` and `DbSet<SubscriptionPlan> SubscriptionPlans`.
- **`backend/src/Seed.Infrastructure/DependencyInjection.cs`** — Billing handlers registered inside `IsPaymentsModuleEnabled()` block (lines 73-104).
- **`backend/src/Seed.Api/Controllers/AdminPlansController.cs`** — Reference pattern for admin billing controller: uses ISender, HasPermission attribute, primary constructor, CurrentUserId/IpAddress/UserAgent helpers.
- **`backend/src/Seed.Application/Common/Models/PagedResult.cs`** — `PagedResult<T>(Items, PageNumber, PageSize, TotalCount)` sealed record with computed `TotalPages`, `HasPreviousPage`, `HasNextPage`.
- **`backend/src/Seed.Application/Common/Result.cs`** — `Result<T>` with `Success(T data)` and `Failure(params string[] errors)`.
- **`backend/src/Seed.Application/Admin/Users/Queries/GetUsers/GetUsersQuery.cs`** — Reference for paginated query pattern with PageNumber, PageSize, filters, sort.

### Pattern già in uso che il task deve seguire
1. **Query contracts in Application, handlers in Infrastructure** (per T-07 convention).
2. **Manual DI registration** of `IRequestHandler<...>` inside `IsPaymentsModuleEnabled()` block.
3. **Primary constructor pattern** for handlers (e.g., `GetAdminPlansQueryHandler(ApplicationDbContext dbContext)`).
4. **LINQ projection** with `Select()` for read queries (not Mapster), per T-07.
5. **`PagedResult<T>`** for paginated responses.
6. **`HasPermission` attribute** on controller actions.
7. **`sealed record`** for DTOs.
8. **InMemory DB** for unit tests, `WebhookWebApplicationFactory` for integration tests.
9. **Permissions auto-seeding** via `Permissions.GetAll()` — just add to `Permissions.cs` and the `All` array.

## Piano di esecuzione

### Step 1: Add `Subscriptions` permission to `Permissions.cs`
- **File:** `backend/src/Seed.Domain/Authorization/Permissions.cs`
- Add:
  ```csharp
  public static class Subscriptions
  {
      public const string Read = "Subscriptions.Read";
  }
  ```
- Add `Subscriptions.Read` to the `All` array.

### Step 2: Create DTOs in `Seed.Application/Admin/Subscriptions/Models/`
- **File:** `backend/src/Seed.Application/Admin/Subscriptions/Models/SubscriptionMetricsDto.cs`
  ```csharp
  public sealed record SubscriptionMetricsDto(
      decimal Mrr, int ActiveCount, int TrialingCount, decimal ChurnRate);
  ```
- **File:** `backend/src/Seed.Application/Admin/Subscriptions/Models/AdminSubscriptionDto.cs`
  ```csharp
  public sealed record AdminSubscriptionDto(
      Guid Id, string UserEmail, string PlanName,
      string Status, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
      DateTime? TrialEnd, DateTime? CanceledAt, DateTime CreatedAt);
  ```
- **File:** `backend/src/Seed.Application/Admin/Subscriptions/Models/AdminSubscriptionDetailDto.cs`
  ```csharp
  public sealed record AdminSubscriptionDetailDto(
      Guid Id, Guid UserId, string UserEmail, string UserFullName,
      Guid PlanId, string PlanName, decimal MonthlyPrice, decimal YearlyPrice,
      string Status, string? StripeSubscriptionId, string? StripeCustomerId,
      DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
      DateTime? TrialEnd, DateTime? CanceledAt,
      DateTime CreatedAt, DateTime UpdatedAt);
  ```

### Step 3: Create query contracts in `Seed.Application/Admin/Subscriptions/Queries/`
- **File:** `backend/src/Seed.Application/Admin/Subscriptions/Queries/GetSubscriptionMetrics/GetSubscriptionMetricsQuery.cs`
  ```csharp
  public sealed record GetSubscriptionMetricsQuery : IRequest<Result<SubscriptionMetricsDto>>;
  ```
- **File:** `backend/src/Seed.Application/Admin/Subscriptions/Queries/GetSubscriptionsList/GetSubscriptionsListQuery.cs`
  ```csharp
  public sealed record GetSubscriptionsListQuery : IRequest<Result<PagedResult<AdminSubscriptionDto>>>
  {
      public int PageNumber { get; init; } = 1;
      public int PageSize { get; init; } = 10;
      public Guid? PlanIdFilter { get; init; }
      public string? StatusFilter { get; init; }
  }
  ```
- **File:** `backend/src/Seed.Application/Admin/Subscriptions/Queries/GetSubscriptionDetail/GetSubscriptionDetailQuery.cs`
  ```csharp
  public sealed record GetSubscriptionDetailQuery(Guid Id) : IRequest<Result<AdminSubscriptionDetailDto>>;
  ```

### Step 4: Create query handlers in `Seed.Infrastructure/Billing/Queries/`
- **File:** `backend/src/Seed.Infrastructure/Billing/Queries/GetSubscriptionMetricsQueryHandler.cs`
  - MRR calculation: for each active/trialing subscription, get MonthlyPrice from plan. If user is on yearly billing (detected by period length > 35 days), use YearlyPrice/12. Sum all.
  - Active count: `Status == Active`
  - Trialing count: `Status == Trialing`
  - Churn rate: `canceled in last 30 days / (active + trialing + canceled in last 30 days)` — avoid division by zero.

- **File:** `backend/src/Seed.Infrastructure/Billing/Queries/GetSubscriptionsListQueryHandler.cs`
  - Query `UserSubscriptions` with `Include(Plan)` and `Include(User)`
  - Apply filters: PlanId, Status (parse string to enum)
  - Order by `CreatedAt` descending
  - Paginate with Skip/Take
  - Project to `AdminSubscriptionDto`

- **File:** `backend/src/Seed.Infrastructure/Billing/Queries/GetSubscriptionDetailQueryHandler.cs`
  - Query by Id with Include(Plan) and Include(User)
  - Return `Failure("Subscription not found")` if null
  - Project to `AdminSubscriptionDetailDto`

### Step 5: Register handlers in DI
- **File:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`
- Add 3 new `AddScoped<IRequestHandler<...>>` lines inside the `IsPaymentsModuleEnabled()` block, after existing billing handler registrations.
- Add required `using` statements for the new query/model namespaces.

### Step 6: Create `AdminSubscriptionsController`
- **File:** `backend/src/Seed.Api/Controllers/AdminSubscriptionsController.cs`
- Route: `api/v1.0/admin/subscriptions`
- `[Authorize]` on class
- Primary constructor with `ISender`
- 3 endpoints:
  - `GET /metrics` with `[HasPermission(Permissions.Subscriptions.Read)]`
  - `GET /` with pagination query params and `[HasPermission(Permissions.Subscriptions.Read)]`
  - `GET /{id:guid}` with `[HasPermission(Permissions.Subscriptions.Read)]`

### Step 7: Write unit tests
- **File:** `backend/tests/Seed.UnitTests/Billing/Queries/GetSubscriptionMetricsQueryHandlerTests.cs`
  - Test: calculates MRR from active subscriptions (monthly billing)
  - Test: includes trialing subscriptions in MRR
  - Test: handles yearly billing (period > 35 days → YearlyPrice/12)
  - Test: calculates churn rate correctly
  - Test: returns zero metrics when no subscriptions

- **File:** `backend/tests/Seed.UnitTests/Billing/Queries/GetSubscriptionsListQueryHandlerTests.cs`
  - Test: returns paginated results
  - Test: filters by planId
  - Test: filters by status
  - Test: returns empty list when no subscriptions

- **File:** `backend/tests/Seed.UnitTests/Billing/Queries/GetSubscriptionDetailQueryHandlerTests.cs`
  - Test: returns subscription detail
  - Test: returns failure when not found

### Step 8: Write integration test
- **File:** `backend/tests/Seed.IntegrationTests/Billing/AdminSubscriptionsControllerTests.cs`
  - Test: GET /metrics returns 200 with metrics (seed subscription data)
  - Test: GET / returns paginated list with filters
  - Use `WebhookWebApplicationFactory` (already configures payments module as enabled)
  - Pattern: register user, assign admin role + Subscriptions.Read permission, seed subscription data, call endpoints

## Criteri di completamento
- [ ] Metrics query calculates MRR, active count, trialing count, churn
- [ ] List query with pagination and filtering
- [ ] Detail query
- [ ] Subscriptions permissions added
- [ ] Controller with proper permission attributes
- [ ] Unit tests for metric calculations
- [ ] Integration test for list endpoint with filters
