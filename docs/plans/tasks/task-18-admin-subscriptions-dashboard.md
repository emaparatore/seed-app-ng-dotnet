# Task 18: Frontend — Admin subscriptions dashboard

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-008 | Admin — dashboard abbonamenti | T-11, T-18 | 🔄 In Progress (backend done) |

### Dipendenze (da 'Depends on:')
**T-11: Admin subscriptions dashboard API**
**Implementation Notes (verbatim):**
- MRR calculation detects yearly billing by period length > 35 days and uses `YearlyPrice/12`; churn rate guards against division by zero when no subscriptions exist
- Query handlers placed in `Seed.Infrastructure/Billing/Queries/` (not Application) because `ApplicationDbContext` is only available in Infrastructure — consistent with T-07/T-10 convention
- All 3 handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
- Integration tests seed a real user with `Subscriptions.Read` permission via `WebhookWebApplicationFactory`, which already enables the payments module
- `Subscriptions.Read` permission is added to the `All` array in `Permissions.cs`, so it is picked up automatically by `RolesAndPermissionsSeeder` without any seeder change

**T-17: Frontend — Admin plans management**
**Implementation Notes (verbatim):**
- `PlanFeature` reused from `billing.models.ts` via re-export in `admin-plans.models.ts` — no duplication between public and admin models
- `ConfirmDialog` reused from `../../users/confirm-dialog/confirm-dialog` — no new dialog component needed for archive confirmation
- `Plans: { Read, Create, Update }` block added to `permissions.ts`; permissions are automatically picked up by existing permission guard infrastructure
- In `plan-edit-dialog.ts`, `updatePlan` and `createPlan` calls kept separate (not a union type) to resolve TypeScript incompatibility between `Observable<void>` and `Observable<{id: string}>`
- Build verified with no new errors (only pre-existing `RouterLink` warnings in unrelated components)

### Convenzioni da task Done correlati
- **T-14 (Pricing page):** `billing.models.ts` defines `Plan` and `PlanFeature` interfaces mirroring backend DTOs (Guid → `string`). `BillingService` uses `inject(AUTH_CONFIG).apiUrl` for base URL. Component state managed via signals: `loading`, `plans`, `error`; computed signals for derived state.
- **T-17 (Admin plans):** `AdminPlansService` uses `${inject(AUTH_CONFIG).apiUrl}/admin/plans` pattern for API URL. `PlanList` component uses `signal()` for `loading`, `plans`, `error` state. Reuses `ConfirmDialog` from `../../users/confirm-dialog/confirm-dialog`.
- **T-16 (Subscription management):** `SubscriptionComponent` uses five signals and four computed signals for all UI states. `DatePipe` and `DecimalPipe` imported explicitly in standalone component (required in Angular 17+).
- **T-11 (Backend API):** Backend returns `PagedResult<AdminSubscriptionDto>` with `items`, `pageNumber`, `pageSize`, `totalCount`, `totalPages`, `hasPreviousPage`, `hasNextPage` fields.

### Riferimenti
- `docs/requirements/FEAT-3.md` — US-008: Admin — dashboard abbonamenti
- `docs/plans/PLAN-5.md` — T-18 definition, Story Coverage table

## Stato attuale del codice

### Backend API endpoints (already implemented):
- `GET /api/v1.0/admin/subscriptions/metrics` → returns `SubscriptionMetricsDto(Mrr, ActiveCount, TrialingCount, ChurnRate)` — requires `Subscriptions.Read`
- `GET /api/v1.0/admin/subscriptions?pageNumber=&pageSize=&planId=&status=` → returns `PagedResult<AdminSubscriptionDto>` — requires `Subscriptions.Read`
- `GET /api/v1.0/admin/subscriptions/{id}` → returns `AdminSubscriptionDetailDto` — requires `Subscriptions.Read`

### Backend DTOs:
- `SubscriptionMetricsDto(decimal Mrr, int ActiveCount, int TrialingCount, decimal ChurnRate)`
- `AdminSubscriptionDto(Guid Id, string UserEmail, string PlanName, string Status, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd, DateTime? TrialEnd, DateTime? CanceledAt, DateTime CreatedAt)`
- `AdminSubscriptionDetailDto(Guid Id, Guid UserId, string UserEmail, string UserFullName, Guid PlanId, string PlanName, decimal MonthlyPrice, decimal YearlyPrice, string Status, string? StripeSubscriptionId, string? StripeCustomerId, DateTime CurrentPeriodStart, DateTime CurrentPeriodEnd, DateTime? TrialEnd, DateTime? CanceledAt, DateTime CreatedAt, DateTime UpdatedAt)`
- `PagedResult<T>` has: `Items`, `PageNumber`, `PageSize`, `TotalCount`, `TotalPages`, `HasPreviousPage`, `HasNextPage`

### Frontend files rilevanti:
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — `PERMISSIONS` constant, currently **missing** `Subscriptions` block
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — admin routes, needs new `subscriptions` route
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — sidebar nav, needs "Abbonamenti" link
- `frontend/web/projects/app/src/app/pages/admin/plans/admin-plans.service.ts` — pattern for admin service (uses `AUTH_CONFIG.apiUrl`)
- `frontend/web/projects/app/src/app/pages/admin/plans/admin-plans.models.ts` — pattern for admin models (re-exports from billing.models)
- `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.ts` — pattern for admin list component (signals, MatTable)
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.ts` — pattern for paginated list with filters (MatPaginator, FormControl, debounce)
- `frontend/web/projects/app/src/app/pages/admin/users/models/user.models.ts` — contains `PagedResult<T>` interface reusable across admin pages
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.ts` — pattern for metric cards (stats-grid layout, mat-card stat cards)
- `frontend/web/projects/app/src/app/pages/admin/dashboard/dashboard.scss` — CSS for `.stats-grid`, `.stat-card`, skeleton loading

### Patterns in use:
- Standalone components with explicit imports (Angular 17+ style)
- Signal-based state (`signal()`, `computed()`)
- `@if`/`@else if`/`@else`/`@for` template syntax (Angular 17+)
- `inject()` for DI in constructors or field initializers
- `MatSnackBar` for success/error feedback
- `MatDialog` for detail views/dialogs
- Error pattern: `err.error?.errors?.[0] ?? 'Errore nel caricamento...'`
- Loading skeleton pattern with `.skeleton-*` CSS classes and `pulse` animation
- Service uses `inject(AUTH_CONFIG).apiUrl` for base URL

## Piano di esecuzione

### Step 1: Add `Subscriptions.Read` permission to frontend
- **File:** `frontend/web/projects/shared-auth/src/lib/models/permissions.ts`
- Add `Subscriptions: { Read: 'Subscriptions.Read' }` block to `PERMISSIONS` constant

### Step 2: Create models
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/admin-subscriptions.models.ts`
- Define TypeScript interfaces mirroring backend DTOs:
  - `SubscriptionMetrics` — `{ mrr: number; activeCount: number; trialingCount: number; churnRate: number }`
  - `AdminSubscription` — `{ id: string; userEmail: string; planName: string; status: string; currentPeriodStart: string; currentPeriodEnd: string; trialEnd: string | null; canceledAt: string | null; createdAt: string }`
  - `AdminSubscriptionDetail` — extends with `userId: string; userFullName: string; planId: string; monthlyPrice: number; yearlyPrice: number; stripeSubscriptionId: string | null; stripeCustomerId: string | null; updatedAt: string`
  - Import `PagedResult` from `../../users/models/user.models` (already defined there)

### Step 3: Create service
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/admin-subscriptions.service.ts`
- `AdminSubscriptionsService` with:
  - `private readonly apiUrl = \`${inject(AUTH_CONFIG).apiUrl}/admin/subscriptions\``
  - `getMetrics(): Observable<SubscriptionMetrics>`
  - `getSubscriptions(params): Observable<PagedResult<AdminSubscription>>` — params: `pageNumber`, `pageSize`, `planId?`, `status?`
  - `getSubscriptionById(id): Observable<AdminSubscriptionDetail>`
- Follow `AdminPlansService` pattern: `@Injectable({ providedIn: 'root' })`, inject `HttpClient` and `AUTH_CONFIG`

### Step 4: Create subscription list component
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-list/subscription-list.ts`
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-list/subscription-list.html`
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-list/subscription-list.scss`
- Component `SubscriptionList` (exported as `SubscriptionList`):
  - **Metrics section** at top: 4 stat cards (MRR, Active, Trialing, Churn rate) using `stats-grid` / `stat-card` pattern from dashboard
  - **Filters**: Plan select (fetch plans from `AdminPlansService.getPlans()`), Status select (Active, Trialing, PastDue, Canceled, Expired)
  - **Table**: columns `userEmail`, `planName`, `status`, `currentPeriodStart`, `currentPeriodEnd`, `actions`
  - **Pagination**: `MatPaginator` — follow user-list pattern with `pageIndex`, `pageSize`, `totalCount` signals
  - **Row click or view button**: open detail dialog
  - Status badges with color coding (reuse plan-list `.status-badge` pattern: active=green, trialing=blue, past_due=orange, canceled=red, expired=gray)
  - Skeleton loading for both metrics cards and table
  - Error and empty states following existing pattern

### Step 5: Create subscription detail dialog
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-detail-dialog/subscription-detail-dialog.ts`
- **File (new):** `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-detail-dialog/subscription-detail-dialog.html`
- Dialog component showing full `AdminSubscriptionDetail` info:
  - User info: email, full name
  - Plan info: name, monthly/yearly price
  - Subscription info: status, Stripe IDs, period dates, trial end, canceled at, created/updated at
  - Read-only view (no mutations from admin)
- Follow `PlanEditDialog` pattern for dialog structure (`MAT_DIALOG_DATA` inject, `MatDialogModule`, `MatDialogRef`)

### Step 6: Add route and sidebar navigation
- **File:** `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`
- Add route: `{ path: 'subscriptions', loadComponent: () => import('./subscriptions/subscription-list/subscription-list').then(m => m.SubscriptionList), canActivate: [permissionGuard('Subscriptions.Read')], data: { title: 'Abbonamenti' } }`
- **File:** `frontend/web/projects/app/src/app/pages/admin/admin-layout.html`
- Add nav item after "Piani": `<a mat-list-item routerLink="subscriptions" routerLinkActive="active" *hasPermission="permissions.Subscriptions.Read" (click)="onNavClick()"><mat-icon matListItemIcon>subscriptions</mat-icon><span matListItemTitle>Abbonamenti</span></a>`

### Step 7: Verify build
- Run `cd frontend/web && npm run build` to verify no compilation errors

## Criteri di completamento
- [x] Metrics cards displayed with correct values
- [x] Subscriptions list with filtering and pagination
- [x] Detail view accessible
- [x] Permission-gated route and navigation item

## Risultato

### File modificati
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — aggiunto blocco `Subscriptions: { Read: 'Subscriptions.Read' }`
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — aggiunta route `subscriptions` con `permissionGuard('Subscriptions.Read')`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — aggiunto link "Abbonamenti" dopo "Piani"

### File creati
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/admin-subscriptions.models.ts` — interfacce `SubscriptionMetrics`, `AdminSubscription`, `AdminSubscriptionDetail`, `GetSubscriptionsParams`; re-export di `PagedResult`
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/admin-subscriptions.service.ts` — `AdminSubscriptionsService` con `getMetrics()`, `getSubscriptions()`, `getSubscriptionById()`
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-list/subscription-list.ts` — componente lista con 4 metric cards, filtri piano/stato, tabella paginata, apertura detail dialog
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-list/subscription-list.html` — template con skeleton loading, cards, filtri, tabella, paginatore
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-list/subscription-list.scss` — stili SCSS (stats-grid, status-badge per Active/Trialing/PastDue/Canceled/Expired, skeleton, empty/error state)
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-detail-dialog/subscription-detail-dialog.ts` — dialog read-only con MAT_DIALOG_DATA
- `frontend/web/projects/app/src/app/pages/admin/subscriptions/subscription-detail-dialog/subscription-detail-dialog.html` — template con sezioni Utente, Piano, Abbonamento, Stripe

### Scelte chiave
- Il detail viene caricato on-demand al click (HTTP call) anziché usare i dati già in lista, per avere tutti i campi extra di `AdminSubscriptionDetail`
- Lo stato `PastDue` viene gestito con CSS class `past-due` (dash) per evitare conflitti con nomi di classe invalidi
- Build verificata: nessun errore nuovo, solo warning pre-esistenti (RouterLink in Pricing/Subscription, budget exceeded)

### Deviazioni dal piano
- Nessuna deviazione significativa. Il detail dialog non ha SCSS separato (stili inline nel componente), seguendo il pattern di `PlanEditDialog`.
