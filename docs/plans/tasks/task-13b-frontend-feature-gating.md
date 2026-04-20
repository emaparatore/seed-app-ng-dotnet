# Task 13b: Frontend — Feature gating (directive, guard, service)

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-011 | Feature gating frontend | T-13 | 🔄 In Progress (backend done) |

### Dipendenze (da 'Depends on:')
T-13: Include subscription info in auth/me response

**Implementation Notes (T-13, verbatim):**
- `ISubscriptionInfoService` created as a separate interface from `ISubscriptionAccessService` — the former returns structured data for `/me`, the latter is bool-based for authorization
- `SubscriptionInfoDto` is a `sealed record` with `CurrentPlan`, `PlanFeatures`, `SubscriptionStatus`, `TrialEndsAt`
- `SubscriptionInfoService` queries with `Include(Plan).ThenInclude(Features)`, filters on Active/Trialing statuses, returns `null` if no active subscription found
- `NullSubscriptionInfoService` (always returns `null`) registered as fallback when payments module is disabled — frontend interprets absence as "all features available" (DA-1)
- Two new unit tests added: `Should_Include_Subscription_Info_When_Service_Returns_Data` and `Should_Return_Null_Subscription_When_Service_Returns_Null`

### Convenzioni da task Done correlati
From T-12 (Subscription guards — backend authorization):
- `PermissionAuthorizationPolicyProvider` extended to handle `Plan:` and `Feature:` prefixes in addition to `Permission:`, keeping a single policy provider for all authorization schemes
- `AlwaysAllowSubscriptionAccessService` registered as fallback (else branch) when payments module is disabled, following the same if/else pattern as the email service in `DependencyInjection.cs`
- `ISubscriptionAccessService` uses `AnyAsync` for efficiency; queries filter on `SubscriptionStatus.Active` and `SubscriptionStatus.Trialing` only

From T-14 (Frontend — Pricing page):
- `billing.models.ts` defines `Plan` and `PlanFeature` interfaces mirroring backend DTOs (Guid → `string`)
- `BillingService` uses `inject(AUTH_CONFIG).apiUrl` for base URL
- Component state managed via signals: `loading`, `plans`, `error`, `billingInterval`; `sortedPlans` is a computed that orders by `sortOrder` without mutating the original signal

From T-17 (Frontend — Admin plans management):
- `PlanFeature` reused from `billing.models.ts` via re-export in `admin-plans.models.ts` — no duplication between public and admin models
- `Plans: { Read, Create, Update }` block added to `permissions.ts`; permissions are automatically picked up by existing permission guard infrastructure

### Riferimenti
- `docs/requirements/FEAT-3.md` — US-011: Feature gating frontend
- Backend DTO: `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/SubscriptionInfoDto.cs` — `sealed record SubscriptionInfoDto(string CurrentPlan, IReadOnlyList<string> PlanFeatures, string SubscriptionStatus, DateTime? TrialEndsAt)`
- Backend MeResponse: `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/MeResponse.cs` — includes `SubscriptionInfoDto? Subscription` field

## Stato attuale del codice

- **`frontend/web/projects/shared-auth/src/lib/models/auth.models.ts`** — `User` interface has `id, email, firstName, lastName, roles, permissions`. Does NOT include `subscription` field. Backend `/auth/me` returns `MeResponse` with `Subscription` (SubscriptionInfoDto?) but the frontend discards it.
- **`frontend/web/projects/shared-auth/src/lib/services/auth.service.ts`** — `getProfile()` fetches `User` from `/auth/me`, maps to `_currentUser` signal. No subscription data stored.
- **`frontend/web/projects/shared-auth/src/lib/services/permission.service.ts`** — `providedIn: 'root'`, exposes `permissions` signal from AuthService, `hasPermission(permission: string): boolean`, `hasAnyPermission(permissions: string[]): boolean`. Pattern to follow for SubscriptionService.
- **`frontend/web/projects/shared-auth/src/lib/directives/has-permission.directive.ts`** — Structural directive using `input.required<string>()`, `effect()`, `TemplateRef`, `ViewContainerRef`. Pattern to follow for RequiresPlanDirective.
- **`frontend/web/projects/shared-auth/src/lib/guards/permission.guard.ts`** — Factory function `permissionGuard(permission: string): CanActivateFn`, uses `inject()` inside returned function. Pattern to follow for requiresPlanGuard.
- **`frontend/web/projects/shared-auth/src/public-api.ts`** — Exports all guards, directives, services, models from shared-auth library.
- **`frontend/web/projects/shared-auth/src/lib/models/permissions.ts`** — Already has `Plans` and `Subscriptions` permission blocks.

## Piano di esecuzione

### Step 1: Extend User model with subscription info
- **File:** `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts`
- Add `SubscriptionInfo` interface:
  ```ts
  export interface SubscriptionInfo {
    currentPlan: string;
    planFeatures: string[];
    subscriptionStatus: string;
    trialEndsAt: string | null;
  }
  ```
- Extend `User` interface with `subscription?: SubscriptionInfo | null`

### Step 2: Update AuthService to store subscription data
- **File:** `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts`
- Add private signal `_subscription = signal<SubscriptionInfo | null>(null)`
- Add public readonly signal `subscription = this._subscription.asReadonly()`
- Update `getProfile()` to map subscription data from /me response (the response already includes it from backend, just need to type it)
- Update `clearAuth()` to reset subscription signal to null

### Step 3: Create SubscriptionService
- **File (new):** `frontend/web/projects/shared-auth/src/lib/services/subscription.service.ts`
- `@Injectable({ providedIn: 'root' })`, following PermissionService pattern
- Inject `AuthService`, expose:
  - `currentPlan = computed(() => this.authService.subscription()?.currentPlan ?? null)` 
  - `planFeatures = computed(() => this.authService.subscription()?.planFeatures ?? [])`
  - `subscriptionStatus = computed(() => this.authService.subscription()?.subscriptionStatus ?? null)`
  - `trialEndsAt = computed(() => this.authService.subscription()?.trialEndsAt ?? null)`
  - `hasPlan(planName: string): boolean` — if subscription is null (payments module disabled), return true; otherwise check currentPlan matches
  - `hasFeature(featureKey: string): boolean` — if subscription is null (payments module disabled), return true; otherwise check planFeatures includes key
  - `hasAnyPlan(planNames: string[]): boolean` — if subscription is null, return true; otherwise check currentPlan is in list

### Step 4: Create RequiresPlanDirective
- **File (new):** `frontend/web/projects/shared-auth/src/lib/directives/requires-plan.directive.ts`
- Structural directive `*requiresPlan="'Pro'"` following `HasPermissionDirective` pattern exactly:
  - `input.required<string | string[]>()` for requiresPlan (accepts single plan or array)
  - `effect()` to toggle view via `ViewContainerRef.createEmbeddedView` / `.clear()`
  - Uses `SubscriptionService.hasPlan()` or `hasAnyPlan()`

### Step 5: Create requiresPlanGuard
- **File (new):** `frontend/web/projects/shared-auth/src/lib/guards/requires-plan.guard.ts`
- Factory function `requiresPlanGuard(...planNames: string[]): CanActivateFn` following `permissionGuard` pattern
- If `SubscriptionService.hasAnyPlan(planNames)` → return true
- Otherwise → redirect to `/pricing`

### Step 6: Export from public-api.ts
- **File:** `frontend/web/projects/shared-auth/src/public-api.ts`
- Add exports:
  ```ts
  export * from './lib/services/subscription.service';
  export * from './lib/directives/requires-plan.directive';
  export * from './lib/guards/requires-plan.guard';
  ```

### Step 7: Write unit tests
- **File (new):** `frontend/web/projects/shared-auth/src/lib/services/subscription.service.spec.ts`
  - Test `hasPlan` returns true when subscription is null (payments module disabled)
  - Test `hasPlan` returns true when plan matches
  - Test `hasPlan` returns false when plan doesn't match
  - Test `hasFeature` returns true when subscription is null
  - Test `hasFeature` returns true when feature present
  - Test `hasFeature` returns false when feature absent
- **File (new):** `frontend/web/projects/shared-auth/src/lib/directives/requires-plan.directive.spec.ts`
  - Follow `has-permission.directive.spec.ts` pattern exactly
  - Test shows content when user has plan
  - Test hides content when user lacks plan
  - Test shows content when subscription is null (module disabled)
- **File (new):** `frontend/web/projects/shared-auth/src/lib/guards/requires-plan.guard.spec.ts`
  - Follow `permission.guard.spec.ts` pattern exactly
  - Test returns true when user has required plan
  - Test redirects to /pricing when user lacks required plan
  - Test returns true when subscription is null (module disabled)

### Step 8: Verify build and tests
- Run `ng test shared-auth` from `frontend/web/`
- Run `ng build shared-auth` from `frontend/web/`

## Criteri di completamento
- [x] `SubscriptionService` exposes plan signals from auth/me data
- [x] `*requiresPlan` directive conditionally renders elements
- [x] `requiresPlanGuard` protects routes
- [x] All checks pass when payments module is disabled
- [x] Unit tests for service and directive

## Risultato

### File modificati
- `frontend/web/projects/shared-auth/src/lib/models/auth.models.ts` — added `SubscriptionInfo` interface; extended `User` with `subscription?: SubscriptionInfo | null`
- `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts` — added `_subscription` signal, exposed as `subscription` readonly; updated `getProfile()` to extract `user.subscription`; updated `clearAuth()` to reset to null
- `frontend/web/projects/shared-auth/src/public-api.ts` — added exports for `subscription.service`, `requires-plan.guard`, `requires-plan.directive`

### File creati
- `frontend/web/projects/shared-auth/src/lib/services/subscription.service.ts` — `SubscriptionService` with computed signals and `hasPlan`/`hasAnyPlan`/`hasFeature` methods; null subscription → all return true (payments module disabled)
- `frontend/web/projects/shared-auth/src/lib/directives/requires-plan.directive.ts` — structural directive `*requiresPlan` accepting `string | string[]`; uses `hasPlan`/`hasAnyPlan` from `SubscriptionService`
- `frontend/web/projects/shared-auth/src/lib/guards/requires-plan.guard.ts` — factory guard `requiresPlanGuard(...planNames)` redirecting to `/pricing` on failure
- `frontend/web/projects/shared-auth/src/lib/services/subscription.service.spec.ts` — 9 unit tests for `hasPlan`, `hasAnyPlan`, `hasFeature`
- `frontend/web/projects/shared-auth/src/lib/directives/requires-plan.directive.spec.ts` — 3 tests: shows with plan, hides without, shows when null
- `frontend/web/projects/shared-auth/src/lib/guards/requires-plan.guard.spec.ts` — 3 tests: true with plan, redirect to /pricing without, true when null

### Scelte chiave
- `subscription` stored on `User` interface (optional) so `getProfile()` naturally picks it up from /me without a separate response type
- `hasPlan`/`hasAnyPlan`/`hasFeature` all return `true` when `subscription === null`, consistent with DA-1 (payments module disabled → all features available)
- `RequiresPlanDirective` accepts both `string` and `string[]` for flexibility (single plan or multi-plan check)
- No deviazioni dal mini-plan
