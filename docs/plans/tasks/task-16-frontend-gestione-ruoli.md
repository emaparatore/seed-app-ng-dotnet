# Task 16: Frontend — Gestione ruoli

## Contesto

- **Stato attuale:** La route `/admin/roles` esiste in `admin.routes.ts` ma punta a un componente `AdminPlaceholder`. Il backend è completo con tutti gli endpoint CRUD + permissions in `AdminRolesController`.
- **Dipendenze completate:** T-08 (API ruoli), T-13 (layout admin, routing, navigazione)
- **Pattern di riferimento:** La gestione utenti (`pages/admin/users/`) è il template da seguire per struttura, service, modelli, stili e test.

### API Backend disponibili

| Metodo | Endpoint | Permesso | Risposta |
|--------|----------|----------|----------|
| GET | `/api/v1/admin/roles` | Roles.Read | `AdminRoleDto[]` (Id, Name, Description, IsSystemRole, UserCount, CreatedAt) |
| GET | `/api/v1/admin/roles/{id}` | Roles.Read | `AdminRoleDetailDto` (+ Permissions[]) |
| POST | `/api/v1/admin/roles` | Roles.Create | `Guid` |
| PUT | `/api/v1/admin/roles/{id}` | Roles.Update | `bool` |
| DELETE | `/api/v1/admin/roles/{id}` | Roles.Delete | `bool` |
| GET | `/api/v1/admin/roles/permissions` | Roles.Read | `PermissionDto[]` (Id, Name, Description, Category) |

### Vincoli di business logic (backend)
- Non si possono eliminare ruoli di sistema (`IsSystemRole = true`)
- Non si possono eliminare ruoli con utenti assegnati
- Non si possono modificare i permessi del SuperAdmin
- Alla modifica permessi: invalidazione cache e blacklist token

## Piano di esecuzione

### Step 1: Creare modelli TypeScript
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/models/role.models.ts`

Definire:
- `AdminRole` — per la lista (Id, Name, Description, IsSystemRole, UserCount, CreatedAt)
- `AdminRoleDetail` — per il dettaglio (estende AdminRole con Permissions: string[])
- `Permission` — (Id, Name, Description, Category)
- `CreateRoleRequest` — (Name, Description, PermissionNames: string[])
- `UpdateRoleRequest` — (Name, Description, PermissionNames: string[])

### Step 2: Creare il service
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/roles.service.ts`

Metodi:
- `getRoles(): Observable<AdminRole[]>`
- `getRoleById(id: string): Observable<AdminRoleDetail>`
- `createRole(request: CreateRoleRequest): Observable<string>` (restituisce l'ID)
- `updateRole(id: string, request: UpdateRoleRequest): Observable<boolean>`
- `deleteRole(id: string): Observable<boolean>`
- `getPermissions(): Observable<Permission[]>`

Pattern: `inject(HttpClient)`, `AUTH_CONFIG.apiUrl`, stessa struttura di `users.service.ts`.

### Step 3: Creare il componente lista ruoli
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.ts`
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.html`
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.scss`

Funzionalità:
- Tabella Material con colonne: Nome, Descrizione, Utenti, Sistema (badge), Azioni
- Badge "Sistema" per ruoli con `IsSystemRole = true`
- Pulsante "Nuovo ruolo" (visibile solo con permesso `Roles.Create`)
- Pulsante "Modifica" per navigare al dettaglio
- Pulsante "Elimina" disabilitato per ruoli di sistema, con dialog di conferma per gli altri
- Skeleton loading, stato vuoto, gestione errori
- SnackBar per feedback

### Step 4: Creare il componente dettaglio/modifica ruolo
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-detail/role-detail.ts`
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-detail/role-detail.html`
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-detail/role-detail.scss`

Funzionalità:
- Form reattivo con campi: Nome, Descrizione
- Matrice permessi raggruppata per categoria (area): checkbox per ogni permesso
- Checkbox "seleziona tutti" per area
- SuperAdmin: matrice in sola lettura (tutti i checkbox checked e disabilitati)
- Indicazione numero utenti impattati
- Pulsante "Salva" con stato loading
- Navigazione indietro alla lista
- Toast successo/errore
- Skeleton loading durante caricamento

### Step 5: Creare il dialog di creazione ruolo
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/create-role-dialog/create-role-dialog.ts`
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/create-role-dialog/create-role-dialog.html`
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/create-role-dialog/create-role-dialog.scss`

Funzionalità:
- Dialog con form: Nome, Descrizione
- Matrice permessi raggruppata per area (stessa struttura del dettaglio)
- Opzione "Duplica da" — dropdown con ruoli esistenti per pre-popolare i permessi
- Pulsante "Crea" con validazione e stato loading
- Toast successo/errore

### Step 6: Aggiornare le routes
**File da modificare:** `/project/frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`

- Cambiare la route `roles` da placeholder a `role-list`
- Aggiungere route `roles/:id` per il dettaglio con guard `Roles.Read`

### Step 7: Riutilizzare il ConfirmDialog
Riutilizzare il `ConfirmDialog` già esistente in `users/confirm-dialog/` (o spostarlo in una posizione condivisa se opportuno). Valutare se importarlo direttamente.

### Step 8: Scrivere test
**File:** `/project/frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.spec.ts`

Test da scrivere (seguendo il pattern di `user-list.spec.ts`):
- Creazione componente
- Rendering dati nella tabella
- Badge "Sistema" visibile per ruoli di sistema
- Pulsante elimina disabilitato per ruoli di sistema
- Dialog di conferma alla eliminazione
- Pulsante "Nuovo ruolo" visibile/nascosto in base ai permessi
- Stato vuoto quando non ci sono ruoli
- Stato errore
- Skeleton loading

### Step 9: Verificare build e test
```bash
cd frontend/web && ng build app
cd frontend/web && ng test app
```

## Criteri di completamento

- [ ] **Lista ruoli** renderizza correttamente con nome, descrizione, conteggio utenti, badge "Sistema"
- [ ] Pulsante elimina **disabilitato** per ruoli di sistema, con **dialog di conferma** per gli altri
- [ ] **Pagina dettaglio/modifica** con form nome+descrizione e matrice permessi raggruppata per categoria
- [ ] Checkbox **"seleziona tutti"** per ogni area nella matrice permessi
- [ ] **Numero utenti impattati** mostrato nella pagina dettaglio
- [ ] SuperAdmin: matrice permessi in **sola lettura**
- [ ] **Pagina creazione** con opzione "duplica da" per copiare permessi da un ruolo esistente
- [ ] **Toast** per successo/errore e **skeleton loading** durante il caricamento
- [ ] Route aggiornate: `/admin/roles` → lista, `/admin/roles/:id` → dettaglio
- [ ] Permessi controllati: `Roles.Create`, `Roles.Update`, `Roles.Delete` sugli elementi UI appropriati
- [ ] **Build** Angular passa senza errori
- [ ] **Test** del componente lista passano

## Risultato

### File creati
- `frontend/web/projects/app/src/app/pages/admin/roles/models/role.models.ts` — Modelli TypeScript (AdminRole, AdminRoleDetail, Permission, CreateRoleRequest, UpdateRoleRequest)
- `frontend/web/projects/app/src/app/pages/admin/roles/roles.service.ts` — Service con metodi CRUD + getPermissions
- `frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.ts` — Componente lista ruoli con tabella Material
- `frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.html` — Template lista con skeleton, empty state, error state
- `frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.scss` — Stili lista (badge sistema, skeleton, stati)
- `frontend/web/projects/app/src/app/pages/admin/roles/role-detail/role-detail.ts` — Componente dettaglio/modifica con matrice permessi
- `frontend/web/projects/app/src/app/pages/admin/roles/role-detail/role-detail.html` — Template dettaglio con form, info, matrice permessi raggruppata
- `frontend/web/projects/app/src/app/pages/admin/roles/role-detail/role-detail.scss` — Stili dettaglio (header, grid, permessi, skeleton)
- `frontend/web/projects/app/src/app/pages/admin/roles/create-role-dialog/create-role-dialog.ts` — Dialog creazione ruolo con "duplica da"
- `frontend/web/projects/app/src/app/pages/admin/roles/create-role-dialog/create-role-dialog.html` — Template dialog con form e matrice permessi
- `frontend/web/projects/app/src/app/pages/admin/roles/create-role-dialog/create-role-dialog.scss` — Stili dialog
- `frontend/web/projects/app/src/app/pages/admin/roles/role-list/role-list.spec.ts` — 9 test (creazione, rendering tabella, badge sistema, delete disabilitato, permessi, stati vuoto/errore/loading)

### File modificati
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Route `roles` cambiata da placeholder a children con role-list e role-detail/:id

### Scelte implementative e motivazioni
- **Pattern replicato da users/**: Struttura directory, service con inject(HttpClient) + AUTH_CONFIG, componenti con signals, stili Material identici
- **ConfirmDialog importato da users/**: Il ConfirmDialog è stato riutilizzato direttamente da `users/confirm-dialog/` anziché spostarlo in una posizione condivisa, per minimizzare le modifiche ai file esistenti e mantenere il focus sul task
- **Matrice permessi con computed signal**: I permessi sono raggruppati per categoria usando un `computed()` che reagisce al signal `allPermissions`, con checkbox "seleziona tutti" per gruppo e stato indeterminate
- **SuperAdmin in sola lettura**: Controllo basato su `role.name === 'SuperAdmin'` che disabilita tutti i checkbox della matrice e nasconde il pulsante salva
- **Eliminazione ruoli con utenti**: Bloccata client-side con snackbar di avviso prima ancora di aprire il dialog di conferma
- **"Duplica da" nel dialog creazione**: Carica i permessi del ruolo selezionato via `getRoleById()` e li pre-popola nel set di permessi selezionati

### Deviazioni dal piano
- **Step 7 (ConfirmDialog)**: Il piano suggeriva di valutare se spostarlo in posizione condivisa. Si è scelto di importarlo direttamente da `users/confirm-dialog/` per semplicità e per non modificare i file della gestione utenti già funzionanti
- **Nessuna paginazione nella lista ruoli**: A differenza della lista utenti, la lista ruoli non ha paginazione né filtri avanzati perché l'API backend restituisce un array semplice (non paginato) — il numero di ruoli in un sistema è tipicamente limitato
