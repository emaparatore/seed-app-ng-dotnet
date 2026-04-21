# Task 14: Frontend — Pricing page

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-001 | Visualizzare i piani disponibili | T-07, T-14 | 🔄 In Progress (backend done) |

### Dipendenze (da 'Depends on:')
T-07: Public plans API — list available plans

**Implementation Notes (T-07 verbatim):**
- Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application.
- Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block, since MediatR only scans the Application assembly.
- Used LINQ projection (`Select`) instead of Mapster for mapping — simpler for a read-only query with no complex logic.
- Integration tests reuse `WebhookWebApplicationFactory` which already configures the payments module as enabled.
- No additional DI or middleware changes needed — controller discovered via standard MVC assembly scan.

### Convenzioni da task Done correlati
- **T-07 (Public plans API):** Backend endpoint is `GET /api/v1.0/plans` on `PlansController`, `[AllowAnonymous]`. Returns `IReadOnlyList<PlanDto>` on success (HTTP 200), or `{ errors: [...] }` on failure (HTTP 400).
- **T-08 (Checkout flow):** Backend endpoint is `POST /api/v1.0/billing/checkout` on `BillingController`, `[Authorize]`. Accepts `{ planId, billingInterval }`. Returns `{ checkoutUrl }`. Metadata keys `"userId"` and `"planId"` passed for webhook compatibility. `BillingInterval` enum: Monthly, Yearly.
- **T-02 (Domain entities):** `PlanDto` fields: `Id (Guid), Name, Description?, MonthlyPrice (decimal), YearlyPrice (decimal), TrialDays (int), IsFreeTier (bool), IsDefault (bool), IsPopular (bool), SortOrder (int), Features: PlanFeatureDto[]`. `PlanFeatureDto` fields: `Id (Guid), Key, Description, LimitValue?, SortOrder (int)`.
- **T-13 (Auth/me subscription info):** `MeResponse` now includes optional `subscription` with `CurrentPlan`, `PlanFeatures`, `SubscriptionStatus`, `TrialEndsAt`. When payments module disabled or no subscription, these are null (frontend treats as "all features available").

### Riferimenti
- `docs/requirements/FEAT-3.md` — RF-1 (Modello dati dei piani), RF-3 (Checkout e pagamento), DA-4 (Stripe Checkout redirect), DA-1 (Modulo attivabile)
- `docs/plans/PLAN-5.md` — T-14 definition

## Stato attuale del codice

### File esistenti rilevanti
- `frontend/web/projects/app/src/app/app.routes.ts` — Main routing. Currently has routes for home, login, register, profile, admin, etc. No `/pricing` route. Wildcard `**` redirect is last entry.
- `frontend/web/projects/app/src/app/pages/home/home.ts` — Landing page component. Pattern: standalone component, `templateUrl` + `styleUrl`, inject PLATFORM_ID.
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.ts` — Example data-fetching component. Pattern: `signal()` for state (`loading`, `data`, `error`), service injection via `inject()`, `OnInit` lifecycle, error handling with `subscribe({ next, error })`.
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.service.ts` — Example service. Pattern: `@Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `inject(AUTH_CONFIG).apiUrl` for base URL, returns `Observable<T>`.
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.html` — Example template. Pattern: `@if`/`@else if`/`@else` control flow, `@for` with `track`, skeleton loading state, error state with retry button, Angular Material components.
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.scss` — Example styles. Pattern: `:host { display: block }`, CSS variables `var(--mat-sys-*)`, responsive breakpoints at 768px, grid layout.
- `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts` — AuthService with `isAuthenticated` computed signal. Used to determine if user is logged in (for CTA button logic).
- `frontend/web/projects/shared-auth/src/lib/auth.config.ts` — `AUTH_CONFIG` injection token providing `apiUrl`.
- `frontend/web/projects/shared-auth/src/public-api.ts` — Exports from shared-auth library.
- `frontend/web/projects/app/src/environments/environment.ts` — `apiUrl: 'http://localhost:5035/api/v1'`.

### Pattern già in uso
- **Standalone components** with `imports: [...]` array (no NgModules)
- **Angular signals** for component state: `signal()`, `computed()`
- **Services** use `@Injectable({ providedIn: 'root' })` with `inject()` pattern
- **API base URL** from `inject(AUTH_CONFIG).apiUrl`
- **Angular Material** v21.2.4 — cards (`MatCardModule`), icons (`MatIconModule`), buttons (`MatButtonModule`)
- **Template syntax**: `@if`/`@else if`/`@for` control flow (not `*ngIf`/`*ngFor`)
- **SCSS** with CSS custom properties `var(--mat-sys-*)` for theming
- **Lazy loading** via `loadComponent` in routes
- **Export pattern**: component exported as named class (e.g., `export class Home`, `export class Dashboard`)

## Piano di esecuzione

### Files da creare

1. **`frontend/web/projects/app/src/app/pages/pricing/billing.models.ts`**
   - TypeScript interfaces mirroring backend DTOs:
     - `PlanFeature` — `id: string, key: string, description: string, limitValue: string | null, sortOrder: number`
     - `Plan` — `id: string, name: string, description: string | null, monthlyPrice: number, yearlyPrice: number, trialDays: number, isFreeTier: boolean, isDefault: boolean, isPopular: boolean, sortOrder: number, features: PlanFeature[]`
   - Note: Guid maps to `string` in TypeScript (JSON serialization)

2. **`frontend/web/projects/app/src/app/pages/pricing/billing.service.ts`**
   - `@Injectable({ providedIn: 'root' })`
   - `private readonly http = inject(HttpClient)`
   - `private readonly apiUrl = inject(AUTH_CONFIG).apiUrl` (note: plans endpoint is `/api/v1.0/plans`, not under `/billing`)
   - `getPlans(): Observable<Plan[]>` — `GET ${apiUrl}/plans` (the apiUrl already ends with `/api/v1`, but the plans controller is at `/api/v1.0/plans`; verify the actual URL base from environment — environment has `apiUrl: 'http://localhost:5035/api/v1'`, and PlansController route is `api/v{version:apiVersion}/plans` with version 1.0, so the URL should be `${apiUrl}/plans` since v1 ≈ v1.0)

3. **`frontend/web/projects/app/src/app/pages/pricing/pricing.ts`**
   - Standalone component, selector `app-pricing`
   - Imports: `MatCardModule`, `MatIconModule`, `MatButtonModule`, `MatButtonToggleModule`, `RouterLink`, `CurrencyPipe`
   - Injects: `BillingService`, `AuthService` (from shared-auth), `Router`
   - State signals: `loading = signal(true)`, `plans = signal<Plan[]>([])`, `error = signal<string | null>(null)`, `billingInterval = signal<'monthly' | 'yearly'>('monthly')`
   - Computed: `displayPrice(plan)` based on billingInterval, `yearlyDiscount` computed
   - `ngOnInit`: fetch plans from BillingService
   - CTA logic:
     - Free tier → `routerLink="/register"` (if not authenticated) or nothing special if already auth
     - Paid plan (not authenticated) → `routerLink="/login"` (or navigate to login with returnUrl)
     - Paid plan (authenticated) → navigate to checkout (will be implemented in T-15, for now just link to login or show disabled)
   - Monthly/yearly toggle using `MatButtonToggle`

4. **`frontend/web/projects/app/src/app/pages/pricing/pricing.html`**
   - Header section with title "Scegli il piano giusto per te" (or similar)
   - Monthly/yearly toggle (`mat-button-toggle-group`)
   - Plan cards grid using `mat-card` with:
     - Plan name, description
     - Price display (monthly or yearly based on toggle)
     - "Più popolare" badge when `plan.isPopular`
     - Feature list with check icons
     - Trial info "Prova gratis per X giorni" when `plan.trialDays > 0`
     - CTA button: "Inizia gratis" for free tier, "Scegli piano" for paid
   - Loading skeleton state
   - Error state with retry

5. **`frontend/web/projects/app/src/app/pages/pricing/pricing.scss`**
   - `:host { display: block }` pattern
   - Responsive grid for plan cards (auto-fit, minmax)
   - Popular plan highlighted styling (border or badge)
   - CSS variables for theming consistency
   - Mobile breakpoint at 768px

### Files da modificare

6. **`frontend/web/projects/app/src/app/app.routes.ts`**
   - Add `/pricing` route (no auth guard, lazy loaded):
     ```typescript
     {
       path: 'pricing',
       loadComponent: () => import('./pages/pricing/pricing').then((m) => m.Pricing),
     },
     ```
   - Insert BEFORE the wildcard `**` redirect

### Approccio step-by-step

1. Create `billing.models.ts` with TypeScript interfaces for `Plan` and `PlanFeature`
2. Create `billing.service.ts` with `getPlans()` method calling `GET /api/v1/plans`
3. Create `pricing.ts` component with signals-based state management, plan fetching, billing interval toggle, CTA logic based on auth state
4. Create `pricing.html` template with loading/error/data states, plan cards grid, monthly/yearly toggle, feature lists, CTA buttons
5. Create `pricing.scss` with responsive grid layout, popular plan highlight, Material theme vars
6. Add `/pricing` route to `app.routes.ts`
7. Verify build compiles: `cd frontend/web && npm run build`

### Test da scrivere/verificare
No unit tests specified in the Definition of Done for T-14 (frontend component). The DoD focuses on rendering and behavior verification. Manual verification:
- Plans render correctly from API
- Toggle switches prices
- Popular plan highlighted
- CTAs link correctly (register for free, login for paid if not auth)
- Responsive layout works on mobile

## Criteri di completamento
(Definition of Done copiata verbatim da PLAN-5.md)
- [ ] Pricing page renders all active plans
- [ ] Monthly/yearly price toggle works
- [ ] Popular plan highlighted
- [ ] Free tier CTA links to registration
- [ ] Paid plan CTA redirects to login if not authenticated
- [ ] Route accessible without authentication
- [ ] Responsive layout (mobile-friendly)

## Risultato

### File modificati
- `frontend/web/projects/app/src/app/app.routes.ts` — aggiunta route `/pricing` (lazy-loaded, no auth guard) prima del wildcard

### File creati
- `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` — interfacce `Plan` e `PlanFeature` che specchiano i backend DTO
- `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` — `BillingService` con `getPlans()` su `GET ${apiUrl}/plans`
- `frontend/web/projects/app/src/app/pages/pricing/pricing.ts` — componente standalone con signals (`loading`, `plans`, `error`, `billingInterval`), computed `sortedPlans`, logica CTA (gratis→register, paid non-auth→login)
- `frontend/web/projects/app/src/app/pages/pricing/pricing.html` — template con toggle mensile/annuale, griglia piani, badge "Più popolare", lista feature, stati loading/error
- `frontend/web/projects/app/src/app/pages/pricing/pricing.scss` — layout responsive (auto-fit grid), evidenziazione piano popolare, skeleton loading, breakpoint 768px

### Scelte chiave
- `billingInterval` è un `signal<'monthly' | 'yearly'>` mutabile (non `WritableSignal` wrappato) per poter usare `.set()` direttamente nel template con `(change)`
- `sortedPlans` è un computed che ordina i piani per `sortOrder` senza mutare il signal originale
- CTA per piano a pagamento: quando autenticato punta ancora a `/login` — il checkout (T-15) non è ancora implementato; il comportamento verrà aggiornato in T-15
- URL del service: `${apiUrl}/plans` — l'environment ha `apiUrl: 'http://localhost:5035/api/v1'`, il controller risponde su `/api/v1.0/plans` (v1 ≈ v1.0 per Asp.Versioning)

### Deviazioni dal mini-plan
- Nessuna deviazione sostanziale. La logica CTA per piano a pagamento + utente autenticato naviga a `/login` come indicato nel piano (T-15 non ancora implementato).
