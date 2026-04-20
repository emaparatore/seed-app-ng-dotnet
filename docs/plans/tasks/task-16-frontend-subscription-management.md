# Task 16: Frontend — Subscription management section

## Contesto ereditato dal piano

### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-003 | Visualizzare il proprio abbonamento | T-09, T-16 | 🔄 In Progress (backend done) |
| US-004 | Gestire pagamento e cancellare abbonamento | T-09, T-16 | 🔄 In Progress (backend done) |
| US-005 | Upgrade/downgrade del piano | T-09, T-16 | 🔄 In Progress (backend done) |

### Dipendenze (da 'Depends on:')

**T-09: Subscription management — portal, view, cancel**
- Implementation Notes:
  - `GetMySubscription` returns `null` (not failure) when no subscription exists — "no data ≠ error" pattern, consistent with clean API semantics
  - `CancelSubscription` sets both `CanceledAt` and `UpdatedAt` locally after calling `IPaymentGateway.CancelSubscriptionAsync` (which sets cancel_at_period_end on Stripe); webhook will later sync final status
  - `CreatePortalSession` intentionally has no audit logging — it's a redirect to Stripe with no local state mutation
  - All 3 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
  - Unit tests use InMemory DB for `ApplicationDbContext` and NSubstitute for `IPaymentGateway`/`IAuditService`, following the pattern established in T-08

**T-15: Frontend — Checkout flow**
- Implementation Notes:
  - `billing.models.ts` extended with `CheckoutSessionResponse` and `CreateCheckoutRequest` interfaces mirroring backend DTOs
  - `BillingService` gained `billingUrl` and `createCheckoutSession()` method; `apiUrl` renamed to `plansUrl` for clarity
  - `checkoutLoading` signal added to `PricingComponent`; not reset on success since `window.location.href` immediately triggers external navigation
  - `CheckoutSuccess` and `CheckoutCancel` standalone components created following the `confirm-email` pattern (MatCard, status icon, RouterLink buttons)
  - Routes `/billing/success` and `/billing/cancel` added with `authGuard`; success page links to `/` temporarily until T-16 implements `/billing/subscription`

### Convenzioni da task Done correlati

- Handler placed in `Seed.Infrastructure/Billing/Queries/` (not Application) because Application does not reference Infrastructure. Query contract and DTOs remain in Application. (T-07)
- `BillingService` in `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` uses `inject(AUTH_CONFIG).apiUrl` for base URL; already has `billingUrl = ${this.baseUrl}/billing` and `plansUrl = ${this.baseUrl}/plans`. (T-14/T-15)
- `billing.models.ts` in `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` defines all billing-related TypeScript interfaces mirroring backend DTOs. (T-14/T-15)
- Component state managed via signals: `loading`, `error`, etc.; computed signals for derived state. (T-14)
- Standalone components with Angular Material imports (MatCard, MatIcon, MatButton, etc.). (T-14/T-15)
- `CheckoutSuccess` and `CheckoutCancel` use `auth-container`/`auth-card` CSS classes with centered card layout. (T-15)
- BillingController endpoints: `GET billing/subscription`, `POST billing/portal` (takes `ReturnUrl`), `POST billing/cancel` (no body). (T-09)
- `authGuard` used for billing routes. (T-15)

### Riferimenti

- `docs/requirements/FEAT-3.md` — US-003 (view subscription), US-004 (manage payment, cancel), US-005 (upgrade/downgrade)
- `docs/plans/PLAN-5.md` — T-16 definition

## Stato attuale del codice

### Backend (already complete — T-09)

- `backend/src/Seed.Api/Controllers/BillingController.cs` — endpoints:
  - `GET api/v1.0/billing/subscription` → returns `UserSubscriptionDto?`
  - `POST api/v1.0/billing/portal` → accepts `{ returnUrl }`, returns `{ portalUrl }`
  - `POST api/v1.0/billing/cancel` → no body, returns 200 OK
- `backend/src/Seed.Application/Billing/Models/UserSubscriptionDto.cs` — DTO:
  ```
  UserSubscriptionDto(Guid Id, string PlanName, string? PlanDescription,
      string Status, decimal MonthlyPrice, decimal YearlyPrice,
      DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd,
      DateTime? TrialEnd, DateTime? CanceledAt,
      bool IsFreeTier, IReadOnlyList<PlanFeatureDto> Features)
  ```
- `backend/src/Seed.Application/Billing/Models/PortalSessionResponse.cs` — `PortalSessionResponse(string PortalUrl)`
- `backend/src/Seed.Application/Billing/Commands/CreatePortalSession/CreatePortalSessionCommand.cs` — takes `ReturnUrl` (string)
- `backend/src/Seed.Application/Billing/Commands/CancelSubscription/CancelSubscriptionCommand.cs` — no user input fields (UserId injected server-side)

### Frontend (current state)

- `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` — has `getPlans()` and `createCheckoutSession()`. Missing: `getMySubscription()`, `createPortalSession()`, `cancelSubscription()`.
- `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` — has `Plan`, `PlanFeature`, `CheckoutSessionResponse`, `CreateCheckoutRequest`. Missing: `UserSubscription`, `PortalSessionResponse`.
- `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.html` — success page currently links to `/` for "Vai alla gestione abbonamento" (should link to `/billing/subscription` after T-16).
- `frontend/web/projects/app/src/app/app.routes.ts` — has `/billing/success` and `/billing/cancel` routes. Missing: `/billing/subscription` route.
- `frontend/web/projects/app/src/app/app.html` — navigation bar with profile link. No subscription/billing link yet.
- `frontend/web/projects/app/src/app/pages/profile/profile.ts` — existing profile page pattern with signals, `MatDialog`, `AuthService`.

## Piano di esecuzione

### Step 1: Add TypeScript interfaces to billing.models.ts

**File:** `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts`

Add interfaces mirroring backend DTOs:
```typescript
export interface UserSubscription {
  id: string;
  planName: string;
  planDescription: string | null;
  status: string;
  monthlyPrice: number;
  yearlyPrice: number;
  currentPeriodStart: string;
  currentPeriodEnd: string;
  trialEnd: string | null;
  canceledAt: string | null;
  isFreeTier: boolean;
  features: PlanFeature[];
}

export interface PortalSessionResponse {
  portalUrl: string;
}
```

### Step 2: Add methods to BillingService

**File:** `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts`

Add three methods:
- `getMySubscription(): Observable<UserSubscription | null>` → `GET ${billingUrl}/subscription`
- `createPortalSession(returnUrl: string): Observable<PortalSessionResponse>` → `POST ${billingUrl}/portal` with `{ returnUrl }`
- `cancelSubscription(): Observable<void>` → `POST ${billingUrl}/cancel`

### Step 3: Create SubscriptionComponent

**Files to create:**
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.ts`
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.html`
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.scss`

Component behavior:
1. On init, call `billingService.getMySubscription()`
2. **Active/Trialing state:** show plan name, status badge, price, next renewal date (`currentPeriodEnd`), features list
3. **Trialing state:** additionally show trial end date and days remaining (computed from `trialEnd`)
4. **No subscription / free tier:** show free tier info + "Upgrade" CTA → `/pricing`
5. **Canceled state:** show cancelation date, period end date
6. Buttons:
   - "Gestisci pagamento" → call `createPortalSession(window.location.href)` → redirect to `portalUrl`
   - "Cambia piano" → `router.navigate(['/pricing'])`
   - "Cancella abbonamento" → confirmation dialog (MatDialog) → call `cancelSubscription()` → reload subscription data
7. Use Angular Material components: MatCard, MatButton, MatIcon, MatDialog, MatProgressSpinner, MatChip (for status badge)
8. Use signals for state management: `loading`, `subscription`, `error`, `canceling`, `portalLoading`

### Step 4: Create cancel confirmation dialog

**File:** `frontend/web/projects/app/src/app/pages/billing/subscription/confirm-cancel-dialog.ts`

Simple dialog (following `confirm-delete-dialog` pattern from profile page):
- Message: "Sei sicuro di voler cancellare l'abbonamento? L'accesso rimarrà attivo fino alla fine del periodo corrente."
- Two buttons: "Annulla" (mat-dialog-close) and "Conferma cancellazione" (mat-dialog-close with true)

### Step 5: Add route

**File:** `frontend/web/projects/app/src/app/app.routes.ts`

Add before the wildcard `**` route:
```typescript
{
  path: 'billing/subscription',
  loadComponent: () => import('./pages/billing/subscription/subscription').then((m) => m.Subscription),
  canActivate: [authGuard],
},
```

### Step 6: Update checkout-success link

**File:** `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.html`

Change `routerLink="/"` on "Vai alla gestione abbonamento" to `routerLink="/billing/subscription"`.

### Step 7: Add navigation link

**File:** `frontend/web/projects/app/src/app/app.html`

Add a link to `/billing/subscription` in the authenticated user section of the toolbar, near the profile link. Could be an icon button or text link (e.g., mat-icon `credit_card` or `subscriptions`).

### Tests

No unit tests explicitly required by the task's Definition of Done. The task is frontend UI with no complex logic beyond API calls. If tests are deemed necessary:
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.spec.ts` — test component renders states (loading, active, trialing, free, canceled, error)
- Can be added via `ng test app`

## Criteri di completamento

(Definition of Done copiata verbatim da PLAN-5.md)

- [x] Subscription details displayed correctly for all states (active, trialing, canceled, free)
- [x] Portal redirect works
- [x] Cancel flow with confirmation dialog
- [x] Upgrade/change plan navigates to pricing
- [x] Trial days remaining shown when applicable
- [x] Link accessible from profile/navigation

## Risultato

### File modificati

- `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` — aggiunta interfacce `UserSubscription` e `PortalSessionResponse`
- `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` — aggiunti metodi `getMySubscription()`, `createPortalSession()`, `cancelSubscription()`
- `frontend/web/projects/app/src/app/app.routes.ts` — aggiunta route `billing/subscription`
- `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.html` — aggiornato link "Vai alla gestione abbonamento" da `/` a `/billing/subscription`
- `frontend/web/projects/app/src/app/app.html` — aggiunto link icon button `credit_card` a `/billing/subscription` nella navbar

### File creati

- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.ts` — componente principale con signals (`loading`, `subscription`, `error`, `canceling`, `portalLoading`) e computed signals (`trialDaysRemaining`, `isActive`, `isTrialing`, `isCanceled`)
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.html` — template con gestione stati: loading spinner, errore con retry, free tier con CTA upgrade, abbonamento attivo/trialing/cancellato con dettagli e azioni
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.scss` — stili per tutti gli stati, status badge chip colorati per stato
- `frontend/web/projects/app/src/app/pages/billing/subscription/confirm-cancel-dialog.ts` — dialog di conferma cancellazione abbonamento

### Scelte chiave

- `loadSubscription()` dichiarata `protected` (non `private`) per permettere il binding nel template (`(click)="loadSubscription()"` nel bottone "Riprova")
- `DatePipe` e `DecimalPipe` importati esplicitamente nel componente standalone (necessario in Angular 17+ standalone)
- `UserSubscription` è definita prima di `PlanFeature` nel file models — TypeScript lo gestisce correttamente (forward reference tra interfacce)
- La navbar usa `mat-icon-button` con `credit_card` icon, coerente con lo stile esistente dell'`account_circle`

### Deviazioni dal piano

- Nessuna deviazione significativa. Il piano è stato implementato interamente.
