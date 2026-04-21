# Task 15: Frontend — Checkout flow

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-002 | Sottoscrivere un piano a pagamento | T-08, T-15 | 🔄 In Progress (backend done) |
| US-006 | Trial period | T-08, T-15 | 🔄 In Progress (backend done) |

### Dipendenze (da 'Depends on:')
**T-08: Checkout flow — create checkout session**
- Implementation Notes:
  - BillingController created with `[Authorize]`, primary constructor pattern (ISender), and helper properties (CurrentUserId, IpAddress, UserAgent) matching AdminSettingsController
  - Handler registered manually in DI inside `IsPaymentsModuleEnabled()` block, consistent with GetPlansQueryHandler
  - InMemoryDatabase used for DbContext in handler unit tests; NSubstitute for UserManager and IPaymentGateway to verify call patterns (Received/DidNotReceive)
  - StripeCustomerId lookup follows existing domain model: searches last UserSubscription for the user, creates new Stripe customer only if none found
  - Metadata keys `"userId"` and `"planId"` passed in checkout request for webhook compatibility with StripeWebhookEventHandler

**T-14: Frontend — Pricing page**
- Implementation Notes:
  - `billing.models.ts` defines `Plan` and `PlanFeature` interfaces mirroring backend DTOs (Guid → `string`)
  - `BillingService` uses `inject(AUTH_CONFIG).apiUrl` for base URL; `getPlans()` calls `GET ${apiUrl}/plans` (v1 ≈ v1.0 for Asp.Versioning)
  - Component state managed via signals: `loading`, `plans`, `error`, `billingInterval`; `sortedPlans` is a computed that orders by `sortOrder` without mutating the original signal
  - Paid plan CTA when authenticated still navigates to `/login` — checkout redirect will be updated in T-15
  - Lazy-loaded route `/pricing` added to `app.routes.ts` before the wildcard `**` redirect

### Convenzioni da task Done correlati
- `BillingService` lives in `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` and uses `inject(AUTH_CONFIG).apiUrl` for base URL
- `billing.models.ts` defines TypeScript interfaces mirroring backend DTOs (Guid → `string`)
- Component state managed via Angular signals (`signal()`, `computed()`)
- Angular Material components used for UI (MatCard, MatIcon, MatButton, MatButtonToggle)
- Routes are lazy-loaded in `app.routes.ts` before the wildcard `**` redirect
- Backend `POST /api/v1.0/billing/checkout` requires auth, accepts `{ planId, billingInterval, successUrl, cancelUrl }` and returns `{ checkoutUrl }`
- `BillingInterval` enum on backend: `Monthly` (0), `Yearly` (1) — frontend sends string `"Monthly"` or `"Yearly"`
- `authGuard` imported from `shared-auth` library

### Riferimenti
- `docs/requirements/FEAT-3.md` — US-002 (sottoscrivere un piano a pagamento), US-006 (trial period)
- `docs/plans/PLAN-5.md` — T-15 definition

## Stato attuale del codice
- **`frontend/web/projects/app/src/app/pages/pricing/pricing.ts`** — Pricing component. `onCtaClick(plan)` has a placeholder comment `// T-15: checkout flow` and currently navigates to `/login` for authenticated users on paid plans. This must be updated to call the checkout API.
- **`frontend/web/projects/app/src/app/pages/pricing/pricing.html`** — Pricing template. CTA button text for authenticated users on paid plans shows "Scegli piano" — should also show "Prova gratis per X giorni" when plan has trial.
- **`frontend/web/projects/app/src/app/pages/pricing/billing.service.ts`** — BillingService with only `getPlans()`. Needs `createCheckoutSession()` method.
- **`frontend/web/projects/app/src/app/pages/pricing/billing.models.ts`** — `Plan` and `PlanFeature` interfaces. Needs `CheckoutSessionResponse` interface.
- **`frontend/web/projects/app/src/app/app.routes.ts`** — Routes. `/pricing` exists. No `/billing/success` or `/billing/cancel` routes yet. `authGuard` already imported.
- **`backend/src/Seed.Api/Controllers/BillingController.cs`** — Backend endpoint `POST /api/v1.0/billing/checkout` accepts `CreateCheckoutSessionCommand` with fields: `PlanId` (Guid), `BillingInterval` (enum), `SuccessUrl` (string), `CancelUrl` (string). Returns `CheckoutSessionResponse` with `checkoutUrl` string.
- **Existing page patterns** — `confirm-email` component uses `@switch` on signal status, MatCard layout, `auth-container` / `auth-card` CSS classes. Simple pages use similar pattern.

## Piano di esecuzione

### Step 1: Add `CheckoutSessionResponse` and `CreateCheckoutRequest` to billing models
- **File:** `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts`
- Add:
  ```typescript
  export interface CheckoutSessionResponse {
    checkoutUrl: string;
  }

  export interface CreateCheckoutRequest {
    planId: string;
    billingInterval: 'Monthly' | 'Yearly';
    successUrl: string;
    cancelUrl: string;
  }
  ```

### Step 2: Add `createCheckoutSession()` to BillingService
- **File:** `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts`
- Add a second private URL `billingUrl` pointing to `${apiUrl}/billing`
- Add method:
  ```typescript
  createCheckoutSession(request: CreateCheckoutRequest): Observable<CheckoutSessionResponse> {
    return this.http.post<CheckoutSessionResponse>(`${this.billingUrl}/checkout`, request);
  }
  ```

### Step 3: Update Pricing component to trigger checkout
- **File:** `frontend/web/projects/app/src/app/pages/pricing/pricing.ts`
- In `onCtaClick(plan)`: when user is authenticated and plan is not free tier, call `billingService.createCheckoutSession()` with:
  - `planId: plan.id`
  - `billingInterval: this.billingInterval() === 'yearly' ? 'Yearly' : 'Monthly'`
  - `successUrl: window.location.origin + '/billing/success'`
  - `cancelUrl: window.location.origin + '/billing/cancel'`
- On success, redirect to `checkoutUrl` via `window.location.href`
- Add a `checkoutLoading` signal to disable the button during the API call
- Handle errors (show snackbar or inline error)

### Step 4: Update Pricing template for trial CTA and loading state
- **File:** `frontend/web/projects/app/src/app/pages/pricing/pricing.html`
- Update CTA button text: when user is authenticated and plan has `trialDays > 0`, show "Prova gratis per {trialDays} giorni" instead of "Scegli piano"
- Add `[disabled]="checkoutLoading()"` to the CTA button
- Optionally show a spinner on the button during checkout loading

### Step 5: Create CheckoutSuccess component
- **File:** `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.ts`
- **File:** `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.html`
- **File:** `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.scss`
- Simple page with MatCard showing:
  - Success icon (`check_circle`)
  - Title: "Abbonamento attivato!"
  - Subtitle: "Il tuo abbonamento è stato attivato con successo."
  - Button: "Vai alla gestione abbonamento" → link to `/billing/subscription` (will exist in T-16, for now link to `/`)
  - Button: "Torna alla home" → link to `/`
- Use same pattern as `confirm-email` component (MatCard, status icons, RouterLink)

### Step 6: Create CheckoutCancel component
- **File:** `frontend/web/projects/app/src/app/pages/billing/checkout-cancel/checkout-cancel.ts`
- **File:** `frontend/web/projects/app/src/app/pages/billing/checkout-cancel/checkout-cancel.html`
- **File:** `frontend/web/projects/app/src/app/pages/billing/checkout-cancel/checkout-cancel.scss`
- Simple page with MatCard showing:
  - Info icon (`info`)
  - Title: "Checkout annullato"
  - Subtitle: "Hai annullato il processo di pagamento. Nessun addebito è stato effettuato."
  - Button: "Torna ai piani" → link to `/pricing`
  - Button: "Torna alla home" → link to `/`

### Step 7: Add routes
- **File:** `frontend/web/projects/app/src/app/app.routes.ts`
- Add before the `pricing` route (all before the wildcard `**`):
  ```typescript
  {
    path: 'billing/success',
    loadComponent: () => import('./pages/billing/checkout-success/checkout-success').then(m => m.CheckoutSuccess),
    canActivate: [authGuard],
  },
  {
    path: 'billing/cancel',
    loadComponent: () => import('./pages/billing/checkout-cancel/checkout-cancel').then(m => m.CheckoutCancel),
    canActivate: [authGuard],
  },
  ```

### Tests
No unit tests are specified in the Definition of Done for T-15 (frontend-only task with no complex logic). The component tests would be primarily testing UI rendering and HTTP calls which are covered by existing patterns. If needed, verify manually:
- Clicking plan CTA as authenticated user triggers checkout API call
- Success/cancel pages render correctly
- Routes are protected by authGuard

## Criteri di completamento
(Definition of Done, verbatim from PLAN-5.md)
- [x] Clicking plan CTA creates checkout session and redirects to Stripe
- [x] Success page rendered after successful checkout
- [x] Cancel page rendered when user cancels checkout
- [x] Trial CTA shows "Prova gratis per X giorni" when plan has trial
- [x] Routes protected by authGuard

## Risultato

### File modificati
- `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` — aggiunto `CheckoutSessionResponse` e `CreateCheckoutRequest`
- `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` — aggiunto `billingUrl`, `createCheckoutSession()` method; rinominato `apiUrl` → `plansUrl` per chiarezza
- `frontend/web/projects/app/src/app/pages/pricing/pricing.ts` — aggiunto `checkoutLoading` signal, `MatProgressSpinnerModule` import, implementato checkout flow in `onCtaClick()`
- `frontend/web/projects/app/src/app/pages/pricing/pricing.html` — CTA aggiornato con spinner durante loading, testo "Prova gratis per X giorni" per piani con trial
- `frontend/web/projects/app/src/app/app.routes.ts` — aggiunte route `/billing/success` e `/billing/cancel` con `authGuard`

### File creati
- `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.ts` — componente success page
- `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.html` — template success page
- `frontend/web/projects/app/src/app/pages/billing/checkout-success/checkout-success.scss` — stili success page
- `frontend/web/projects/app/src/app/pages/billing/checkout-cancel/checkout-cancel.ts` — componente cancel page
- `frontend/web/projects/app/src/app/pages/billing/checkout-cancel/checkout-cancel.html` — template cancel page
- `frontend/web/projects/app/src/app/pages/billing/checkout-cancel/checkout-cancel.scss` — stili cancel page

### Scelte chiave
- Il link "Vai alla gestione abbonamento" in checkout-success punta a `/` temporaneamente (T-16 implementerà `/billing/subscription`)
- `checkoutLoading` non viene resettato a `false` in caso di successo perché `window.location.href` innesca la navigazione esterna e il reset sarebbe irrilevante
- Il spinner CTA appare solo per l'utente autenticato su piani non free (per evitare conflitti visivi con le altre varianti del bottone)

### Deviazioni dal mini-plan
Nessuna deviazione significativa. Il piano è stato seguito fedelmente.
