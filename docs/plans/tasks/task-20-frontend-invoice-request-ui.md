# Task 20: Frontend — Invoice request UI

## Contesto ereditato dal piano

### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-012 | Richiesta fattura manuale | T-19, T-20 | 🔄 In Progress (backend done, frontend pending) |

### Dipendenze (da 'Depends on:')

**T-16: Frontend — Subscription management section**
- `SubscriptionComponent` uses five signals (`loading`, `subscription`, `error`, `canceling`, `portalLoading`) and four computed signals (`trialDaysRemaining`, `isActive`, `isTrialing`, `isCanceled`) for all UI states
- `loadSubscription()` declared `protected` (not `private`) to allow template binding on the "Riprova" retry button
- `DatePipe` and `DecimalPipe` imported explicitly in the standalone component (required in Angular 17+ standalone)
- `confirm-cancel-dialog.ts` follows the `confirm-delete-dialog` pattern from the profile page; returns `true` on confirm
- Navbar link uses `mat-icon-button` with `credit_card` icon, consistent with existing `account_circle` style; checkout-success page updated to link to `/billing/subscription`

**T-19: Backend — Invoice request CRUD**
- `Subscriptions.Manage` permission added to the existing `Subscriptions` class (not a new class) and inserted in `All` array — auto-picked up by `RolesAndPermissionsSeeder`
- All 4 handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`, consistent with prior billing tasks
- `UpdateInvoiceRequestStatusCommand`: `InvoiceRequestId` is `[JsonIgnore]` (bound from URL path), `NewStatus` comes from request body — same pattern as `UpdatePlanCommand`; sets `ProcessedAt` only when new status is `Issued`
- Admin handlers placed in `Seed.Infrastructure/Billing/Commands|Queries/` (not Application) because `ApplicationDbContext` is only available in Infrastructure — query/command contracts remain in Application
- `AuditActions.InvoiceRequestCreated` and `AuditActions.InvoiceRequestStatusUpdated` added; 5 unit test files cover handlers (create, update-status, get-my, get-admin) and validator

### Convenzioni da task Done correlati

- **T-17 (Admin Plans):** `PlanFeature` reused from `billing.models.ts` via re-export in `admin-plans.models.ts` — no duplication between public and admin models. `ConfirmDialog` reused from `../../users/confirm-dialog/confirm-dialog` — no new dialog component needed for archive confirmation. `Plans: { Read, Create, Update }` block added to `permissions.ts`; permissions are automatically picked up by existing permission guard infrastructure.
- **T-18 (Admin Subscriptions Dashboard):** Detail loaded on-demand at row click (HTTP call) rather than using list data. `PastDue` status uses CSS class `past-due` (with dash) to avoid invalid class name conflicts; other statuses use lowercase class names. `Subscriptions: { Read: 'Subscriptions.Read' }` block added to `permissions.ts` in `shared-auth`, auto-picked up by existing permission guard infrastructure. Detail dialog follows `PlanEditDialog` pattern (`MAT_DIALOG_DATA`, `MatDialogModule`, `MatDialogRef`) with no separate SCSS file — styles inline in component.
- **T-15 (Frontend Checkout Flow):** `BillingService` in `pages/pricing/billing.service.ts` uses `providedIn: 'root'`, injects `AUTH_CONFIG` for `apiUrl`, exposes methods returning `Observable`.
- **T-13b (Frontend Feature Gating):** `subscription` stored directly on the `User` interface (optional field) so `getProfile()` picks it up from `/me` without a separate response type.

### Riferimenti

- `docs/requirements/FEAT-3.md` — sezione **RF-9: Richiesta fattura manuale** e **US-012: Richiesta fattura manuale** (acceptance criteria)
- `docs/plans/PLAN-5.md` — Task T-20

**Acceptance criteria da US-012 (FEAT-3.md):**
- Nella sezione abbonamento del profilo, è presente un pulsante "Richiedi fattura"
- Il form richiede: tipo (persona fisica / azienda), nome/ragione sociale, indirizzo completo, codice fiscale e/o P.IVA, codice SDI/PEC (opzionali)
- I dati fiscali vengono salvati nel profilo utente per riutilizzo futuro
- La richiesta viene salvata con riferimento al pagamento Stripe
- L'admin riceve una notifica (audit log + eventuale email) della richiesta
- L'utente può vedere lo storico delle proprie richieste di fattura e il loro stato (richiesta, in lavorazione, emessa)

## Stato attuale del codice

### Backend API (già implementato — T-19)

- **`POST api/v1.0/billing/invoice-request`** — crea richiesta fattura (user, autenticato)
  - Body: `CreateInvoiceRequestCommand` con campi: `CustomerType` (enum: Individual/Company), `FullName`, `CompanyName?`, `Address`, `City`, `PostalCode`, `Country`, `FiscalCode?`, `VatNumber?`, `SdiCode?`, `PecEmail?`, `StripePaymentIntentId?`
  - Response: `Guid` (ID della richiesta creata)
  - Validazione: `FullName`, `Address`, `City`, `PostalCode`, `Country` obbligatori. Per Company: `CompanyName` e `VatNumber` obbligatori.
- **`GET api/v1.0/billing/invoice-requests`** — le mie richieste (user, autenticato)
  - Response: `InvoiceRequestDto[]` con campi: `Id`, `CustomerType`, `FullName`, `CompanyName?`, `Address`, `City`, `PostalCode`, `Country`, `FiscalCode?`, `VatNumber?`, `SdiCode?`, `PecEmail?`, `StripePaymentIntentId?`, `Status`, `CreatedAt`, `ProcessedAt?`
- **`GET api/v1.0/admin/invoice-requests`** — tutte le richieste (admin, `Subscriptions.Read`)
  - Query params: `pageNumber`, `pageSize`, `status`
  - Response: `PagedResult<AdminInvoiceRequestDto>` — come InvoiceRequestDto + `UserEmail`, `UserFullName`
- **`PUT api/v1.0/admin/invoice-requests/{id}/status`** — aggiorna stato (admin, `Subscriptions.Manage`)
  - Body: `{ newStatus: "Requested" | "InProgress" | "Issued" }`

### Frontend — file esistenti rilevanti

- **`frontend/web/projects/app/src/app/pages/billing/subscription/subscription.ts`** — componente abbonamento utente. Qui va aggiunto il pulsante "Richiedi fattura" e il link allo storico.
- **`frontend/web/projects/app/src/app/pages/billing/subscription/subscription.html`** — template del componente abbonamento.
- **`frontend/web/projects/app/src/app/pages/pricing/billing.service.ts`** — service per le API billing user-facing. Qui vanno aggiunti i metodi per invoice request.
- **`frontend/web/projects/app/src/app/pages/pricing/billing.models.ts`** — modelli billing. Qui vanno aggiunte le interfacce per invoice request.
- **`frontend/web/projects/app/src/app/pages/admin/subscriptions/admin-subscriptions.service.ts`** — service admin per subscriptions. Pattern da seguire per admin invoice requests service.
- **`frontend/web/projects/app/src/app/pages/admin/subscriptions/admin-subscriptions.models.ts`** — modelli admin subscriptions. Import di `PagedResult` da `../users/models/user.models`.
- **`frontend/web/projects/app/src/app/pages/admin/admin-layout.html`** — sidebar admin, per aggiungere link "Richieste fattura".
- **`frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`** — routes admin, per aggiungere rotta `invoice-requests`.
- **`frontend/web/projects/app/src/app/app.routes.ts`** — routes app, per aggiungere rotte user-facing (storico fatture).
- **`frontend/web/projects/shared-auth/src/lib/models/permissions.ts`** — permessi frontend. `Subscriptions.Manage` non è ancora presente nel frontend (aggiunto solo nel backend da T-19).

### Pattern in uso da seguire

- **Componenti standalone** con `imports: [...]` nel decorator `@Component`
- **Signals** (`signal()`, `computed()`) per stato locale — no BehaviorSubject
- **`inject()`** per DI — no constructor injection
- **Angular Material** per tutti i componenti UI (table, card, dialog, form field, button, icon, snackbar, etc.)
- **`providedIn: 'root'`** per i services
- **`AUTH_CONFIG`** injection token per `apiUrl`
- **`DatePipe`**, **`DecimalPipe`** importati esplicitamente nei componenti standalone
- **Dialog pattern**: `MatDialog.open(DialogComponent, { width, data })`, dialog component usa `MAT_DIALOG_DATA`, `MatDialogRef`
- **Paginated admin list**: `MatTableModule`, `MatPaginatorModule`, `FormControl` per filtri, signals per loading/error/data
- **Admin route**: `canActivate: [permissionGuard('...')]`, `loadComponent` lazy
- **Admin sidebar**: `<a mat-list-item routerLink="..." routerLinkActive="active" *hasPermission="...">`

## Piano di esecuzione

### Step 1: Modelli e interfacce

**File da modificare:** `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts`

Aggiungere:
```typescript
export interface InvoiceRequest {
  id: string;
  customerType: string;  // 'Individual' | 'Company'
  fullName: string;
  companyName: string | null;
  address: string;
  city: string;
  postalCode: string;
  country: string;
  fiscalCode: string | null;
  vatNumber: string | null;
  sdiCode: string | null;
  pecEmail: string | null;
  stripePaymentIntentId: string | null;
  status: string;  // 'Requested' | 'InProgress' | 'Issued'
  createdAt: string;
  processedAt: string | null;
}

export interface CreateInvoiceRequest {
  customerType: 'Individual' | 'Company';
  fullName: string;
  companyName?: string;
  address: string;
  city: string;
  postalCode: string;
  country: string;
  fiscalCode?: string;
  vatNumber?: string;
  sdiCode?: string;
  pecEmail?: string;
  stripePaymentIntentId?: string;
}
```

### Step 2: BillingService — aggiungere metodi invoice request

**File da modificare:** `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts`

Aggiungere metodi:
- `createInvoiceRequest(request: CreateInvoiceRequest): Observable<Guid>` → `POST billing/invoice-request`
- `getMyInvoiceRequests(): Observable<InvoiceRequest[]>` → `GET billing/invoice-requests`

### Step 3: Dialog richiesta fattura (user-facing)

**File da creare:**
- `frontend/web/projects/app/src/app/pages/billing/subscription/invoice-request-dialog.ts`

Componente dialog standalone con:
- Toggle `CustomerType`: "Persona fisica" / "Azienda" (mat-button-toggle o mat-radio)
- Campi form con `ReactiveFormsModule` (`FormGroup`):
  - `fullName` (obbligatorio)
  - `companyName` (obbligatorio se Company, hidden se Individual)
  - `address` (obbligatorio)
  - `city` (obbligatorio)
  - `postalCode` (obbligatorio)
  - `country` (obbligatorio, default "Italia")
  - `fiscalCode` (opzionale)
  - `vatNumber` (obbligatorio se Company, opzionale se Individual)
  - `sdiCode` (opzionale)
  - `pecEmail` (opzionale)
- Pre-fill da dati precedente richiesta (passati via `MAT_DIALOG_DATA`)
- Ritorna `CreateInvoiceRequest` on submit, `undefined` on cancel

### Step 4: Storico richieste fattura (user-facing)

**File da creare:**
- `frontend/web/projects/app/src/app/pages/billing/invoice-requests/invoice-requests.ts`
- `frontend/web/projects/app/src/app/pages/billing/invoice-requests/invoice-requests.html`
- `frontend/web/projects/app/src/app/pages/billing/invoice-requests/invoice-requests.scss`

Componente standalone con:
- Tabella `mat-table` con colonne: Data richiesta, Tipo, Nome/Ragione sociale, Stato, Data emissione
- Status badge con chip colorati: Richiesta (default), In lavorazione (warn), Emessa (primary)
- Signal-based: `loading`, `requests`, `error`
- Pulsante "Nuova richiesta" che apre `InvoiceRequestDialog`

### Step 5: Integrazione nel componente Subscription

**File da modificare:**
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.ts`
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.html`

Aggiungere:
- Pulsante "Richiedi fattura" nelle `mat-card-actions` (visibile solo se `isActive()` o subscription non free)
- Il click apre `InvoiceRequestDialog`, pre-filling con dati dell'ultima richiesta (fetch da `getMyInvoiceRequests()`)
- Link "Storico richieste fattura" che naviga a `/billing/invoice-requests`

### Step 6: Rotta user-facing

**File da modificare:** `frontend/web/projects/app/src/app/app.routes.ts`

Aggiungere rotta:
```typescript
{
  path: 'billing/invoice-requests',
  loadComponent: () => import('./pages/billing/invoice-requests/invoice-requests').then(m => m.InvoiceRequests),
  canActivate: [authGuard],
}
```

### Step 7: Admin — Modelli e service invoice requests

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/admin-invoice-requests.models.ts`
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/admin-invoice-requests.service.ts`

Modelli:
```typescript
import { PagedResult } from '../users/models/user.models';
export type { PagedResult };

export interface AdminInvoiceRequest {
  id: string;
  userEmail: string;
  userFullName: string;
  customerType: string;
  fullName: string;
  companyName: string | null;
  address: string;
  city: string;
  postalCode: string;
  country: string;
  fiscalCode: string | null;
  vatNumber: string | null;
  sdiCode: string | null;
  pecEmail: string | null;
  stripePaymentIntentId: string | null;
  status: string;
  createdAt: string;
  processedAt: string | null;
}
```

Service (pattern da `AdminSubscriptionsService`):
- `getInvoiceRequests(params: { pageNumber?, pageSize?, status? }): Observable<PagedResult<AdminInvoiceRequest>>`
- `updateInvoiceRequestStatus(id: string, newStatus: string): Observable<void>` → `PUT admin/invoice-requests/{id}/status`

### Step 8: Admin — Lista richieste fattura

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/invoice-request-list/invoice-request-list.ts`
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/invoice-request-list/invoice-request-list.html`
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/invoice-request-list/invoice-request-list.scss`

Componente con:
- Tabella paginata: User email, Nome/Ragione sociale, Tipo, Stato, Data richiesta, Azioni
- Filtro per status (Richiesta, In lavorazione, Emessa)
- Azione "Aggiorna stato" inline con select (mat-select) per cambiare stato
- Pattern identico a `SubscriptionList` (signals, paginator, filter FormControl)

### Step 9: Admin — Route, sidebar, permessi

**File da modificare:**
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — aggiungere rotta `invoice-requests` con `permissionGuard('Subscriptions.Read')`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — aggiungere link sidebar "Richieste fattura" con icona `receipt_long`, `*hasPermission="permissions.Subscriptions.Read"`
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — aggiungere `Manage: 'Subscriptions.Manage'` nel blocco `Subscriptions`

### Step 10: Verifica build

Eseguire `npm run build` da `frontend/web/` per verificare che non ci siano errori di compilazione.

## Criteri di completamento

(Definition of Done dal piano, copiata verbatim)

- [x] Invoice request form with all fields and conditional rendering
- [x] Fiscal data pre-filled from previous requests
- [x] Request history visible to user
- [x] Admin can view and update request status
- [x] Form validation

## Risultato

### File modificati

- `frontend/web/projects/app/src/app/pages/pricing/billing.models.ts` — aggiunte interfacce `InvoiceRequest` e `CreateInvoiceRequest`
- `frontend/web/projects/app/src/app/pages/pricing/billing.service.ts` — aggiunti metodi `createInvoiceRequest()` e `getMyInvoiceRequests()`
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.ts` — aggiunto `openInvoiceRequest()`, `requestingInvoice` signal, inject `MatSnackBar`, import `InvoiceRequestDialog`
- `frontend/web/projects/app/src/app/pages/billing/subscription/subscription.html` — aggiunti pulsanti "Richiedi fattura" e "Storico fatture" nelle card actions
- `frontend/web/projects/app/src/app/app.routes.ts` — aggiunta rotta `/billing/invoice-requests`
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — aggiunto `Manage: 'Subscriptions.Manage'` nel blocco `Subscriptions`
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — aggiunta rotta `invoice-requests` con guard `Subscriptions.Read`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — aggiunto link sidebar "Richieste fattura" con icona `receipt_long`

### File creati

- `frontend/web/projects/app/src/app/pages/billing/subscription/invoice-request-dialog.ts` — dialog form fattura con toggle Persona fisica/Azienda, validazione condizionale, pre-fill da richiesta precedente
- `frontend/web/projects/app/src/app/pages/billing/invoice-requests/invoice-requests.ts` — componente storico richieste utente
- `frontend/web/projects/app/src/app/pages/billing/invoice-requests/invoice-requests.html` — template tabella storico con status badge
- `frontend/web/projects/app/src/app/pages/billing/invoice-requests/invoice-requests.scss` — stili componente storico
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/admin-invoice-requests.models.ts` — modelli admin (`AdminInvoiceRequest`, re-export `PagedResult`)
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/admin-invoice-requests.service.ts` — service admin con `getInvoiceRequests()` e `updateInvoiceRequestStatus()`
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/invoice-request-list/invoice-request-list.ts` — componente lista admin paginata
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/invoice-request-list/invoice-request-list.html` — template lista admin con mat-select inline per aggiornamento stato
- `frontend/web/projects/app/src/app/pages/admin/invoice-requests/invoice-request-list/invoice-request-list.scss` — stili componente admin

### Scelte chiave

- **Pre-fill nel componente Subscription**: la prima volta che si apre il dialog si chiama `getMyInvoiceRequests()` per recuperare l'ultima richiesta; il risultato viene cachato in `lastInvoiceRequest` per aperture successive senza ulteriori HTTP call.
- **Dialog inline styles**: seguendo la convenzione di `ConfirmCancelDialog`, gli stili del dialog sono inline nel componente (styles array), non in un file .scss separato.
- **Admin status update inline**: l'aggiornamento dello stato è un `mat-select` direttamente nella riga della tabella (non un dialog separato), seguendo il requisito del mini-plan.
- **Route guard admin**: usa `Subscriptions.Read` (non `Subscriptions.Manage`) per la visualizzazione della lista, coerente con il pattern degli altri moduli admin.

### Deviazioni dal mini-plan

Nessuna deviazione sostanziale. Il piano è stato seguito fedelmente.
