# Task 15: Frontend — Gestione utenti

## Contesto

- **Backend pronto:** `AdminUsersController` con 9 endpoint (GET list, GET by id, POST create, PUT update, DELETE, PUT status, PUT roles, POST force-password-change, POST reset-password) — tutti protetti da permessi
- **DTOs backend:** `AdminUserDto` (lista: Id, Email, FirstName, LastName, IsActive, Roles, CreatedAt) e `AdminUserDetailDto` (dettaglio: + UpdatedAt, MustChangePassword, EmailConfirmed)
- **Paginazione:** `PagedResult<T>` con Items, PageNumber, PageSize, TotalCount, TotalPages, HasPreviousPage, HasNextPage
- **Query params:** SearchTerm, RoleFilter, StatusFilter, DateFrom, DateTo, SortBy, SortDescending
- **Layout admin già implementato** (T-13): sidebar con routing, guards, `HasPermissionDirective`, `PermissionService`
- **Pattern di riferimento:** Dashboard component con signals (loading/data/error), `AdminDashboardService` con `AUTH_CONFIG` injection
- **Rotta attuale:** `/admin/users` punta ad `AdminPlaceholder` — da sostituire
- **Permessi frontend:** `PERMISSIONS.Users.Read/Create/Update/Delete/ToggleStatus/AssignRoles`
- **Angular Material** disponibile (MatTableModule, MatPaginatorModule, MatSortModule, MatDialogModule, MatChipsModule, etc.)

## Piano di esecuzione

### Step 1: Modelli e servizio API

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/users/models/user.models.ts`
```
- AdminUser interface (mirrors AdminUserDto)
- AdminUserDetail interface (mirrors AdminUserDetailDto)
- PagedResult<T> generic interface
- CreateUserRequest, UpdateUserRequest, ToggleStatusRequest, AssignRolesRequest
```

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/users/users.service.ts`
```
- AdminUsersService (providedIn: 'root')
- Inject HttpClient + AUTH_CONFIG (same pattern as AdminDashboardService)
- getUsers(params): Observable<PagedResult<AdminUser>>
- getUserById(id): Observable<AdminUserDetail>
- createUser(request): Observable<{id: string}>
- updateUser(id, request): Observable<void>
- deleteUser(id): Observable<void>
- toggleUserStatus(id, isActive): Observable<void>
- assignRoles(id, roleNames): Observable<void>
- forcePasswordChange(id): Observable<void>
- resetPassword(id): Observable<void>
```

### Step 2: Lista utenti (componente principale)

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.ts`
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.html`
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.scss`

**Funzionalità:**
- Mat-table con colonne: avatar/iniziali + nome, email, ruoli (mat-chip), stato (badge), data registrazione, azioni
- MatPaginator per paginazione server-side (pageIndex, pageSize → query params)
- MatSort per ordinamento server-side
- Campo ricerca con debounce (300ms)
- Filtri: dropdown ruolo, dropdown stato (tutti/attivi/disattivati), date range (date picker from/to)
- Pulsanti azione per riga: toggle status (se Users.ToggleStatus), elimina (se Users.Delete)
- Pulsante "Nuovo utente" in header (se Users.Create)
- Signal-based state: loading, users, error, totalCount
- Skeleton loading durante il caricamento
- Stato vuoto quando non ci sono risultati

### Step 3: Dialog conferma eliminazione

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/users/confirm-dialog/confirm-dialog.ts`

**Funzionalità:**
- Dialog Material generico riutilizzabile
- Title, message, confirmText, cancelText come input data
- Ritorna true/false al close

### Step 4: Dialog creazione utente

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/users/create-user-dialog/create-user-dialog.ts`
- `frontend/web/projects/app/src/app/pages/admin/users/create-user-dialog/create-user-dialog.html`

**Funzionalità:**
- Dialog Material con form: firstName, lastName, email, password (con pulsante auto-genera), selezione ruoli (multi-select o chips)
- Validazione: required fields, email format, password strength
- Alla conferma chiama createUser e chiude il dialog con il risultato
- Necessita lista ruoli → aggiungere `getRoles()` al service (GET /admin/roles per avere la lista)

### Step 5: Pagina dettaglio/modifica utente

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.ts`
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.html`
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.scss`

**Funzionalità:**
- Rotta: `/admin/users/:id`
- Carica dettaglio utente tramite getUserById
- Form con: firstName, lastName, email (editabili se Users.Update)
- Sezione ruoli: lista con add/remove (se Users.AssignRoles) — usa multi-select con lista ruoli disponibili
- Info account (sola lettura): data creazione, ultimo aggiornamento, email confermata, must change password
- Pulsanti azione (visibili in base ai permessi):
  - Salva modifiche (Users.Update)
  - Forza cambio password (Users.Update)
  - Reset password (Users.Update)
  - Toggle stato (Users.ToggleStatus)
  - Elimina (Users.Delete) con confirm dialog
- Toast (MatSnackBar) per successo/errore
- Skeleton loading
- Navigazione back alla lista

### Step 6: Aggiornamento routing

**File da modificare:** `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`
```
- Cambiare rotta 'users' da placeholder a child routes:
  - '' → UserList component
  - ':id' → UserDetail component
  - Entrambe con permissionGuard('Users.Read')
```

### Step 7: Test

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.spec.ts`
**File da creare:** `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.spec.ts`

**Test cases per UserList:**
- Renders table with users data
- Shows loading skeleton initially
- Calls service with pagination params on page change
- Calls service with search term on search input
- Shows create button only with Users.Create permission
- Shows delete button only with Users.Delete permission
- Opens confirm dialog on delete click
- Shows empty state when no results

**Test cases per UserDetail:**
- Loads and displays user details
- Shows edit form fields
- Saves user changes on submit
- Shows action buttons based on permissions
- Navigates back on cancel/back

### Step 8: Build verification

- `ng build app` compila senza errori
- `ng test app` tutti i test passano

## Criteri di completamento

- [ ] Lista utenti con tabella Material paginata server-side, ricerca, filtri, ordinamento
- [ ] Colonne: nome con iniziali, email, ruoli (chip), stato (badge), data registrazione, azioni
- [ ] Toggle status inline funzionante (con permesso)
- [ ] Elimina con dialog di conferma funzionante (con permesso)
- [ ] Pagina dettaglio/modifica con form e azioni (salva, reset pwd, forza cambio pwd, toggle status, elimina)
- [ ] Dialog creazione utente con validazione e selezione ruoli
- [ ] Pulsanti e azioni visibili solo con i permessi corretti (HasPermissionDirective / PermissionService)
- [ ] Toast di successo/errore per ogni operazione
- [ ] Skeleton loading per lista e dettaglio
- [ ] Stato vuoto quando nessun risultato
- [ ] Routing aggiornato: `/admin/users` → lista, `/admin/users/:id` → dettaglio
- [ ] Test passano (`ng test app`)
- [ ] Build compila (`ng build app`)

## Risultato

### File creati
- `frontend/web/projects/app/src/app/pages/admin/users/models/user.models.ts` — Interfacce TypeScript per AdminUser, AdminUserDetail, PagedResult<T>, request DTOs, GetUsersParams
- `frontend/web/projects/app/src/app/pages/admin/users/users.service.ts` — AdminUsersService con tutti i 9 endpoint + getRoles()
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.ts` — Componente lista utenti con tabella Material, paginazione server-side, ricerca con debounce, filtri, ordinamento
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.html` — Template lista utenti
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.scss` — Stili lista utenti con skeleton loading, empty state, error state
- `frontend/web/projects/app/src/app/pages/admin/users/user-list/user-list.spec.ts` — 9 test per la lista utenti
- `frontend/web/projects/app/src/app/pages/admin/users/confirm-dialog/confirm-dialog.ts` — Dialog conferma generico riutilizzabile
- `frontend/web/projects/app/src/app/pages/admin/users/create-user-dialog/create-user-dialog.ts` — Dialog creazione utente con form validato e generazione password
- `frontend/web/projects/app/src/app/pages/admin/users/create-user-dialog/create-user-dialog.html` — Template dialog creazione utente
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.ts` — Pagina dettaglio/modifica utente con tutte le azioni
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.html` — Template dettaglio utente
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.scss` — Stili dettaglio utente
- `frontend/web/projects/app/src/app/pages/admin/users/user-detail/user-detail.spec.ts` — 7 test per il dettaglio utente

### File modificati
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Sostituita rotta placeholder con child routes per lista e dettaglio utente

### Scelte implementative e motivazioni
- **Signal-based state management** — Segue il pattern della Dashboard esistente: `signal()` per loading, data, error
- **Server-side pagination/sort/filter** — Tutti i parametri di ricerca vengono passati come query params al backend, nessun filtraggio client-side
- **Debounce 300ms sulla ricerca** — Evita chiamate API eccessive durante la digitazione
- **ConfirmDialog generico** — Riutilizzabile per qualsiasi conferma (eliminazione, toggle status, forza password, reset password)
- **CreateUserDialog con generazione password** — Genera password sicure di 16 caratteri con caratteri alfanumerici + speciali, escludendo caratteri ambigui (0, O, l, 1)
- **Permessi controllati sia via HasPermissionDirective (template) che PermissionService (logica)** — Pulsanti nascosti se l'utente non ha il permesso corrispondente
- **getRoles() nel service** — Necessario per il multi-select dei ruoli nella creazione e nel dettaglio utente; chiama GET /admin/roles
- **MatSnackBar per toast** — Feedback visivo per successo/errore di ogni operazione
- **event.stopPropagation()** sui pulsanti azione nella tabella — Previene la navigazione al dettaglio quando si clicca su toggle status o elimina
- **Routing con children** — `/admin/users` carica UserList, `/admin/users/:id` carica UserDetail, entrambi protetti da permissionGuard('Users.Read')

### Deviazioni dal piano
- Nessuna deviazione significativa. Tutti gli step del mini-plan sono stati implementati come descritto.
