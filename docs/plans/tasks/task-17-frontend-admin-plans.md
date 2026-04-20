# Task 17: Frontend — Admin plans management

## Contesto ereditato dal piano

### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-007 | Admin — CRUD piani | T-10, T-17 | In Progress (backend done) |

### Dipendenze (da 'Depends on:')

T-10: Admin CRUD plans — Implementation Notes:
- `AdminPlanDetailDto` not created separately — `AdminPlanDto` reused for both list and detail endpoints, as it already contains full details including Stripe IDs and features
- `UpdatePlan` manages features via Key matching: features with the same Key are updated, missing ones removed, new ones added
- `ArchivePlanCommand` does not call `IPaymentGateway` — archiving is a DB-only status change, no Stripe sync needed
- Plans permissions (Read/Create/Update) are seeded automatically because `RolesAndPermissionsSeeder` reads `Permissions.GetAll()` — no manual seeder change required
- All 5 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`, consistent with prior billing tasks

### Convenzioni da task Done correlati

**T-14 (Frontend — Pricing page):**
- `billing.models.ts` defines `Plan` and `PlanFeature` interfaces mirroring backend DTOs (Guid → `string`)
- `BillingService` uses `inject(AUTH_CONFIG).apiUrl` for base URL; `getPlans()` calls `GET ${apiUrl}/plans` (v1 ~ v1.0 for Asp.Versioning)
- Component state managed via signals: `loading`, `plans`, `error`, `billingInterval`; `sortedPlans` is a computed that orders by `sortOrder` without mutating the original signal
- Lazy-loaded route added to routes before the wildcard `**` redirect

**T-16 (Frontend — Subscription management):**
- `SubscriptionComponent` uses five signals and four computed signals for all UI states
- `DatePipe` and `DecimalPipe` imported explicitly in the standalone component (required in Angular 17+ standalone)
- `confirm-cancel-dialog.ts` follows the `confirm-delete-dialog` pattern from the profile page; returns `true` on confirm

**T-07 (Public plans API):**
- Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure
- Handler registered manually in DI inside `IsPaymentsModuleEnabled()` block

**T-08 (Checkout flow):**
- BillingController created with `[Authorize]`, primary constructor pattern (ISender), and helper properties (CurrentUserId, IpAddress, UserAgent) matching AdminSettingsController

### Riferimenti

- `docs/requirements/FEAT-3.md` — US-007: Admin CRUD piani (RF-7)
- `docs/plans/PLAN-5.md` — Task T-17 definition and T-10 implementation notes

## Stato attuale del codice

### Backend API (gia' implementato da T-10)

- `backend/src/Seed.Api/Controllers/AdminPlansController.cs` — Controller con 5 endpoint:
  - `GET api/v1.0/admin/plans` — lista piani con subscriber count (richiede `Plans.Read`)
  - `GET api/v1.0/admin/plans/{id}` — dettaglio piano (richiede `Plans.Read`)
  - `POST api/v1.0/admin/plans` — crea piano (richiede `Plans.Create`)
  - `PUT api/v1.0/admin/plans/{id}` — aggiorna piano (richiede `Plans.Update`)
  - `POST api/v1.0/admin/plans/{id}/archive` — archivia piano (richiede `Plans.Update`)
- `backend/src/Seed.Application/Admin/Plans/Models/AdminPlanDto.cs` — DTO: `AdminPlanDto(Guid Id, string Name, string? Description, decimal MonthlyPrice, decimal YearlyPrice, string? StripePriceIdMonthly, string? StripePriceIdYearly, string? StripeProductId, int TrialDays, bool IsFreeTier, bool IsDefault, bool IsPopular, string Status, int SortOrder, DateTime CreatedAt, DateTime UpdatedAt, int SubscriberCount, IReadOnlyList<PlanFeatureDto> Features)`
- `backend/src/Seed.Application/Admin/Plans/Models/CreatePlanFeatureRequest.cs` — `CreatePlanFeatureRequest(string Key, string Description, string? LimitValue, int SortOrder)`
- `backend/src/Seed.Application/Admin/Plans/Commands/CreatePlan/CreatePlanCommand.cs` — Campi: Name, Description?, MonthlyPrice, YearlyPrice, TrialDays, IsFreeTier, IsDefault, IsPopular, SortOrder, List<CreatePlanFeatureRequest> Features. Campi JsonIgnore: CurrentUserId, IpAddress, UserAgent.
- `backend/src/Seed.Application/Admin/Plans/Commands/UpdatePlan/UpdatePlanCommand.cs` — Stessi campi di Create + PlanId (JsonIgnore). Features usa Key matching per aggiornare/rimuovere/aggiungere.
- `backend/src/Seed.Application/Admin/Plans/Commands/ArchivePlan/ArchivePlanCommand.cs` — Solo Guid planId + campi audit (JsonIgnore).

### Frontend - Pattern esistenti

- **Admin layout:** `frontend/web/projects/app/src/app/pages/admin/admin-layout.ts` — Sidebar con `mat-nav-list`, ogni item usa `*hasPermission`, `routerLink`, `routerLinkActive="active"`, `mat-icon`, `(click)="onNavClick()"`.
- **Admin routes:** `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Ogni route usa `permissionGuard('Permission.Name')`, `loadComponent`, e `data: { title: '...' }`.
- **Admin service pattern:** `frontend/web/projects/app/src/app/pages/admin/users/users.service.ts` — `Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `inject(AUTH_CONFIG).apiUrl`, metodi che ritornano `Observable<T>`.
- **Admin list pattern:** `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.ts` — Component standalone con `signal()` per stato, `MatTable`, skeleton loading, error state, empty state, `MatSnackBar` per notifiche, `MatDialog` per create/confirm.
- **Create dialog pattern:** `frontend/web/projects/app/src/app/pages/admin/users/create-user-dialog/create-user-dialog.ts` — `FormBuilder.nonNullable.group({})`, `inject(MAT_DIALOG_DATA)`, `saving` signal, `errorMessage` signal, `onSubmit()` + `onCancel()`.
- **Confirm dialog:** `frontend/web/projects/app/src/app/pages/admin/users/confirm-dialog/confirm-dialog.ts` — Riusabile con `ConfirmDialogData { title, message, confirmText?, cancelText? }`.
- **Billing models:** `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` — Contiene gia' `Plan`, `PlanFeature` interfaces per l'API pubblica.
- **Permissions frontend:** `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — `PERMISSIONS` const con struttura `{ Category: { Action: 'Category.Action' } }`. NON contiene ancora `Plans` ne' `Subscriptions`.

## Piano di esecuzione

### Step 1: Aggiungere permessi Plans al frontend

- **File:** `frontend/web/projects/shared-auth/src/lib/models/permissions.ts`
- Aggiungere:
  ```typescript
  Plans: {
    Read: 'Plans.Read',
    Create: 'Plans.Create',
    Update: 'Plans.Update',
  },
  ```

### Step 2: Creare i modelli admin billing

- **File da creare:** `frontend/web/projects/app/src/app/pages/admin/plans/admin-plans.models.ts`
- Interfacce da definire:
  - `AdminPlan` — mirror di `AdminPlanDto`: id, name, description, monthlyPrice, yearlyPrice, stripePriceIdMonthly, stripePriceIdYearly, stripeProductId, trialDays, isFreeTier, isDefault, isPopular, status, sortOrder, createdAt, updatedAt, subscriberCount, features: PlanFeature[]
  - `CreatePlanRequest` — name, description, monthlyPrice, yearlyPrice, trialDays, isFreeTier, isDefault, isPopular, sortOrder, features: CreatePlanFeatureRequest[]
  - `CreatePlanFeatureRequest` — key, description, limitValue, sortOrder
  - Riusare `PlanFeature` da `billing.models.ts` (importare)

### Step 3: Creare il servizio admin plans

- **File da creare:** `frontend/web/projects/app/src/app/pages/admin/plans/admin-plans.service.ts`
- Pattern: seguire `AdminUsersService` — `Injectable({ providedIn: 'root' })`, `inject(HttpClient)`, `inject(AUTH_CONFIG).apiUrl`
- URL base: `${apiUrl}/admin/plans`
- Metodi:
  - `getPlans(): Observable<AdminPlan[]>` — `GET /admin/plans`
  - `getPlanById(id: string): Observable<AdminPlan>` — `GET /admin/plans/{id}`
  - `createPlan(request: CreatePlanRequest): Observable<{ id: string }>` — `POST /admin/plans`
  - `updatePlan(id: string, request: CreatePlanRequest): Observable<void>` — `PUT /admin/plans/{id}`
  - `archivePlan(id: string): Observable<void>` — `POST /admin/plans/{id}/archive`

### Step 4: Creare il componente lista piani admin

- **File da creare:**
  - `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.ts`
  - `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.html`
  - `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.scss`
- Pattern: seguire `user-list.ts` come riferimento
- Colonne tabella: Name, Monthly Price, Yearly Price, Status, Subscribers, Actions
- Stato gestito via signals: `loading`, `plans`, `error`
- Azioni: Crea (bottone header), Modifica (icon button per riga), Archivia (icon button per riga con conferma)
- Status badge con classi: `active` (verde), `inactive` (giallo), `archived` (grigio)
- Skeleton loading, error state, empty state seguendo il pattern user-list
- Usare `MatSnackBar` per notifiche successo/errore
- Usare `MatDialog` per aprire il dialog create/edit e il ConfirmDialog per archivia
- `HasPermissionDirective` per mostrare/nascondere bottone crea e azioni
- `DecimalPipe` per formattare i prezzi (es. `| number:'1.2-2'`)
- La ConfirmDialog per l'archivazione va importata da `../../users/confirm-dialog/confirm-dialog` (riusare quella esistente)

### Step 5: Creare il dialog create/edit piano

- **File da creare:**
  - `frontend/web/projects/app/src/app/pages/admin/plans/plan-edit-dialog/plan-edit-dialog.ts`
  - `frontend/web/projects/app/src/app/pages/admin/plans/plan-edit-dialog/plan-edit-dialog.html`
- Pattern: seguire `create-user-dialog.ts`
- Dialog data: `{ plan?: AdminPlan }` — se `plan` presente, e' edit; se assente, e' create
- Form fields con `FormBuilder.nonNullable.group()`:
  - name (required), description, monthlyPrice (required, min 0), yearlyPrice (required, min 0), trialDays (min 0), isFreeTier (checkbox), isDefault (checkbox), isPopular (checkbox), sortOrder (required, min 0)
  - features: `FormArray` di `FormGroup` — ciascuno con key (required), description (required), limitValue, sortOrder
  - Bottoni "Aggiungi feature" e "Rimuovi" per gestione dinamica features
- In modalita' edit: warning visivo su modifica prezzo ("La modifica del prezzo creera' un nuovo Price su Stripe")
- `saving` signal, `errorMessage` signal
- Submit: chiama service.createPlan() o service.updatePlan() e chiude il dialog con il risultato
- Titolo dialog: "Nuovo piano" (create) o "Modifica piano" (edit)
- In edit mode, pre-fill form con i dati del piano esistente

### Step 6: Aggiungere route e navigazione sidebar

- **File:** `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`
- Aggiungere route:
  ```typescript
  {
    path: 'plans',
    loadComponent: () => import('./plans/plan-list/plan-list').then((m) => m.PlanList),
    canActivate: [permissionGuard('Plans.Read')],
    data: { title: 'Piani' },
  },
  ```

- **File:** `frontend/web/projects/app/src/app/pages/admin/admin-layout.html`
- Aggiungere item sidebar (dopo "Ruoli" e prima di "Audit Log"):
  ```html
  <a mat-list-item routerLink="plans" routerLinkActive="active"
     *hasPermission="permissions.Plans.Read" (click)="onNavClick()">
    <mat-icon matListItemIcon>payments</mat-icon>
    <span matListItemTitle>Piani</span>
  </a>
  ```

### Step 7: Verificare build e funzionamento

- Eseguire `npm run build` da `frontend/web/` per verificare compilazione
- Eseguire `ng test app` da `frontend/web/` per verificare test esistenti

## Criteri di completamento

(Definition of Done copiata verbatim da PLAN-5.md)

- [x] Plans list with all columns rendered
- [x] Create plan dialog with all fields + dynamic feature list
- [x] Edit plan dialog
- [x] Archive with confirmation
- [x] Subscriber count shown per plan
- [x] Permission-gated route and navigation item
- [x] Form validation

## Risultato

### File modificati

- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — aggiunto blocco `Plans: { Read, Create, Update }`
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — aggiunta route `plans` con `permissionGuard('Plans.Read')`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — aggiunto item sidebar "Piani" con icona `payments`

### File creati

- `frontend/web/projects/app/src/app/pages/admin/plans/admin-plans.models.ts` — interfacce `AdminPlan`, `CreatePlanRequest`, `CreatePlanFeatureRequest`; re-export di `PlanFeature` da `billing.models.ts`
- `frontend/web/projects/app/src/app/pages/admin/plans/admin-plans.service.ts` — `AdminPlansService` con i 5 metodi CRUD/archive
- `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.ts` — componente lista piani con signals, skeleton loading, error state, empty state
- `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.html` — tabella con colonne Name, Prezzo mensile, Prezzo annuale, Stato, Subscriber, Azioni; badge default/popolare/free
- `frontend/web/projects/app/src/app/pages/admin/plans/plan-list/plan-list.scss` — stili seguendo il pattern user-list; status badge active/inactive/archived
- `frontend/web/projects/app/src/app/pages/admin/plans/plan-edit-dialog/plan-edit-dialog.ts` — dialog create/edit con `FormArray` per le features, warning Stripe in edit mode
- `frontend/web/projects/app/src/app/pages/admin/plans/plan-edit-dialog/plan-edit-dialog.html` — form completo con tutti i campi e gestione dinamica feature

### Scelte chiave

- `PlanFeature` riusata da `billing.models.ts` via re-export in `admin-plans.models.ts` (no duplicazione)
- `ConfirmDialog` riusata da `../../users/confirm-dialog/confirm-dialog` come da piano
- Nella `onSubmit()` del dialog, le chiamate `updatePlan` e `createPlan` sono separate (non union type) per risolvere l'incompatibilità TypeScript tra `Observable<void>` e `Observable<{id: string}>`
- Build verificato: nessun errore (solo warning pre-esistenti su `RouterLink` non usato in componenti precedenti)
