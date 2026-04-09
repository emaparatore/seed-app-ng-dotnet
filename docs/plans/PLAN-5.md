# Implementation Plan: FEAT-3 — Subscription Plans & Payments (Stripe)

**Requirements:** `docs/requirements/FEAT-3.md`
**Status:** In Progress
**Created:** 2026-04-09
**Last Updated:** 2026-04-09

## Story Coverage

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-001 | Visualizzare i piani disponibili | T-07, T-14 | ⏳ Not Started |
| US-002 | Sottoscrivere un piano a pagamento | T-08, T-15 | ⏳ Not Started |
| US-003 | Visualizzare il proprio abbonamento | T-09, T-16 | ⏳ Not Started |
| US-004 | Gestire pagamento e cancellare abbonamento | T-09, T-16 | ⏳ Not Started |
| US-005 | Upgrade/downgrade del piano | T-09, T-16 | ⏳ Not Started |
| US-006 | Trial period | T-08, T-15 | ⏳ Not Started |
| US-007 | Admin — CRUD piani | T-10, T-17 | ⏳ Not Started |
| US-008 | Admin — dashboard abbonamenti | T-11, T-18 | ⏳ Not Started |
| US-009 | Webhook processing | T-06 | ⏳ Not Started |
| US-010 | Subscription guard su endpoint | T-12 | ⏳ Not Started |
| US-011 | Feature gating frontend | T-13 | ⏳ Not Started |
| US-012 | Richiesta fattura manuale | T-19, T-20 | ⏳ Not Started |

---

## Phase 1 — Backend Infrastructure

### T-01: Module toggle system and Stripe configuration

**Stories:** Trasversale (DA-1, RNF-3)
**Size:** Small
**Status:** [x] Done

**What to do:**
Create the module toggle and Stripe configuration infrastructure:

1. Add `ModulesSettings` and `PaymentsModuleSettings` POCOs in `Seed.Shared/Configuration/` with `Enabled` (bool) and `Provider` (string) properties.
2. Add `StripeSettings` POCO in `Seed.Shared/Configuration/` with `SecretKey`, `PublishableKey`, `WebhookSecret`.
3. Add the configuration sections to `appsettings.json` (disabled by default) and `appsettings.Development.json` (with placeholder keys).
4. Register `services.Configure<>()` calls in `DependencyInjection.cs`.
5. Add a helper method or extension `IsPaymentsModuleEnabled(IConfiguration)` for conditional registration.

**Definition of Done:**
- [x] `PaymentsModuleSettings`, `ModulesSettings`, and `StripeSettings` POCOs exist with correct section names
- [x] `appsettings.json` contains `Modules:Payments` section (disabled) and `Stripe` section (empty keys)
- [x] `appsettings.Development.json` contains `Stripe` section with test placeholder keys
- [x] Settings registered in DI (`ModulesSettings` always, `StripeSettings` conditionally)
- [x] Extension method `IsPaymentsModuleEnabled` implemented with 3 unit tests
- [x] Solution builds and all tests pass

**Implementation Notes:**
- Added `Microsoft.Extensions.Configuration.Binder` (v10.0.3) to `Seed.Shared.csproj` to support `IConfiguration.GetValue<T>` in the extension method
- `StripeSettings` registration follows the same conditional pattern as the existing SMTP block in `DependencyInjection.cs`
- `ModulesSettings` is registered unconditionally to allow injection even when the payments module is disabled
- Extension method `IsPaymentsModuleEnabled` lives in `Seed.Shared/Extensions/ConfigurationExtensions.cs`
- 3 unit tests cover enabled, disabled, and missing-section scenarios

---

### T-02: Domain entities — Plan, Subscription, InvoiceRequest

**Stories:** US-001, US-002, US-003, US-007, US-012 (RF-1, RF-2, RF-9)
**Size:** Medium
**Status:** [ ] Not Started

**What to do:**
Create the domain entities in `Seed.Domain/Entities/`:

1. **`SubscriptionPlan`** — Id (Guid), Name, Description, MonthlyPrice (decimal), YearlyPrice (decimal), StripePriceIdMonthly, StripePriceIdYearly, StripeProductId, TrialDays (int), IsFreeTier (bool), IsDefault (bool), IsPopular (bool), Status (enum: Active/Inactive/Archived), SortOrder (int), CreatedAt, UpdatedAt.
2. **`PlanFeature`** — Id (Guid), PlanId (FK), Key (string, e.g. "api-access"), Description (string), LimitValue (string?, e.g. "10" for "max 10 projects"), SortOrder (int). Relates to `SubscriptionPlan`.
3. **`UserSubscription`** — Id (Guid), UserId (FK → ApplicationUser), PlanId (FK → SubscriptionPlan), Status (enum: Active/Trialing/PastDue/Canceled/Expired), StripeSubscriptionId, StripeCustomerId, CurrentPeriodStart, CurrentPeriodEnd, TrialEnd (DateTime?), CanceledAt, CreatedAt, UpdatedAt.
4. **`InvoiceRequest`** — Id (Guid), UserId (FK), StripePaymentIntentId, CustomerType (enum: Individual/Company), FullName, CompanyName, Address, City, PostalCode, Country, FiscalCode, VatNumber, SdiCode, PecEmail, Status (enum: Requested/InProgress/Issued), CreatedAt, UpdatedAt, ProcessedAt.
5. **`SubscriptionStatus`** enum and **`PlanStatus`** enum in `Seed.Domain/Enums/`.
6. Add navigation property `ICollection<UserSubscription> Subscriptions` to `ApplicationUser`.

**Definition of Done:**
- [ ] All entities created with proper types and nullable annotations
- [ ] Enums created for Status fields
- [ ] Navigation properties set correctly
- [ ] Solution builds successfully
- [ ] Unit tests for any domain validation logic

---

### T-03: EF Core configuration and migration

**Stories:** Trasversale
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-02

**What to do:**
1. Create EF Core entity configurations in `Seed.Infrastructure/Persistence/Configurations/`:
   - `SubscriptionPlanConfiguration` — table name, indexes on Status/IsDefault, proper decimal precision for prices.
   - `PlanFeatureConfiguration` — composite unique index on (PlanId, Key).
   - `UserSubscriptionConfiguration` — index on UserId + Status, index on StripeSubscriptionId (unique), foreign keys with appropriate delete behavior.
   - `InvoiceRequestConfiguration` — index on UserId, index on Status.
2. Add `DbSet<>` properties to `ApplicationDbContext`.
3. Generate migration: `dotnet ef migrations add AddSubscriptionPayments`.
4. Verify migration compiles and applies correctly.

**Definition of Done:**
- [ ] All entity configurations created with proper constraints and indexes
- [ ] DbSets added to ApplicationDbContext
- [ ] Migration generated and verified
- [ ] `dotnet ef database update` runs without errors
- [ ] Solution builds successfully

---

### T-04: IPaymentGateway interface and MockPaymentGateway

**Stories:** Trasversale (DA-2)
**Size:** Medium
**Status:** [ ] Not Started

**What to do:**
Create the payment gateway abstraction in `Seed.Application/Common/Interfaces/`:

1. **`IPaymentGateway`** interface with methods:
   - `Task<string> CreateCustomerAsync(string email, string name, CancellationToken ct)` — returns Stripe Customer ID
   - `Task<string> CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct)` — returns checkout URL
   - `Task<string> CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)` — returns portal URL
   - `Task CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)`
   - `Task<SubscriptionDetails?> GetSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)`
   - `Task<ProductSyncResult> SyncPlanToProviderAsync(SyncPlanRequest request, CancellationToken ct)` — returns product/price IDs

2. Create request/response DTOs for the gateway methods in `Seed.Application/Common/Models/` (e.g. `CreateCheckoutRequest`, `SubscriptionDetails`, `SyncPlanRequest`, `ProductSyncResult`).

3. **`MockPaymentGateway`** in `Seed.Infrastructure/Services/Payments/` that returns deterministic fake data (fake customer IDs, fake URLs, etc.) for dev/testing.

4. Register `MockPaymentGateway` conditionally in DI: when `Modules:Payments:Enabled = true` but no real provider is configured or for testing.

**Definition of Done:**
- [ ] `IPaymentGateway` interface defined with all methods
- [ ] Gateway DTOs created
- [ ] `MockPaymentGateway` implemented with fake data
- [ ] Conditional DI registration working
- [ ] Unit tests for MockPaymentGateway
- [ ] Solution builds successfully

---

### T-05: StripePaymentGateway implementation

**Stories:** Trasversale (DA-2, DA-4, DA-5)
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-01, T-04

**What to do:**
1. Add `Stripe.net` NuGet package to `Seed.Infrastructure`.
2. Create `StripePaymentGateway` in `Seed.Infrastructure/Services/Payments/` implementing `IPaymentGateway`:
   - `CreateCustomerAsync` → `CustomerService.CreateAsync()`
   - `CreateCheckoutSessionAsync` → `SessionService.CreateAsync()` with mode=subscription, success/cancel URLs, customer email, price ID, trial period
   - `CreateCustomerPortalSessionAsync` → `Stripe.BillingPortal.SessionService.CreateAsync()`
   - `CancelSubscriptionAsync` → `SubscriptionService.CancelAsync()` with `cancel_at_period_end = true`
   - `GetSubscriptionAsync` → `SubscriptionService.GetAsync()`
   - `SyncPlanToProviderAsync` → `ProductService.CreateAsync/UpdateAsync()` + `PriceService.CreateAsync()`
3. Register conditionally: when `Modules:Payments:Enabled = true` AND `Provider = "Stripe"` AND `Stripe:SecretKey` is non-empty.
4. Configure `StripeConfiguration.ApiKey` from settings.

**Definition of Done:**
- [ ] `Stripe.net` package added
- [ ] `StripePaymentGateway` fully implemented
- [ ] Conditional DI registration (Stripe vs Mock vs disabled)
- [ ] Solution builds successfully
- [ ] Integration test with MockPaymentGateway verifying DI wiring

---

### T-06: Webhook handler endpoint and event processing

**Stories:** US-009 (RF-5, RNF-1, RNF-2)
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-02, T-03, T-05

**What to do:**
1. Create `StripeWebhookController` in `Seed.Api/Controllers/` at route `POST /webhooks/stripe`:
   - No `[Authorize]` attribute — public endpoint
   - Read raw request body
   - Validate Stripe signature using `EventUtility.ConstructEvent()` with webhook secret
   - Return 400 if signature invalid
   - Parse event type and dispatch to handler

2. Create `IWebhookEventHandler` interface and `StripeWebhookEventHandler` in `Seed.Application/` or `Seed.Infrastructure/`:
   - `checkout.session.completed` → find user by email/metadata, create or update `UserSubscription`, set status Active
   - `invoice.payment_succeeded` → update subscription period dates
   - `invoice.payment_failed` → set subscription status to PastDue
   - `customer.subscription.updated` → update plan, status, period dates
   - `customer.subscription.deleted` → set status to Canceled/Expired
   - `customer.subscription.trial_will_end` → (log + optional email notification)

3. Implement idempotency: store processed event IDs (in-memory cache or DB table) and skip duplicates.
4. Log all received events via `IAuditService` or structured logging.
5. Add `Subscription` audit actions to `AuditActions.cs`.
6. The webhook controller should only be registered when payments module is enabled.

**Definition of Done:**
- [ ] Webhook endpoint created at `/webhooks/stripe`
- [ ] Stripe signature validation working
- [ ] All 6 event types handled correctly
- [ ] Idempotency implemented (duplicate events ignored)
- [ ] Events logged
- [ ] HTTP 200 returned for unknown event types
- [ ] HTTP 400 returned for invalid signatures
- [ ] Unit tests for event handler logic (using mock data)
- [ ] Integration test for webhook endpoint (signature validation, event dispatch)

---

## Phase 2 — Backend Business Logic

### T-07: Public plans API — list available plans

**Stories:** US-001 (RF-1)
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-02, T-03

**What to do:**
1. Create `GetPlansQuery` and `GetPlansQueryHandler` in `Seed.Application/Billing/Queries/GetPlans/`:
   - Returns all plans with Status = Active, ordered by SortOrder
   - Includes PlanFeatures
   - No auth required
2. Create DTOs: `PlanDto`, `PlanFeatureDto`.
3. Create `PlansController` in `Seed.Api/Controllers/` at `api/v1.0/plans`:
   - `[AllowAnonymous] GET /` — returns list of active plans with features
4. Controller only registered when payments module is enabled.

**Definition of Done:**
- [ ] Query + Handler created following existing patterns
- [ ] DTOs created as sealed records
- [ ] Controller with AllowAnonymous GET endpoint
- [ ] Endpoint returns plans with features, sorted correctly
- [ ] Unit test for handler
- [ ] Integration test for endpoint

---

### T-08: Checkout flow — create checkout session

**Stories:** US-002, US-006 (RF-3, RF-8)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-04, T-05, T-07

**What to do:**
1. Create `CreateCheckoutSessionCommand` and handler in `Seed.Application/Billing/Commands/CreateCheckoutSession/`:
   - Input: PlanId, BillingInterval (Monthly/Yearly), UserId
   - Validate plan exists and is active
   - If user doesn't have a StripeCustomerId → call `IPaymentGateway.CreateCustomerAsync()`
   - Store StripeCustomerId on UserSubscription (or a new field on User)
   - Call `IPaymentGateway.CreateCheckoutSessionAsync()` with the correct Price ID (monthly/yearly), customer ID, trial days if applicable
   - Return checkout session URL
2. Create `CreateCheckoutSessionValidator` — validate PlanId not empty, BillingInterval valid.
3. Add endpoint to `BillingController`: `POST api/v1.0/billing/checkout` (requires auth).
4. Add audit log entry.

**Definition of Done:**
- [ ] Command + Handler + Validator created
- [ ] Stripe customer creation for first-time users
- [ ] Correct price ID selected based on billing interval
- [ ] Trial period included when plan has TrialDays > 0
- [ ] Returns checkout URL
- [ ] Audit log entry on checkout session creation
- [ ] Unit tests for handler (with MockPaymentGateway)
- [ ] Validator tests

---

### T-09: Subscription management — portal, view, cancel

**Stories:** US-003, US-004, US-005 (RF-4)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-06, T-08

**What to do:**
1. Create `GetMySubscriptionQuery` and handler in `Seed.Application/Billing/Queries/GetMySubscription/`:
   - Returns current active/trialing subscription for UserId
   - Includes plan details and features
   - Returns null/empty DTO if no subscription (or free tier info)
2. Create `CreatePortalSessionCommand` and handler in `Seed.Application/Billing/Commands/CreatePortalSession/`:
   - Input: UserId, ReturnUrl
   - Gets user's StripeCustomerId
   - Calls `IPaymentGateway.CreateCustomerPortalSessionAsync()`
   - Returns portal URL
3. Create `CancelSubscriptionCommand` and handler:
   - Sets cancel_at_period_end via gateway
   - Audit log entry
4. Add endpoints to `BillingController`:
   - `GET api/v1.0/billing/subscription` — current subscription
   - `POST api/v1.0/billing/portal` — get portal URL
   - `POST api/v1.0/billing/cancel` — cancel subscription
5. Create DTOs: `UserSubscriptionDto`, `PortalSessionDto`.

**Definition of Done:**
- [ ] GetMySubscription query returns subscription with plan details
- [ ] CreatePortalSession returns valid portal URL
- [ ] CancelSubscription sets cancel_at_period_end
- [ ] All endpoints created and auth-protected
- [ ] Audit log for cancel action
- [ ] Unit tests for all handlers
- [ ] Integration tests for endpoints

---

### T-10: Admin CRUD plans

**Stories:** US-007 (RF-7)
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-02, T-03, T-05

**What to do:**
1. Create commands and handlers in `Seed.Application/Admin/Plans/`:
   - `CreatePlanCommand` — creates plan + features, syncs to Stripe via `IPaymentGateway.SyncPlanToProviderAsync()`, stores returned Product/Price IDs
   - `UpdatePlanCommand` — updates plan metadata and features, syncs to Stripe (new Price if price changed)
   - `ArchivePlanCommand` — sets Status = Archived
   - `GetPlansQuery` (admin version) — returns all plans (including inactive/archived) with subscriber counts
   - `GetPlanByIdQuery` — returns single plan with full details
2. Create validators for create/update commands.
3. Create `AdminPlansController` at `api/v1.0/admin/plans`:
   - `GET /` — list all plans with subscriber counts (requires `Plans.Read`)
   - `GET /{id}` — plan detail (requires `Plans.Read`)
   - `POST /` — create plan (requires `Plans.Create`)
   - `PUT /{id}` — update plan (requires `Plans.Update`)
   - `POST /{id}/archive` — archive plan (requires `Plans.Update`)
4. Add `Plans` permissions to `Permissions.cs` (Read, Create, Update).
5. Add audit log actions for plan CRUD.

**Definition of Done:**
- [ ] Create, Update, Archive commands with handlers and validators
- [ ] Admin list query with subscriber counts
- [ ] Admin detail query
- [ ] Stripe sync on create/update
- [ ] Plans permissions added and registered in seeder
- [ ] Audit log for all mutations
- [ ] Controller with proper permission attributes
- [ ] Unit tests for all handlers
- [ ] Validator tests

---

### T-11: Admin subscriptions dashboard API

**Stories:** US-008 (RF-7)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03, T-06

**What to do:**
1. Create queries in `Seed.Application/Admin/Subscriptions/`:
   - `GetSubscriptionMetricsQuery` — returns MRR (sum of active monthly-equivalent prices), active count, trialing count, churn rate (canceled in last 30 days / total)
   - `GetSubscriptionsListQuery` — paginated list of subscriptions with filters (planId, status), includes user email and plan name
   - `GetSubscriptionDetailQuery` — single subscription with full details
2. Create DTOs: `SubscriptionMetricsDto`, `AdminSubscriptionDto`, `AdminSubscriptionDetailDto`.
3. Create `AdminSubscriptionsController` at `api/v1.0/admin/subscriptions`:
   - `GET /metrics` — aggregate metrics (requires `Subscriptions.Read`)
   - `GET /` — paginated list (requires `Subscriptions.Read`)
   - `GET /{id}` — detail (requires `Subscriptions.Read`)
4. Add `Subscriptions` permissions to `Permissions.cs` (Read).

**Definition of Done:**
- [ ] Metrics query calculates MRR, active count, trialing count, churn
- [ ] List query with pagination and filtering
- [ ] Detail query
- [ ] Subscriptions permissions added
- [ ] Controller with proper permission attributes
- [ ] Unit tests for metric calculations
- [ ] Integration test for list endpoint with filters

---

### T-12: Subscription guards — backend authorization

**Stories:** US-010 (RF-6)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03

**What to do:**
1. Create `RequiresPlanAttribute` — accepts plan names, e.g. `[RequiresPlan("Pro", "Enterprise")]`.
2. Create `RequiresFeatureAttribute` — accepts feature key, e.g. `[RequiresFeature("api-access")]`.
3. Create `RequiresPlanAuthorizationHandler` as an `IAuthorizationHandler`:
   - Check if payments module is disabled → pass always
   - Check user's active subscription plan name against required plans
   - Check subscription status is Active or Trialing
   - Return 403 with clear message if not authorized
4. Create `RequiresFeatureAuthorizationHandler`:
   - Check if payments module is disabled → pass always
   - Check user's plan features against required feature key
5. Register authorization policies and handlers in DI.
6. Add `ISubscriptionAccessService` in Application layer to encapsulate the check logic (query DB for user's subscription + plan features).

**Definition of Done:**
- [ ] `[RequiresPlan]` attribute works on controllers/actions
- [ ] `[RequiresFeature]` attribute works on controllers/actions
- [ ] Guards pass always when module is disabled
- [ ] Guards check subscription status (active/trialing only)
- [ ] HTTP 403 returned with descriptive message
- [ ] Unit tests for authorization handlers (module enabled vs disabled, various subscription states)

---

### T-13: Include subscription info in auth/me response

**Stories:** US-011 (RF-2)
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-03

**What to do:**
1. Extend `MeResponse` with optional subscription fields: `CurrentPlan` (name), `PlanFeatures` (list of feature keys), `SubscriptionStatus`, `TrialEndsAt`.
2. Update `GetMeQueryHandler` to load user's active subscription with plan and features when payments module is enabled.
3. When payments module is disabled, return null for subscription fields (frontend treats as "all features available").
4. Update `AuthResponse` / `UserDto` similarly if needed.

**Definition of Done:**
- [ ] `MeResponse` includes subscription data
- [ ] Handler loads subscription conditionally (only when module enabled)
- [ ] Returns null/empty when module disabled or no subscription
- [ ] Existing auth tests still pass
- [ ] Unit test for handler with subscription data

---

## Phase 3 — Frontend

### T-14: Frontend — Pricing page

**Stories:** US-001
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-07

**What to do:**
1. Create `PricingComponent` at `frontend/web/projects/app/src/app/pages/pricing/`.
2. Create `BillingService` in the same folder with methods to call backend APIs.
3. Create `billing.models.ts` with TypeScript interfaces mirroring backend DTOs.
4. Implement the pricing page:
   - Fetch plans from `GET /api/v1.0/plans`
   - Monthly/Yearly toggle
   - Plan cards with name, description, price, feature list (check/cross icons)
   - "Most Popular" badge
   - CTA buttons: "Inizia gratis" (free tier → register), "Scegli piano" (paid → checkout or login)
5. Add route `/pricing` in `app.routes.ts` — no auth guard.
6. Use Angular Material cards, toggle buttons, icons.

**Definition of Done:**
- [ ] Pricing page renders all active plans
- [ ] Monthly/yearly price toggle works
- [ ] Popular plan highlighted
- [ ] Free tier CTA links to registration
- [ ] Paid plan CTA redirects to login if not authenticated
- [ ] Route accessible without authentication
- [ ] Responsive layout (mobile-friendly)

---

### T-15: Frontend — Checkout flow

**Stories:** US-002, US-006
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-08, T-14

**What to do:**
1. Add `createCheckoutSession(planId, billingInterval)` to `BillingService`.
2. When authenticated user clicks "Scegli piano" → call checkout API → redirect to Stripe Checkout URL.
3. Create `CheckoutSuccessComponent` — simple confirmation page at `/billing/success` showing "Abbonamento attivato!" with link to subscription page.
4. Create `CheckoutCancelComponent` — page at `/billing/cancel` showing "Checkout annullato" with link back to pricing.
5. Add routes with `authGuard`.

**Definition of Done:**
- [ ] Clicking plan CTA creates checkout session and redirects to Stripe
- [ ] Success page rendered after successful checkout
- [ ] Cancel page rendered when user cancels checkout
- [ ] Trial CTA shows "Prova gratis per X giorni" when plan has trial
- [ ] Routes protected by authGuard

---

### T-16: Frontend — Subscription management section

**Stories:** US-003, US-004, US-005
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-09, T-15

**What to do:**
1. Create `SubscriptionComponent` in the profile area or as standalone page at `/billing/subscription`.
2. Add methods to `BillingService`: `getMySubscription()`, `createPortalSession()`, `cancelSubscription()`.
3. Implement the subscription view:
   - Show current plan name, status, price, next renewal date
   - If no paid plan → show free tier info + "Upgrade" CTA linking to pricing
   - If trialing → show trial end date and days remaining
   - "Gestisci pagamento" button → redirect to Stripe Customer Portal
   - "Cambia piano" button → navigate to pricing page (with current plan highlighted)
   - "Cancella abbonamento" → confirmation dialog → call cancel API
4. Add route(s) with `authGuard`.

**Definition of Done:**
- [ ] Subscription details displayed correctly for all states (active, trialing, canceled, free)
- [ ] Portal redirect works
- [ ] Cancel flow with confirmation dialog
- [ ] Upgrade/change plan navigates to pricing
- [ ] Trial days remaining shown when applicable
- [ ] Link accessible from profile/navigation

---

### T-17: Frontend — Admin plans management

**Stories:** US-007
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-10

**What to do:**
1. Create `AdminPlansComponent` (list) and `AdminPlanEditComponent` (create/edit dialog) in `frontend/web/projects/app/src/app/pages/admin/plans/`.
2. Create `AdminBillingService` with admin API calls.
3. Implement:
   - Plans list table with columns: Name, Price (monthly/yearly), Status, Subscribers count, Actions
   - Create plan dialog: name, description, monthly price, yearly price, trial days, features list (dynamic add/remove), is popular, is free tier, sort order
   - Edit plan dialog: same fields, warning about price changes creating new Stripe Price
   - Archive action with confirmation
4. Add admin route `/admin/plans` with `permissionGuard('Plans.Read')`.
5. Add "Piani" to admin sidebar navigation.

**Definition of Done:**
- [ ] Plans list with all columns rendered
- [ ] Create plan dialog with all fields + dynamic feature list
- [ ] Edit plan dialog
- [ ] Archive with confirmation
- [ ] Subscriber count shown per plan
- [ ] Permission-gated route and navigation item
- [ ] Form validation

---

### T-18: Frontend — Admin subscriptions dashboard

**Stories:** US-008
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-11, T-17

**What to do:**
1. Create `AdminSubscriptionsComponent` in `frontend/web/projects/app/src/app/pages/admin/subscriptions/`.
2. Implement:
   - Metrics cards: MRR, Active subscriptions, Trialing, Churn rate
   - Subscriptions list table with filters (plan, status) and pagination
   - Columns: User email, Plan, Status, Start date, Renewal date
   - Click row → detail view (inline or dialog) with subscription history
3. Add admin route `/admin/subscriptions` with `permissionGuard('Subscriptions.Read')`.
4. Add "Abbonamenti" to admin sidebar navigation.

**Definition of Done:**
- [ ] Metrics cards displayed with correct values
- [ ] Subscriptions list with filtering and pagination
- [ ] Detail view accessible
- [ ] Permission-gated route and navigation item

---

### T-13b: Frontend — Feature gating (directive, guard, service)

**Stories:** US-011
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-13

**What to do:**
1. Create `SubscriptionService` in `shared-auth` or `shared-core`:
   - Exposes `currentPlan` signal, `planFeatures` signal (from auth/me response)
   - Method `hasPlan(planName): boolean`
   - Method `hasFeature(featureKey): boolean`
   - When payments module is disabled (no subscription data in me response), all checks return true
2. Create `RequiresPlanDirective` (`*requiresPlan="'Pro'"`) for conditional rendering.
3. Create `requiresPlanGuard` Angular route guard → redirects to `/pricing` if plan doesn't match.
4. Export from library's `public-api.ts`.

**Definition of Done:**
- [ ] `SubscriptionService` exposes plan signals from auth/me data
- [ ] `*requiresPlan` directive conditionally renders elements
- [ ] `requiresPlanGuard` protects routes
- [ ] All checks pass when payments module is disabled
- [ ] Unit tests for service and directive

---

## Phase 4 — Invoice Request & GDPR Integration

### T-19: Backend — Invoice request CRUD

**Stories:** US-012 (RF-9)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03, T-09

**What to do:**
1. Create commands/queries in `Seed.Application/Billing/`:
   - `CreateInvoiceRequestCommand` — saves invoice request with fiscal data, links to Stripe payment, notifies admin via audit log
   - `GetMyInvoiceRequestsQuery` — returns user's invoice request history
   - `GetInvoiceRequestsQuery` (admin) — paginated list with status filter
   - `UpdateInvoiceRequestStatusCommand` (admin) — update status (Requested → InProgress → Issued)
2. Create validators for the create command.
3. Add endpoints:
   - `POST api/v1.0/billing/invoice-request` — create (user, requires auth)
   - `GET api/v1.0/billing/invoice-requests` — my requests (user, requires auth)
   - `GET api/v1.0/admin/invoice-requests` — all requests (admin, requires `Subscriptions.Read`)
   - `PUT api/v1.0/admin/invoice-requests/{id}/status` — update status (admin)

**Definition of Done:**
- [ ] Create invoice request with all fiscal fields
- [ ] User can view own invoice request history
- [ ] Admin can list and update request status
- [ ] Audit log on create and status change
- [ ] Validator tests
- [ ] Unit tests for handlers

---

### T-20: Frontend — Invoice request UI

**Stories:** US-012
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-16, T-19

**What to do:**
1. Add "Richiedi fattura" button in the subscription management section.
2. Create invoice request dialog/form:
   - Customer type toggle: Persona fisica / Azienda
   - Fields: nome/ragione sociale, indirizzo completo, CF, P.IVA (conditional on type), SDI, PEC
   - Save fiscal data to profile for reuse (pre-fill on subsequent requests)
3. Create invoice request history view — table with status badges (Richiesta, In lavorazione, Emessa).
4. Admin side: add invoice requests list in admin area with status update capability.

**Definition of Done:**
- [ ] Invoice request form with all fields and conditional rendering
- [ ] Fiscal data pre-filled from previous requests
- [ ] Request history visible to user
- [ ] Admin can view and update request status
- [ ] Form validation

---

## Phase 5 — GDPR Integration & Finalization

### T-21: GDPR integration — subscription cleanup on account deletion

**Stories:** Trasversale (DA, Open Question 4)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-05, T-06

**What to do:**
1. Extend `IUserPurgeService` / user deletion flow to handle subscriptions:
   - Cancel active Stripe subscription via `IPaymentGateway.CancelSubscriptionAsync()`
   - Delete Stripe Customer via Stripe API (add method to `IPaymentGateway`)
   - Anonymize `UserSubscription` records (remove UserId link, keep aggregate data)
   - Keep `InvoiceRequest` records for 10 years but anonymize personal data (replace name/address with "ANONYMIZED", keep fiscal codes for legal compliance)
2. Ensure this only runs when payments module is enabled.
3. Update `DataCleanupService` if needed for retention policies.

**Definition of Done:**
- [ ] Account deletion cancels Stripe subscription
- [ ] Account deletion deletes Stripe Customer
- [ ] UserSubscription anonymized (not deleted)
- [ ] InvoiceRequest kept 10 years with anonymized personal data
- [ ] No-op when payments module disabled
- [ ] Unit tests for purge logic

---

### T-22: Permissions seeder and Bootstrap update

**Stories:** Trasversale
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-10, T-11
🔒 **INTERACTIVE ONLY** — modifies seeder that affects production data

**What to do:**
1. Add `Plans.Read`, `Plans.Create`, `Plans.Update`, `Subscriptions.Read` to `Permissions.cs` and the permissions array.
2. Update `RolesAndPermissionsSeeder` to seed the new permissions.
3. Update `PermissionSeeder` in Bootstrap if separate.
4. Add the new permissions to the frontend `PERMISSIONS` constant in `shared-auth`.

**Definition of Done:**
- [ ] New permissions in `Permissions.cs` and `GetAll()` array
- [ ] Seeder updated to create new permissions
- [ ] Frontend permissions constant updated
- [ ] Bootstrap runs without errors
- [ ] Existing permissions unchanged

---

### T-23: Conditional module registration — routes and middleware

**Stories:** Trasversale (DA-1)
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-01

**What to do:**
1. Create an extension method `AddPaymentsModule(this IServiceCollection, IConfiguration)` that registers all payments-related services, controllers, and authorization handlers only when enabled.
2. Ensure billing/webhook controllers are not registered when module is disabled.
3. Verify that when `Modules:Payments:Enabled = false`:
   - No payment routes appear in Swagger
   - Subscription guards pass always
   - No Stripe SDK is initialized
4. Frontend: add a config flag (from `/auth/me` or a dedicated `/config` endpoint) indicating if payments module is active, to conditionally show/hide navigation items and routes.

**Definition of Done:**
- [ ] All payment services/controllers conditionally registered
- [ ] Module disabled → no payment routes, guards pass always
- [ ] Module enabled → full functionality
- [ ] Frontend hides payment UI when module disabled
- [ ] Integration test verifying disabled module behavior

---

### T-24: Email notifications for subscription events

**Stories:** US-006, Trasversale
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-06

**What to do:**
1. Add methods to `IEmailService`:
   - `SendSubscriptionConfirmationAsync(email, planName)` — after successful checkout
   - `SendTrialEndingNotificationAsync(email, planName, daysRemaining)` — before trial expiry
   - `SendSubscriptionCanceledAsync(email, planName, endDate)` — after cancellation
2. Implement in both `SmtpEmailService` and `ConsoleEmailService`.
3. Call from webhook event handlers where appropriate.

**Definition of Done:**
- [ ] Three email methods added to interface and both implementations
- [ ] Webhook handlers send emails on relevant events
- [ ] Console fallback logs email content
- [ ] Unit tests for email service methods

---

### T-25: Documentation — setup guide, Stripe configuration, testing

**Stories:** Trasversale
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-23, T-24 (all tasks complete)

**What to do:**
Create `docs/subscription-payments.md` covering:

1. **Overview del modulo** — cosa fa, architettura (IPaymentGateway, webhook flow, module toggle), diagramma del flusso checkout → webhook → subscription.

2. **Come attivare il modulo:**
   - Configurazione `appsettings.json`: sezione `Modules:Payments` e `Stripe`
   - Elenco di tutti i parametri con valori di default e descrizione

3. **Setup Stripe:**
   - Creare un account Stripe e ottenere le API keys (test e live)
   - Configurare il Customer Portal da Stripe Dashboard (quali opzioni abilitare)
   - Creare il webhook endpoint su Stripe Dashboard (eventi da sottoscrivere, signing secret)
   - Abilitare Stripe Tax (opzionale, fuori scope iniziale)

4. **Sviluppo locale:**
   - Configurare `appsettings.Development.json` con test keys
   - Installare Stripe CLI: `stripe login`
   - Forward webhook in locale: `stripe listen --forward-to localhost:5000/webhooks/stripe`
   - Carte di test: `4242 4242 4242 4242` (successo), `4000 0000 0000 0341` (fallimento), `4000 0025 0000 3155` (3D Secure)
   - Come triggerare eventi manualmente: `stripe trigger checkout.session.completed`
   - MockPaymentGateway: quando si attiva, come usarlo per sviluppo FE senza Stripe

5. **Ambiente staging/produzione:**
   - Switch da test keys a live keys
   - Configurare webhook endpoint con URL pubblico
   - Verificare che il webhook signing secret sia quello di produzione
   - Checklist pre-go-live: piani creati, free tier configurato, Customer Portal configurato, webhook attivo e verificato
   - Monitoraggio: come verificare che i webhook arrivino (Stripe Dashboard → Webhooks → logs)

6. **Troubleshooting:**
   - Webhook non ricevuti (firewall, URL sbagliato, signing secret errato)
   - Subscription non si aggiorna (event ID duplicato, errore nel handler)
   - Customer Portal non si apre (Customer non creato, Portal non configurato)
   - Checkout fallisce (Price ID non valido, piano archiviato)

7. **Come aggiungere un nuovo piano:**
   - Via admin UI
   - Cosa succede su Stripe (Product + Price creati)
   - Come archiviare un piano (gli utenti esistenti restano)

8. **Come proteggere un endpoint con plan/feature guard:**
   - Backend: `[RequiresPlan("Pro")]`, `[RequiresFeature("api-access")]`
   - Frontend: `*requiresPlan="'Pro'"`, `requiresPlanGuard`
   - Comportamento quando il modulo è disabilitato

Aggiornare anche:
- `CLAUDE.md` — aggiungere `docs/subscription-payments.md` alla lista dei docs esistenti
- `README.md` — aggiungere il doc alla tabella indice (se presente)

**Definition of Done:**
- [ ] `docs/subscription-payments.md` creato con tutte le sezioni
- [ ] Istruzioni Stripe verificabili (link a docs ufficiali dove appropriato)
- [ ] Sezione sviluppo locale testabile passo-passo
- [ ] Checklist staging/produzione completa
- [ ] Troubleshooting con almeno 4 scenari comuni
- [ ] `CLAUDE.md` aggiornato con riferimento al nuovo doc
- [ ] Un developer senza contesto può attivare il modulo seguendo solo il doc

---

## Dependency Graph

```
T-01 (config) ──────────────────┐
T-02 (entities) ───┐            │
                    ├── T-03 (migration) ──┐
T-04 (interface) ──┤                       │
                    ├── T-05 (Stripe impl) ┤
                    │                       ├── T-06 (webhooks)
                    │                       ├── T-07 (plans API) ── T-14 (pricing page)
                    │                       │                               │
                    │                       ├── T-08 (checkout) ── T-15 (checkout FE)
                    │                       │                               │
                    │                       ├── T-09 (subscription mgmt) ── T-16 (subscription FE)
                    │                       │                                       │
                    │                       ├── T-10 (admin plans) ── T-17 (admin plans FE)
                    │                       │                                       │
                    │                       ├── T-11 (admin dashboard) ── T-18 (admin dash FE)
                    │                       │
                    │                       ├── T-12 (guards BE)
                    │                       ├── T-13 (auth/me) ── T-13b (guards FE)
                    │                       │
                    │                       ├── T-19 (invoice BE) ── T-20 (invoice FE)
                    │                       │
                    │                       ├── T-21 (GDPR)
                    │                       ├── T-22 (permissions seeder)
                    │                       ├── T-23 (module toggle) ─┐
                    │                       └── T-24 (emails) ────────┴── T-25 (documentation)
```

## Notes

- **Stripe Test Mode:** During development, use Stripe test mode keys. All checkout/webhook flows can be tested with Stripe test cards (e.g., `4242 4242 4242 4242`).
- **Webhook local testing:** Use `stripe listen --forward-to localhost:5000/webhooks/stripe` to forward test events locally.
- **Free tier:** A default free plan must be seeded when the module is enabled. Consider adding this to the Bootstrap seeder.
- **Price immutability:** Stripe Prices are immutable. Changing a plan's price requires creating a new Stripe Price and archiving the old one. The `UpdatePlan` handler must handle this.
