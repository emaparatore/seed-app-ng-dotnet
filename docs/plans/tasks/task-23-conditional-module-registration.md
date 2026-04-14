# Task 23: Conditional module registration — routes and middleware

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| (Trasversale) | DA-1: Modulo attivabile via configurazione | T-01, T-23 | In Progress |

### Dipendenze (da 'Depends on:')
T-01: Module toggle system and Stripe configuration -
**Implementation Notes:**
- Added `Microsoft.Extensions.Configuration.Binder` (v10.0.3) to `Seed.Shared.csproj` to support `IConfiguration.GetValue<T>` in the extension method
- `StripeSettings` registration follows the same conditional pattern as the existing SMTP block in `DependencyInjection.cs`
- `ModulesSettings` is registered unconditionally to allow injection even when the payments module is disabled
- Extension method `IsPaymentsModuleEnabled` lives in `Seed.Shared/Extensions/ConfigurationExtensions.cs`
- 3 unit tests cover enabled, disabled, and missing-section scenarios

### Convenzioni da task Done correlati
- **T-12 (Subscription guards):** `PermissionAuthorizationPolicyProvider` extended to handle `Plan:` and `Feature:` prefixes in addition to `Permission:`, keeping a single policy provider for all authorization schemes. `AlwaysAllowSubscriptionAccessService` registered as fallback (else branch) when payments module is disabled. Both handlers (`RequiresPlanAuthorizationHandler`, `RequiresFeatureAuthorizationHandler`) registered unconditionally in `Program.cs` — the module-disabled bypass is internal to each handler, not a DI concern.
- **T-13 (Subscription info in auth/me):** `NullSubscriptionInfoService` (always returns `null`) registered as fallback when payments module is disabled — frontend interprets absence as "all features available" (DA-1). `ISubscriptionInfoService` created as a separate interface from `ISubscriptionAccessService`.
- **T-13b (Frontend feature gating):** `SubscriptionService` mirrors `PermissionService` pattern: `providedIn: 'root'`, computed signals, boolean check methods. `hasPlan`/`hasAnyPlan`/`hasFeature` all return `true` when `subscription === null`, consistent with DA-1 (payments module disabled → all features available).
- **T-01 (Module toggle):** Extension method `IsPaymentsModuleEnabled` lives in `Seed.Shared/Extensions/ConfigurationExtensions.cs`. `ModulesSettings` is registered unconditionally.
- **T-21 (GDPR):** `IServiceProvider.GetService<IPaymentGateway>()` used in `UserPurgeService` to optionally resolve the gateway — avoids changing DI registration, returns null when module is disabled (no-op).

### Riferimenti
- `docs/requirements/FEAT-3.md` — sezione DA-1: "Modulo attivabile via configurazione". Quando disabilitato: le rotte API dei pagamenti non vengono registrate, le navigation guards frontend nascondono le pagine subscription, i subscription guards backend passano sempre.

## Stato attuale del codice

### Backend
- **`backend/src/Seed.Infrastructure/DependencyInjection.cs`** — All billing handlers, `IPaymentGateway`, `IWebhookEventHandler`, `ISubscriptionAccessService`, `ISubscriptionInfoService` are conditionally registered inside `if (configuration.IsPaymentsModuleEnabled())` block (lines 84-132). When disabled, `AlwaysAllowSubscriptionAccessService` and `NullSubscriptionInfoService` are registered (lines 128-132). **Currently controllers are always registered** — `builder.Services.AddControllers()` in `Program.cs` scans all assemblies, including billing controllers.
- **`backend/src/Seed.Api/Controllers/BillingController.cs`** — User-facing billing endpoints (checkout, subscription, portal, cancel, invoice requests).
- **`backend/src/Seed.Api/Controllers/StripeWebhookController.cs`** — Stripe webhook endpoint.
- **`backend/src/Seed.Api/Controllers/AdminPlansController.cs`** — Admin CRUD plans.
- **`backend/src/Seed.Api/Controllers/AdminSubscriptionsController.cs`** — Admin subscriptions dashboard.
- **`backend/src/Seed.Api/Controllers/AdminInvoiceRequestsController.cs`** — Admin invoice requests.
- **`backend/src/Seed.Api/Controllers/PlansController.cs`** (if exists) — Public plans endpoint.
- **`backend/src/Seed.Shared/Extensions/ConfigurationExtensions.cs`** — `IsPaymentsModuleEnabled()` extension method.
- **`backend/src/Seed.Shared/Configuration/ModulesSettings.cs`** — `ModulesSettings` POCO.
- **Authorization handlers** in `Program.cs` registered unconditionally (by design, T-12) — they check module state internally.

### Frontend
- **`frontend/web/projects/app/src/app/app.routes.ts`** — Routes for `/pricing`, `/billing/success`, `/billing/cancel`, `/billing/subscription`, `/billing/invoice-requests` are always registered (lines 61-83).
- **`frontend/web/projects/app/src/app/app.html`** — Line 12: billing subscription icon link always shown when authenticated.
- **`frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`** — Admin routes for `plans`, `subscriptions`, `invoice-requests` always registered (lines 45-61).
- **`frontend/web/projects/app/src/app/pages/admin/admin-layout.html`** — Admin sidebar shows Plans, Abbonamenti, Richieste fattura links always (lines 24-38).
- **`frontend/web/projects/shared-auth/src/lib/models/auth.models.ts`** — `User` interface has optional `subscription?: SubscriptionInfo | null` field. When module disabled, backend returns `null`.
- **`frontend/web/projects/shared-auth/src/lib/services/auth.service.ts`** — `_subscription` signal, updated from `/me` response.
- **No config flag** currently indicates whether payments module is active to the frontend.

### Integration tests
- **`backend/tests/Seed.IntegrationTests/Infrastructure/CustomWebApplicationFactory.cs`** — Default factory (payments module disabled by default since `appsettings.json` has `Enabled: false`).
- **`backend/tests/Seed.IntegrationTests/Webhooks/WebhookWebApplicationFactory.cs`** — Payments-enabled factory.

## Piano di esecuzione

### Step 1: Backend — Expose payments module flag via `/auth/me` (or extend existing response)

The frontend needs to know if the payments module is active. Two options:
- (A) Add a `paymentsEnabled` field to `MeResponse`
- (B) Create a dedicated unauthenticated `GET /api/v1.0/config` endpoint returning `{ paymentsEnabled: bool }`

**Approach: Option B** — A `/config` endpoint is better because:
- Unauthenticated users need this info too (to show/hide pricing page link)
- T-23 explicitly says "from `/auth/me` or a dedicated `/config` endpoint"

1. Create `backend/src/Seed.Api/Controllers/ConfigController.cs`:
   - `GET /api/v1.0/config` — anonymous endpoint
   - Returns `{ paymentsEnabled: bool }` by reading `IConfiguration.IsPaymentsModuleEnabled()`
   - No MediatR needed, simple controller

### Step 2: Backend — Conditionally exclude billing controllers when module disabled

Use ASP.NET Core's `ApplicationPartManager` to conditionally remove billing controller types when the module is disabled.

1. Create `backend/src/Seed.Api/Extensions/PaymentsModuleExtensions.cs`:
   - Extension method `AddPaymentsModule(this IServiceCollection, IConfiguration)` that:
     - When module disabled: uses `ApplicationPartManager.FeatureProviders` or a convention to exclude `BillingController`, `StripeWebhookController`, `AdminPlansController`, `AdminSubscriptionsController`, `AdminInvoiceRequestsController` and the public `PlansController` (if separate)
   - Approach: Use `IApplicationModelConvention` that removes controllers by type when disabled. OR use `ConfigureApplicationPartManager` to remove specific types.
   - Simpler approach: Use a `[ServiceFilter]` or action filter that returns 404 when disabled. But the task says "no payment routes appear in Swagger."
   - Best approach: `IControllerModelConvention` that sets `controller.ApiExplorer.IsVisible = false` and adds an action filter returning 404.
   - **Cleanest approach:** Create an `IApplicationModelConvention` that removes billing controllers entirely from the application model when the module is disabled. This prevents route registration and Swagger exposure.

2. Modify `backend/src/Seed.Api/Program.cs`:
   - Call the new extension after `AddControllers()` to apply the convention.

### Step 3: Frontend — Add config service and module flag

1. Create a config model in `frontend/web/projects/shared-core/src/lib/models/` or directly in the app:
   - `AppConfig` interface: `{ paymentsEnabled: boolean }`

2. Create `frontend/web/projects/app/src/app/services/config.service.ts`:
   - Calls `GET /api/v1.0/config` on init
   - Exposes `paymentsEnabled` signal
   - Could use `APP_INITIALIZER` to load before routing, or load lazily

3. Alternatively, add a `paymentsEnabled` signal to an existing service (e.g., `AuthService`). But since this info is needed before auth, a separate config service with `APP_INITIALIZER` is cleaner.

### Step 4: Frontend — Conditionally show/hide navigation and routes

1. **`frontend/web/projects/app/src/app/app.html`** — Wrap the billing subscription icon (line 12) in `@if (configService.paymentsEnabled())`.

2. **`frontend/web/projects/app/src/app/app.routes.ts`** — Option A: keep routes but add a guard that redirects when disabled. Option B: dynamically build routes. **Option A is simpler** — add a `paymentsEnabledGuard` that checks the config service.

3. **`frontend/web/projects/app/src/app/pages/admin/admin-layout.html`** — Wrap plans/subscriptions/invoice-requests nav items (lines 24-38) in conditional blocks checking `paymentsEnabled`.

4. **`frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`** — Add same guard to admin plans/subscriptions/invoice-requests routes.

### Step 5: Integration test — Module disabled behavior

1. Create `backend/tests/Seed.IntegrationTests/Billing/PaymentsModuleDisabledTests.cs`:
   - Uses `CustomWebApplicationFactory` (module disabled by default)
   - Test: `GET /api/v1.0/plans` returns 404
   - Test: `GET /api/v1.0/config` returns `{ paymentsEnabled: false }`
   - Test: Billing endpoints not accessible (404)

### Detailed file list

**Files to create:**
- `backend/src/Seed.Api/Controllers/ConfigController.cs`
- `backend/src/Seed.Api/Conventions/PaymentsModuleConvention.cs` (or in Extensions/)
- `frontend/web/projects/app/src/app/services/config.service.ts`
- `frontend/web/projects/app/src/app/guards/payments-enabled.guard.ts`
- `backend/tests/Seed.IntegrationTests/Billing/PaymentsModuleDisabledTests.cs`

**Files to modify:**
- `backend/src/Seed.Api/Program.cs` — Register the convention
- `frontend/web/projects/app/src/app/app.html` — Conditionally show billing icon
- `frontend/web/projects/app/src/app/app.routes.ts` — Add `paymentsEnabledGuard` to billing routes
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — Conditionally show plans/subscriptions/invoice-requests nav items
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Add guard to admin billing routes
- `frontend/web/projects/app/src/app/app.config.ts` (or equivalent) — Register config service / APP_INITIALIZER

### Test plan

**Integration tests** (`backend/tests/Seed.IntegrationTests/Billing/PaymentsModuleDisabledTests.cs`):
- `ModuleDisabled_PlansEndpoint_Returns404`
- `ModuleDisabled_BillingEndpoints_Return404`
- `ModuleDisabled_WebhookEndpoint_Returns404`
- `ModuleDisabled_ConfigEndpoint_ReturnsPaymentsDisabled`
- `ModuleEnabled_ConfigEndpoint_ReturnsPaymentsEnabled` (use `WebhookWebApplicationFactory`)

## Criteri di completamento
- [x] All payment services/controllers conditionally registered
- [x] Module disabled → no payment routes, guards pass always
- [x] Module enabled → full functionality
- [x] Frontend hides payment UI when module disabled
- [x] Integration test verifying disabled module behavior

## Risultato

### File creati
- `backend/src/Seed.Api/Controllers/ConfigController.cs` — endpoint `GET /api/v1.0/config` anonimo che restituisce `{ paymentsEnabled: bool }`
- `backend/src/Seed.Api/Conventions/PaymentsModuleConvention.cs` — `IApplicationModelConvention` che rimuove `BillingController`, `StripeWebhookController`, `AdminPlansController`, `AdminSubscriptionsController`, `AdminInvoiceRequestsController`, `PlansController` dall'application model quando il modulo è disabilitato
- `frontend/web/projects/app/src/app/services/config.service.ts` — service con signal `paymentsEnabled`, carica `/config` via `APP_INITIALIZER`
- `frontend/web/projects/app/src/app/guards/payments-enabled.guard.ts` — guard che reindirizza a `/` se `paymentsEnabled() === false`
- `backend/tests/Seed.IntegrationTests/Billing/PaymentsModuleDisabledTests.cs` — 5 test di integrazione (disabled: plans/billing/webhook → 404, config → false; enabled: config → true)

### File modificati
- `backend/src/Seed.Api/Program.cs` — `AddControllers()` ora aggiunge `PaymentsModuleConvention` quando il modulo è disabilitato; aggiunti `using Seed.Shared.Extensions` e `using Seed.Api.Conventions`
- `frontend/web/projects/app/src/app/app.config.ts` — aggiunto `APP_INITIALIZER` per `ConfigService.loadConfig()`
- `frontend/web/projects/app/src/app/app.ts` — iniettato `ConfigService` per uso nel template
- `frontend/web/projects/app/src/app/app.html` — billing icon wrappata in `@if (configService.paymentsEnabled())`
- `frontend/web/projects/app/src/app/app.routes.ts` — aggiunto `paymentsEnabledGuard` a pricing, billing/success, billing/cancel, billing/subscription, billing/invoice-requests
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.ts` — iniettato `ConfigService`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — nav items plans/subscriptions/invoice-requests wrappati in `@if (configService.paymentsEnabled())`
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — aggiunto `paymentsEnabledGuard` alle rotte plans/subscriptions/invoice-requests

### Scelte chiave
- **`IApplicationModelConvention`** invece di action filter o middleware: rimuove i controller dall'application model al build time, quindi le route non vengono registrate e Swagger non le espone (soddisfa il requisito "no payment routes in Swagger")
- **`APP_INITIALIZER`** per caricare la config: garantisce che `paymentsEnabled` sia valorizzato prima che le route guard vengano eseguite
- **Fallback a `false`** nel `ConfigService` in caso di errore HTTP: comportamento sicuro (nessuna feature payment esposta se il backend non risponde)
- **`paymentsEnabledGuard` sulla route `/pricing`** oltre alle route `/billing/*`: anche la pagina pubblica dei prezzi viene nascosta quando il modulo è disabilitato

### Deviazioni dal mini-plan
Nessuna deviazione significativa. Il piano è stato seguito fedelmente.
