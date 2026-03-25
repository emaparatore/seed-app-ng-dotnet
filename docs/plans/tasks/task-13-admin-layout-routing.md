# Task 13: Layout admin, routing e navigazione condizionale

## Contesto

### Stato attuale del codice rilevante

**Routing (`projects/app/src/app/app.routes.ts`):**
- Route esistenti usano `loadComponent()` con lazy loading di standalone component
- Guards funzionali: `authGuard`, `guestGuard`, `mustChangePasswordGuard`
- Non esiste ancora nessuna rotta `/admin`

**Auth state (`projects/shared-auth/src/lib/services/auth.service.ts`):**
- `permissions` signal (`signal<string[]>`) già presente — popolato da `AuthResponse.permissions`
- Nessun metodo `hasPermission()` — da aggiungere
- `isAuthenticated` computed signal disponibile

**Layout corrente (`projects/app/src/app/app.ts` + `app.html`):**
- `<mat-toolbar>` sticky con logo, brand, link utente/logout o login/register
- `<router-outlet />` per il contenuto
- Nessuna sidebar, nessun link "Admin"

**Auth models (`projects/shared-auth/src/lib/models/auth.models.ts`):**
- `AuthResponse` include `permissions: string[]` e `mustChangePassword: boolean`
- `User` include `roles: string[]`

**Backend permissions (`Seed.Domain/Authorization/Permissions.cs`):**
16 permessi in 6 aree: Users (6), Roles (4), AuditLog (2), Settings (2), Dashboard (1), SystemHealth (1)

**Public API (`projects/shared-auth/src/public-api.ts`):**
Esporta: `auth.config`, `auth.models`, `auth.service`, `auth.interceptor`, 3 guards, `auth-initializer.provider`

### Dipendenze e vincoli
- Dipende da T-03 (infrastruttura autorizzazione) — completato
- I permessi arrivano già nella risposta login e sono salvati nel signal `permissions`
- Angular Material 21 con Material 3 theme già configurato (azure/blue palette)
- Pattern: standalone components, signals, lazy loading, functional guards
- Le sotto-pagine admin (T-14..T-20) verranno implementate nei task successivi — qui si creano solo placeholder/route

## Piano di esecuzione

### Step 1: PermissionService in shared-auth

**File da creare:** `frontend/web/projects/shared-auth/src/lib/services/permission.service.ts`

- Injectable service con `inject(AuthService)`
- `permissions = this.authService.permissions` — signal readonly
- `hasPermission(permission: string): boolean` — legge dal signal
- `hasAnyPermission(permissions: string[]): boolean` — almeno uno
- `isAdmin: Signal<boolean>` — computed: ha almeno un permesso admin

**File da modificare:** `frontend/web/projects/shared-auth/src/public-api.ts`
- Aggiungere export di `PermissionService`

### Step 2: Costanti permessi frontend

**File da creare:** `frontend/web/projects/shared-auth/src/lib/models/permissions.ts`

- Oggetto costante `PERMISSIONS` che replica le costanti backend:
  ```typescript
  export const PERMISSIONS = {
    Users: { Read: 'Users.Read', Create: 'Users.Create', ... },
    Roles: { Read: 'Roles.Read', ... },
    AuditLog: { Read: 'AuditLog.Read', Export: 'AuditLog.Export' },
    Settings: { Read: 'Settings.Read', Manage: 'Settings.Manage' },
    Dashboard: { ViewStats: 'Dashboard.ViewStats' },
    SystemHealth: { Read: 'SystemHealth.Read' },
  } as const;
  ```
- Esportare da `public-api.ts`

### Step 3: Guards admin

**File da creare:** `frontend/web/projects/shared-auth/src/lib/guards/admin.guard.ts`

- `adminGuard: CanActivateFn` — verifica `permissionService.isAdmin()`, redirect a `/` se false

**File da creare:** `frontend/web/projects/shared-auth/src/lib/guards/permission.guard.ts`

- `permissionGuard(permission: string): CanActivateFn` — factory function che restituisce un guard
- Verifica `permissionService.hasPermission(permission)`, redirect a `/admin` se false

**File da modificare:** `frontend/web/projects/shared-auth/src/public-api.ts`
- Aggiungere export dei nuovi guards

### Step 4: Direttiva HasPermission

**File da creare:** `frontend/web/projects/shared-auth/src/lib/directives/has-permission.directive.ts`

- Structural directive `*hasPermission="'Users.Read'"`
- Inject `PermissionService`, usa `effect()` per reagire ai cambiamenti
- Crea/distrugge il template in base al risultato di `hasPermission()`

**File da modificare:** `frontend/web/projects/shared-auth/src/public-api.ts`
- Aggiungere export della direttiva

### Step 5: Admin layout component

**File da creare:**
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.ts`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html`
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.scss`

**Struttura:**
- Standalone component con `<router-outlet />`
- Sidebar con `mat-nav-list` (Material `MatListModule`)
- Voci sidebar condizionate con `*hasPermission`:
  - Dashboard → `Dashboard.ViewStats`
  - Utenti → `Users.Read`
  - Ruoli → `Roles.Read`
  - Audit Log → `AuditLog.Read`
  - Impostazioni → `Settings.Read`
  - Stato Sistema → `SystemHealth.Read`
- Content area flessibile a fianco della sidebar
- Layout responsive: sidebar collassabile su mobile (opzionale, minimo sidebar fissa)
- Usa `MatSidenavModule` o layout flex/grid custom con `MatListModule`

### Step 6: Placeholder component per le sotto-pagine

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/placeholder.ts`

- Componente generico temporaneo con titolo configurabile via route data
- Usato come placeholder per le pagine admin non ancora implementate (T-14..T-20)

### Step 7: Admin routes

**File da creare:** `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`

```typescript
export const adminRoutes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', loadComponent: ..., canActivate: [permissionGuard('Dashboard.ViewStats')] },
  { path: 'users', loadComponent: ..., canActivate: [permissionGuard('Users.Read')] },
  { path: 'roles', loadComponent: ..., canActivate: [permissionGuard('Roles.Read')] },
  { path: 'audit-log', loadComponent: ..., canActivate: [permissionGuard('AuditLog.Read')] },
  { path: 'settings', loadComponent: ..., canActivate: [permissionGuard('Settings.Read')] },
  { path: 'system-health', loadComponent: ..., canActivate: [permissionGuard('SystemHealth.Read')] },
];
```

**File da modificare:** `frontend/web/projects/app/src/app/app.routes.ts`

- Aggiungere rotta admin:
  ```typescript
  {
    path: 'admin',
    loadComponent: () => import('./pages/admin/admin-layout').then(m => m.AdminLayout),
    canActivate: [authGuard, adminGuard],
    loadChildren: () => import('./pages/admin/admin.routes').then(m => m.adminRoutes),
  }
  ```

### Step 8: Link "Admin" nella navbar

**File da modificare:**
- `frontend/web/projects/app/src/app/app.ts` — inject `PermissionService`
- `frontend/web/projects/app/src/app/app.html` — aggiungere link "Admin" condizionato su `permissionService.isAdmin()`

### Step 9: Test

**File da creare:**
- `frontend/web/projects/shared-auth/src/lib/services/permission.service.spec.ts`
  - Test: `hasPermission` restituisce true/false correttamente
  - Test: `hasAnyPermission` con mix di permessi
  - Test: `isAdmin` computed è true quando ha almeno un permesso admin, false altrimenti
- `frontend/web/projects/shared-auth/src/lib/guards/admin.guard.spec.ts`
  - Test: permette accesso se isAdmin è true
  - Test: redirect a `/` se isAdmin è false
- `frontend/web/projects/shared-auth/src/lib/guards/permission.guard.spec.ts`
  - Test: permette accesso con permesso corretto
  - Test: redirect se permesso mancante
- `frontend/web/projects/shared-auth/src/lib/directives/has-permission.directive.spec.ts`
  - Test: mostra contenuto se ha permesso
  - Test: nasconde contenuto se non ha permesso

**Verifiche finali:**
- `npm test` da `frontend/web/` — tutti i test passano
- `npm run build` da `frontend/web/` — build OK
- Navigazione manuale: rotta `/admin` carica il layout, sidebar visibile, placeholder nelle sotto-rotte

## Criteri di completamento

1. **PermissionService** espone `hasPermission()`, `hasAnyPermission()`, `isAdmin` come signal — esportato da shared-auth
2. **Costanti PERMISSIONS** replicate nel frontend — esportate da shared-auth
3. **adminGuard** blocca utenti senza permessi admin e redirige a `/`
4. **permissionGuard** factory blocca utenti senza il permesso specifico e redirige a `/admin`
5. **HasPermissionDirective** mostra/nasconde elementi in base al permesso
6. **Admin layout** con sidebar funzionante, voci condizionate per permesso
7. **Rotte admin** lazy-loaded con placeholder per ogni sotto-pagina
8. **Link "Admin"** visibile nella navbar principale solo per utenti con almeno un permesso admin
9. **Test** per PermissionService, adminGuard, permissionGuard, HasPermissionDirective — tutti passano
10. **Build** frontend OK senza errori

## Risultato

### File creati
- `frontend/web/projects/shared-auth/src/lib/services/permission.service.ts` — PermissionService con `hasPermission()`, `hasAnyPermission()`, `isAdmin` computed signal
- `frontend/web/projects/shared-auth/src/lib/models/permissions.ts` — Costanti `PERMISSIONS` che replicano il backend
- `frontend/web/projects/shared-auth/src/lib/guards/admin.guard.ts` — Guard che verifica `isAdmin`, redirect a `/`
- `frontend/web/projects/shared-auth/src/lib/guards/permission.guard.ts` — Factory guard per permesso specifico, redirect a `/admin`
- `frontend/web/projects/shared-auth/src/lib/directives/has-permission.directive.ts` — Structural directive `*hasPermission` con `effect()` e signal input
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.ts` — Layout admin con sidebar e router-outlet
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.html` — Template con mat-nav-list e voci condizionate per permesso
- `frontend/web/projects/app/src/app/pages/admin/admin-layout.scss` — Stili sidebar fissa 240px con flexbox
- `frontend/web/projects/app/src/app/pages/admin/placeholder.ts` — Componente placeholder con titolo da route data
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Rotte admin con lazy loading e permissionGuard per ogni sotto-pagina
- `frontend/web/projects/shared-auth/src/lib/services/permission.service.spec.ts` — 8 test per PermissionService
- `frontend/web/projects/shared-auth/src/lib/guards/admin.guard.spec.ts` — 2 test per adminGuard
- `frontend/web/projects/shared-auth/src/lib/guards/permission.guard.spec.ts` — 2 test per permissionGuard
- `frontend/web/projects/shared-auth/src/lib/directives/has-permission.directive.spec.ts` — 2 test per HasPermissionDirective

### File modificati
- `frontend/web/projects/shared-auth/src/public-api.ts` — Aggiunti export per PermissionService, PERMISSIONS, adminGuard, permissionGuard, HasPermissionDirective
- `frontend/web/projects/app/src/app/app.routes.ts` — Aggiunta rotta `/admin` con `authGuard` + `adminGuard` e `loadChildren`
- `frontend/web/projects/app/src/app/app.ts` — Aggiunto inject di `PermissionService`
- `frontend/web/projects/app/src/app/app.html` — Aggiunto link "Admin" condizionato su `permissionService.isAdmin()`

### Scelte implementative e motivazioni
- **`isAdmin` = ha almeno un permesso**: definito come `permissions().length > 0` — un utente è "admin" se ha qualsiasi permesso admin, coerente con il fatto che i permessi vengono assegnati solo a ruoli admin
- **HasPermissionDirective con signal input**: usa `input.required<string>()` e `effect()` per reagire ai cambiamenti di permesso in modo reattivo (pattern Angular moderno)
- **permissionGuard come factory function**: restituisce un `CanActivateFn`, permettendo di passare il permesso richiesto come parametro nella definizione delle rotte
- **Sidebar fissa 240px**: scelta minimalista per la sidebar, layout flex semplice senza `MatSidenavModule` (non necessario per una sidebar sempre visibile)
- **Placeholder unico**: un singolo componente `AdminPlaceholder` con titolo da `route.data['title']` invece di 6 componenti separati

### Deviazioni dal piano
- Nessuna deviazione significativa dal piano
