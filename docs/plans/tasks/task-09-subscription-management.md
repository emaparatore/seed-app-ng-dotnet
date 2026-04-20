# Task 09: Subscription management — portal, view, cancel

## Contesto ereditato dal piano

### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-003 | Visualizzare il proprio abbonamento | T-09, T-16 | 🔄 In Progress (domain entities done) |
| US-004 | Gestire pagamento e cancellare abbonamento | T-09, T-16 | ⏳ Not Started |
| US-005 | Upgrade/downgrade del piano | T-09, T-16 | ⏳ Not Started |

### Dipendenze (da 'Depends on:')

**T-06: Webhook handler endpoint and event processing —**
- Used `Stripe.EventTypes` constants instead of `Stripe.Events` (which doesn't exist in Stripe.net v47.4.0)
- `WebhookWebApplicationFactory` extends `CustomWebApplicationFactory` with payments module enabled and a known webhook secret, avoiding modifications to the shared factory
- Unit tests use InMemory database provider for `ApplicationDbContext` to test business logic without external dependencies
- JSON test payloads include `livemode`, `pending_webhooks`, and `request` fields required by `EventUtility.ParseEvent()` in Stripe.net v47
- `IWebhookEventHandler` interface kept Stripe-agnostic in `Seed.Application`, implementation in `Seed.Infrastructure`

**T-08: Checkout flow — create checkout session —**
- BillingController created with `[Authorize]`, primary constructor pattern (ISender), and helper properties (CurrentUserId, IpAddress, UserAgent) matching AdminSettingsController
- Handler registered manually in DI inside `IsPaymentsModuleEnabled()` block, consistent with GetPlansQueryHandler
- InMemoryDatabase used for DbContext in handler unit tests; NSubstitute for UserManager and IPaymentGateway to verify call patterns (Received/DidNotReceive)
- StripeCustomerId lookup follows existing domain model: searches last UserSubscription for the user, creates new Stripe customer only if none found
- Metadata keys `"userId"` and `"planId"` passed in checkout request for webhook compatibility with StripeWebhookEventHandler

### Convenzioni da task Done correlati

- **T-07 (GetPlans):** Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application.
- **T-07 (GetPlans):** Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block, since MediatR only scans the Application assembly.
- **T-07 (GetPlans):** Used LINQ projection (`Select`) instead of Mapster for mapping — simpler for a read-only query with no complex logic.
- **T-07 (GetPlans):** Integration tests reuse `WebhookWebApplicationFactory` which already configures the payments module as enabled.
- **T-08 (Checkout):** BillingController uses primary constructor pattern `BillingController(ISender sender)` with `[Authorize]` and helper properties `CurrentUserId`, `IpAddress`, `UserAgent`.
- **T-04 (IPaymentGateway):** All DTOs use `sealed record` for immutability and value semantics.
- **T-03 (EF Config):** Enum properties converted to string in DB via `HasConversion<string>()` for readability.
- **T-02 (Domain):** Navigation properties to parent entities initialized with `= null!` (EF Core recommended pattern).

### Riferimenti

- `docs/requirements/FEAT-3.md` — RF-4 (gestione abbonamento, portale Stripe, cancellazione)
- `docs/plans/PLAN-5.md` — Phase 2 (Backend Business Logic), T-09

## Stato attuale del codice

### File esistenti rilevanti

- **`backend/src/Seed.Api/Controllers/BillingController.cs`** — Already created in T-08. Has `[Authorize]`, primary constructor with `ISender`, helper properties (`CurrentUserId`, `IpAddress`, `UserAgent`). Currently has only `POST /checkout` endpoint.
- **`backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs`** — Interface with 6 methods including `CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)`, `CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)`, `GetSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)`.
- **`backend/src/Seed.Application/Common/Models/SubscriptionDetails.cs`** — Record with: `SubscriptionId`, `CustomerId`, `Status`, `PriceId`, `CurrentPeriodStart`, `CurrentPeriodEnd`, `TrialEnd`, `CancelAtPeriodEnd`.
- **`backend/src/Seed.Domain/Entities/UserSubscription.cs`** — Entity with: `Id`, `UserId`, `PlanId`, `Status` (SubscriptionStatus enum), `StripeSubscriptionId`, `StripeCustomerId`, `CurrentPeriodStart`, `CurrentPeriodEnd`, `TrialEnd`, `CanceledAt`, `CreatedAt`, `UpdatedAt`. Navigation props: `User`, `Plan`.
- **`backend/src/Seed.Domain/Entities/SubscriptionPlan.cs`** — Entity with: `Id`, `Name`, `Description`, prices, Stripe IDs, `TrialDays`, `IsFreeTier`, `IsDefault`, `IsPopular`, `Status`, `SortOrder`. Navigation: `Features`, `Subscriptions`.
- **`backend/src/Seed.Domain/Entities/PlanFeature.cs`** — Entity with: `Id`, `PlanId`, `Key`, `Description`, `LimitValue`, `SortOrder`.
- **`backend/src/Seed.Domain/Enums/SubscriptionStatus.cs`** — Enum: `Active`, `Trialing`, `PastDue`, `Canceled`, `Expired`.
- **`backend/src/Seed.Domain/Authorization/AuditActions.cs`** — Already has `SubscriptionCanceled` constant. Needs no new constants for cancel.
- **`backend/src/Seed.Application/Common/Interfaces/IAuditService.cs`** — `LogAsync(action, entityType, entityId?, details?, userId?, ipAddress?, userAgent?, ct)`.
- **`backend/src/Seed.Application/Common/Result.cs`** — `Result<T>` with `Success(T data)` and `Failure(params string[] errors)`.
- **`backend/src/Seed.Application/Billing/Models/PlanDto.cs`** — Existing DTO for plan data.
- **`backend/src/Seed.Application/Billing/Models/PlanFeatureDto.cs`** — Existing DTO for plan features.
- **`backend/src/Seed.Infrastructure/DependencyInjection.cs`** — Line 64-86: `IsPaymentsModuleEnabled()` block where billing handlers are registered manually via `AddScoped<IRequestHandler<...>>`.
- **`backend/src/Seed.Infrastructure/Billing/Queries/GetPlansQueryHandler.cs`** — Example of query handler pattern: `ApplicationDbContext` injected via primary constructor, LINQ projection, returns `Result<T>`.
- **`backend/src/Seed.Infrastructure/Billing/Commands/CreateCheckoutSessionCommandHandler.cs`** — Example of command handler pattern: `ApplicationDbContext`, `UserManager`, `IPaymentGateway`, `IAuditService` injected. Uses InMemory DB lookup for `StripeCustomerId`.
- **`backend/tests/Seed.UnitTests/Billing/Commands/CreateCheckoutSessionCommandHandlerTests.cs`** — Test pattern: `IDisposable`, InMemory DB, `NSubstitute` for `UserManager`/`IPaymentGateway`/`IAuditService`.
- **`backend/tests/Seed.IntegrationTests/Webhooks/WebhookWebApplicationFactory.cs`** — Factory with payments module enabled, used by billing integration tests.

### Pattern già in uso

- **Query contracts** go in `Seed.Application/Billing/Queries/<QueryName>/` as `sealed record : IRequest<Result<T>>`.
- **Command contracts** go in `Seed.Application/Billing/Commands/<CommandName>/` as `sealed record : IRequest<Result<T>>`.
- **Handler implementations** go in `Seed.Infrastructure/Billing/Queries/` or `Seed.Infrastructure/Billing/Commands/`.
- **DTOs** go in `Seed.Application/Billing/Models/` as `sealed record`.
- **DI registration** is manual inside `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`.
- **Unit tests** use `InMemoryDatabase` for `ApplicationDbContext`, `NSubstitute` for external services. Class implements `IDisposable` to dispose dbContext.
- **Integration tests** use `WebhookWebApplicationFactory` (`IClassFixture<>`).
- **Controller endpoints** enrich commands with `CurrentUserId`, `IpAddress`, `UserAgent` via `command with { ... }` pattern.
- **Audit logging** uses `IAuditService.LogAsync()` with constants from `AuditActions`.

## Piano di esecuzione

### Step 1: Create DTOs

**File:** `backend/src/Seed.Application/Billing/Models/UserSubscriptionDto.cs`
```csharp
public sealed record UserSubscriptionDto(
    Guid Id, string PlanName, string? PlanDescription,
    string Status, decimal MonthlyPrice, decimal YearlyPrice,
    DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
    DateTime? TrialEnd, DateTime? CanceledAt,
    bool IsFreeTier, IReadOnlyList<PlanFeatureDto> Features);
```

**File:** `backend/src/Seed.Application/Billing/Models/PortalSessionResponse.cs`
```csharp
public sealed record PortalSessionResponse(string PortalUrl);
```

### Step 2: Create GetMySubscriptionQuery

**File:** `backend/src/Seed.Application/Billing/Queries/GetMySubscription/GetMySubscriptionQuery.cs`
- `sealed record GetMySubscriptionQuery(Guid UserId) : IRequest<Result<UserSubscriptionDto?>>`

**File:** `backend/src/Seed.Infrastructure/Billing/Queries/GetMySubscriptionQueryHandler.cs`
- Query `UserSubscriptions` where `UserId == request.UserId` and `Status` is `Active` or `Trialing`, ordered by `CreatedAt` descending, take first
- Include `Plan` and `Plan.Features`
- Use LINQ projection to `UserSubscriptionDto` (consistent with GetPlansQueryHandler)
- Return `null` result (not failure) if no subscription found

### Step 3: Create CreatePortalSessionCommand

**File:** `backend/src/Seed.Application/Billing/Commands/CreatePortalSession/CreatePortalSessionCommand.cs`
- `sealed record CreatePortalSessionCommand(string ReturnUrl) : IRequest<Result<PortalSessionResponse>>` with `[JsonIgnore] Guid UserId`, `[JsonIgnore] string? IpAddress`, `[JsonIgnore] string? UserAgent`

**File:** `backend/src/Seed.Infrastructure/Billing/Commands/CreatePortalSessionCommandHandler.cs`
- Look up user's latest `StripeCustomerId` from `UserSubscriptions` (same pattern as checkout handler)
- Return failure if no `StripeCustomerId` found
- Call `IPaymentGateway.CreateCustomerPortalSessionAsync(stripeCustomerId, returnUrl, ct)`
- Return `PortalSessionResponse` with portal URL

### Step 4: Create CancelSubscriptionCommand

**File:** `backend/src/Seed.Application/Billing/Commands/CancelSubscription/CancelSubscriptionCommand.cs`
- `sealed record CancelSubscriptionCommand() : IRequest<Result<bool>>` with `[JsonIgnore] Guid UserId`, `[JsonIgnore] string? IpAddress`, `[JsonIgnore] string? UserAgent`

**File:** `backend/src/Seed.Infrastructure/Billing/Commands/CancelSubscriptionCommandHandler.cs`
- Find active/trialing subscription for user
- Return failure if no active subscription or no `StripeSubscriptionId`
- Call `IPaymentGateway.CancelSubscriptionAsync(stripeSubscriptionId, ct)`
- Set `CanceledAt = DateTime.UtcNow` on `UserSubscription`, save changes
- Log via `IAuditService` with `AuditActions.SubscriptionCanceled`
- Return success

### Step 5: Add endpoints to BillingController

**File:** `backend/src/Seed.Api/Controllers/BillingController.cs` (modify existing)
- Add `GET /subscription` → sends `GetMySubscriptionQuery(CurrentUserId)`, returns `Ok(result.Data)` (can be null for no subscription)
- Add `POST /portal` → sends `CreatePortalSessionCommand` enriched with UserId/IpAddress/UserAgent, returns `Ok(result.Data)` or `BadRequest`
- Add `POST /cancel` → sends `CancelSubscriptionCommand` enriched with UserId/IpAddress/UserAgent, returns `Ok()` or `BadRequest`

### Step 6: Register handlers in DI

**File:** `backend/src/Seed.Infrastructure/DependencyInjection.cs` (modify existing)
- Inside `IsPaymentsModuleEnabled()` block, add:
  - `services.AddScoped<IRequestHandler<GetMySubscriptionQuery, Result<UserSubscriptionDto?>>, GetMySubscriptionQueryHandler>()`
  - `services.AddScoped<IRequestHandler<CreatePortalSessionCommand, Result<PortalSessionResponse>>, CreatePortalSessionCommandHandler>()`
  - `services.AddScoped<IRequestHandler<CancelSubscriptionCommand, Result<bool>>, CancelSubscriptionCommandHandler>()`

### Step 7: Unit tests

**File:** `backend/tests/Seed.UnitTests/Billing/Queries/GetMySubscriptionQueryHandlerTests.cs`
- Test: returns subscription with plan details when active subscription exists
- Test: returns subscription when status is Trialing
- Test: returns null when no subscription exists
- Test: returns null when subscription is Canceled/Expired
- Test: includes plan features in result

**File:** `backend/tests/Seed.UnitTests/Billing/Commands/CreatePortalSessionCommandHandlerTests.cs`
- Test: returns portal URL when StripeCustomerId exists
- Test: returns failure when no StripeCustomerId found
- Test: calls IPaymentGateway.CreateCustomerPortalSessionAsync with correct parameters

**File:** `backend/tests/Seed.UnitTests/Billing/Commands/CancelSubscriptionCommandHandlerTests.cs`
- Test: cancels subscription and sets CanceledAt
- Test: calls IPaymentGateway.CancelSubscriptionAsync
- Test: logs audit action
- Test: returns failure when no active subscription
- Test: returns failure when no StripeSubscriptionId

### Step 8: Integration tests

**File:** `backend/tests/Seed.IntegrationTests/Billing/BillingControllerTests.cs`
- Test: `GET /subscription` returns 200 with subscription data when user has active subscription
- Test: `GET /subscription` returns 200 with null when user has no subscription
- Test: `POST /cancel` returns 200 when active subscription exists
- Test: `POST /portal` returns 200 with portal URL
- All tests use `WebhookWebApplicationFactory`

## Criteri di completamento

- [x] GetMySubscription query returns subscription with plan details
- [x] CreatePortalSession returns valid portal URL
- [x] CancelSubscription sets cancel_at_period_end
- [x] All endpoints created and auth-protected
- [x] Audit log for cancel action
- [x] Unit tests for all handlers
- [x] Integration tests for endpoints

## Risultato

### File creati

- `backend/src/Seed.Application/Billing/Models/UserSubscriptionDto.cs` — DTO per la subscription dell'utente
- `backend/src/Seed.Application/Billing/Models/PortalSessionResponse.cs` — DTO per la risposta del portal session
- `backend/src/Seed.Application/Billing/Queries/GetMySubscription/GetMySubscriptionQuery.cs` — Query contract
- `backend/src/Seed.Infrastructure/Billing/Queries/GetMySubscriptionQueryHandler.cs` — Query handler con LINQ projection
- `backend/src/Seed.Application/Billing/Commands/CreatePortalSession/CreatePortalSessionCommand.cs` — Command contract
- `backend/src/Seed.Infrastructure/Billing/Commands/CreatePortalSessionCommandHandler.cs` — Handler che cerca StripeCustomerId e chiama IPaymentGateway
- `backend/src/Seed.Application/Billing/Commands/CancelSubscription/CancelSubscriptionCommand.cs` — Command contract
- `backend/src/Seed.Infrastructure/Billing/Commands/CancelSubscriptionCommandHandler.cs` — Handler che cancella via Stripe, setta CanceledAt, logga audit
- `backend/tests/Seed.UnitTests/Billing/Queries/GetMySubscriptionQueryHandlerTests.cs` — 5 test (active, trialing, null, canceled/expired, features)
- `backend/tests/Seed.UnitTests/Billing/Commands/CreatePortalSessionCommandHandlerTests.cs` — 3 test (success, no customer, correct params)
- `backend/tests/Seed.UnitTests/Billing/Commands/CancelSubscriptionCommandHandlerTests.cs` — 5 test (cancel+CanceledAt, gateway call, audit, no subscription, no stripeId)
- `backend/tests/Seed.IntegrationTests/Billing/BillingControllerTests.cs` — 4 test (get sub, get null, cancel, portal)

### File modificati

- `backend/src/Seed.Api/Controllers/BillingController.cs` — Aggiunti 3 endpoint: GET /subscription, POST /portal, POST /cancel
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — Registrati 3 nuovi handler nel blocco IsPaymentsModuleEnabled()

### Scelte chiave

- **GetMySubscription** restituisce `null` (non failure) quando non c'è abbonamento, coerente con il pattern "no data ≠ error"
- **CancelSubscription** setta `CanceledAt` e `UpdatedAt` localmente, delegando la cancellazione effettiva a `IPaymentGateway.CancelSubscriptionAsync` (che su Stripe imposta cancel_at_period_end)
- **CreatePortalSession** non richiede audit logging (è solo un redirect a Stripe, nessuna mutazione locale)
- Tutti i pattern seguono le convenzioni esistenti: primary constructor, LINQ projection, InMemory DB per unit test, NSubstitute per servizi esterni

### Deviazioni dal mini-plan

Nessuna deviazione significativa.
