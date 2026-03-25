# Admin Dashboard

Area amministrativa con gestione utenti, ruoli, impostazioni di sistema, audit log e monitoraggio dello stato del sistema. L'accesso è controllato da un sistema RBAC (Role-Based Access Control) con permessi granulari.

## Indice

- [Configurazione iniziale](#configurazione-iniziale)
- [Sistema di permessi (RBAC)](#sistema-di-permessi-rbac)
- [SuperAdmin seeding](#superadmin-seeding)
- [Cambio password obbligatorio](#cambio-password-obbligatorio)
- [Gestione utenti](#gestione-utenti)
- [Gestione ruoli](#gestione-ruoli)
- [Audit log](#audit-log)
- [Impostazioni di sistema](#impostazioni-di-sistema)
- [Dashboard di riepilogo](#dashboard-di-riepilogo)
- [Stato del sistema](#stato-del-sistema)
- [Navigazione e accesso condizionale](#navigazione-e-accesso-condizionale)
- [Architettura tecnica](#architettura-tecnica)

---

## Configurazione iniziale

### 1. Variabili d'ambiente per il SuperAdmin

Al primo avvio, il sistema crea automaticamente un utente SuperAdmin. Le credenziali vengono lette dalla configurazione.

**Con Docker** (consigliato): configurare nel file `docker/.env`:

```env
SuperAdmin__Email=admin@seedapp.local
SuperAdmin__Password=Admin123!
SuperAdmin__FirstName=Super
SuperAdmin__LastName=Admin
```

Copiare da `.env.example` se non esiste:

```bash
cd docker
cp .env.example .env
# Modificare le credenziali SuperAdmin nel file .env
```

**Senza Docker**: configurare in `backend/src/Seed.Api/appsettings.Development.json` (già preconfigurato per lo sviluppo locale):

```json
{
  "SuperAdmin": {
    "Email": "admin@seedapp.local",
    "Password": "Admin123!",
    "FirstName": "Super",
    "LastName": "Admin"
  }
}
```

In alternativa, usare variabili d'ambiente:

```bash
export SuperAdmin__Email=admin@seedapp.local
export SuperAdmin__Password=Admin123!
export SuperAdmin__FirstName=Super
export SuperAdmin__LastName=Admin
```

### 2. Avvio

Avviare l'applicazione normalmente. All'avvio, in ordine:

1. Le migration EF Core vengono applicate (`MigrateAsync`)
2. Il seeder crea i 16 permessi e i 3 ruoli di sistema (SuperAdmin, Admin, User)
3. Il seeder crea l'utente SuperAdmin (se non esiste già)
4. Il seeder crea le impostazioni di sistema con i valori di default

Tutti i seeder sono idempotenti: esecuzioni ripetute non creano duplicati.

### 3. Primo accesso

1. Accedere con le credenziali SuperAdmin configurate
2. Il sistema obbliga al cambio password (flag `MustChangePassword` attivo)
3. Dopo il cambio password, si ha accesso completo all'area admin

### 4. Produzione

Nel file `.env` usato dal deploy (letto da `docker-compose.deploy.yml`):

```env
SuperAdmin__Email=<email-reale>
SuperAdmin__Password=<password-forte>
SuperAdmin__FirstName=Super
SuperAdmin__LastName=Admin
```

> **Importante:** configurare il file `.env` sul server **prima** di eseguire il deploy. In produzione il seeder viene eseguito come step esplicito del deploy, dopo le migration e prima del riavvio dell'API. Se le variabili non sono presenti, non viene creato nessun SuperAdmin (solo un warning nei log). Dopo il primo deploy e la verifica dell'accesso, rimuovere la variabile `SuperAdmin__Password` dal file `.env` per sicurezza. Il seeder non sovrascrive un SuperAdmin esistente.

---

## Sistema di permessi (RBAC)

### Catalogo permessi

Il sistema definisce 16 permessi atomici nel formato `Risorsa.Azione`:

| Area | Permesso | Descrizione |
|------|----------|-------------|
| Utenti | `Users.Read` | Visualizzare lista e dettaglio utenti |
| Utenti | `Users.Create` | Creare un utente manualmente |
| Utenti | `Users.Update` | Modificare i dati di un utente |
| Utenti | `Users.Delete` | Eliminare un utente |
| Utenti | `Users.AssignRoles` | Assegnare/rimuovere ruoli a un utente |
| Utenti | `Users.ToggleStatus` | Attivare/disattivare un account |
| Ruoli | `Roles.Read` | Visualizzare ruoli e permessi associati |
| Ruoli | `Roles.Create` | Creare un ruolo personalizzato |
| Ruoli | `Roles.Update` | Modificare un ruolo e i suoi permessi |
| Ruoli | `Roles.Delete` | Eliminare un ruolo non di sistema |
| Audit | `AuditLog.Read` | Consultare il registro attività |
| Audit | `AuditLog.Export` | Esportare il registro in CSV |
| Impostazioni | `Settings.Read` | Visualizzare le impostazioni di sistema |
| Impostazioni | `Settings.Manage` | Modificare le impostazioni di sistema |
| Dashboard | `Dashboard.ViewStats` | Visualizzare statistiche di riepilogo |
| Sistema | `SystemHealth.Read` | Visualizzare lo stato di salute dell'app |

I permessi sono definiti centralmente in `Seed.Domain/Authorization/Permissions.cs` e non sono modificabili dall'utente.

### Ruoli di sistema

| Ruolo | Permessi | Note |
|-------|----------|------|
| **SuperAdmin** | Tutti (bypass completo) | Non eliminabile, non disattivabile |
| **Admin** | Tutti tranne `Settings.Manage` e `Roles.Delete` | Non eliminabile |
| **User** | Nessun permesso admin | Accesso solo alla parte pubblica |

I ruoli di sistema hanno il flag `IsSystemRole = true` e non possono essere eliminati. È possibile creare ruoli personalizzati con qualsiasi combinazione di permessi.

### Come funziona l'autorizzazione

1. **Login**: la risposta include l'array `permissions` con tutti i permessi dell'utente (unione dei permessi di tutti i ruoli assegnati)
2. **Frontend**: i permessi sono cachati nel client e usati per mostrare/nascondere elementi UI tramite la direttiva `*hasPermission`
3. **Backend**: ogni endpoint admin è protetto dall'attributo `[HasPermission("NomePermesso")]`. Un authorization handler verifica il permesso tramite `IPermissionService` (cache server-side con TTL 5 minuti)
4. **SuperAdmin bypass**: il ruolo SuperAdmin bypassa tutti i controlli di permesso automaticamente

### Invalidazione token

Quando un utente viene disattivato o i suoi ruoli cambiano, i token JWT attivi vengono invalidati immediatamente tramite un meccanismo di blacklist basato su `IDistributedCache` (in-memory, sostituibile con Redis). L'utente viene forzato a ri-autenticarsi con i nuovi permessi.

---

## SuperAdmin seeding

Il seeder `SuperAdminSeeder` crea l'utente iniziale al primo avvio:

- Legge la configurazione dalla sezione `SuperAdmin` (env vars o appsettings)
- Se non esiste nessun utente con ruolo SuperAdmin, ne crea uno
- Se un SuperAdmin esiste già, l'operazione viene saltata silenziosamente
- Se le variabili d'ambiente non sono configurate, logga un warning e non crea nulla
- L'utente viene creato con `MustChangePassword = true`
- L'evento viene registrato nell'audit log come operazione di sistema

**File coinvolti:**
- `Seed.Shared/Configuration/SuperAdminSettings.cs` — classe di configurazione
- `Seed.Infrastructure/Persistence/Seeders/SuperAdminSeeder.cs` — logica di seeding

---

## Cambio password obbligatorio

Gli utenti con il flag `MustChangePassword = true` sono obbligati a cambiare password prima di poter usare l'applicazione.

**Flusso:**

1. Dopo il login, se `mustChangePassword` è `true`, il frontend redirige a `/change-password`
2. Un guard (`mustChangePasswordGuard`) impedisce la navigazione verso altre pagine
3. Un middleware backend (`MustChangePasswordMiddleware`) restituisce `403 PASSWORD_CHANGE_REQUIRED` per qualsiasi richiesta API diversa da cambio password e logout
4. Dopo il cambio password, il flag viene rimosso e la navigazione è libera

**Endpoint:** `POST /api/v1/auth/change-password`

```json
{
  "currentPassword": "vecchia-password",
  "newPassword": "nuova-password"
}
```

**Quando viene attivato il flag:**
- Alla creazione del SuperAdmin (seeding)
- Alla creazione manuale di un utente dall'area admin
- Quando un admin forza il cambio password su un utente

---

## Gestione utenti

Accessibile da `/admin/users`. Richiede il permesso `Users.Read` per la visualizzazione.

### API Endpoints

| Metodo | Endpoint | Permesso | Descrizione |
|--------|----------|----------|-------------|
| GET | `/api/v1/admin/users` | `Users.Read` | Lista paginata con filtri |
| GET | `/api/v1/admin/users/{id}` | `Users.Read` | Dettaglio utente |
| POST | `/api/v1/admin/users` | `Users.Create` | Creazione utente |
| PUT | `/api/v1/admin/users/{id}` | `Users.Update` | Modifica dati utente |
| DELETE | `/api/v1/admin/users/{id}` | `Users.Delete` | Eliminazione utente (soft delete) |
| PUT | `/api/v1/admin/users/{id}/status` | `Users.ToggleStatus` | Toggle attivo/disattivo |
| PUT | `/api/v1/admin/users/{id}/roles` | `Users.AssignRoles` | Assegnazione ruoli |
| POST | `/api/v1/admin/users/{id}/force-password-change` | `Users.Update` | Forza cambio password |
| POST | `/api/v1/admin/users/{id}/reset-password` | `Users.Update` | Invia link reset password |

### Funzionalità

- **Lista**: tabella paginata lato server con ricerca per nome/email, filtri per ruolo/stato/periodo, ordinamento per qualsiasi colonna
- **Creazione**: form con nome, cognome, email, password (con auto-generazione), selezione ruoli iniziali. Il flag `MustChangePassword` è sempre attivo
- **Modifica**: dati personali, gestione ruoli, azioni su password
- **Eliminazione**: soft delete (flag `IsDeleted`), con dialog di conferma. L'utente non è più visibile ma i dati restano per audit
- **Toggle stato**: attivazione/disattivazione inline dalla lista. L'utente disattivato non può effettuare il login

### Protezioni

- Non è possibile eliminare o disattivare il SuperAdmin originale
- Non è possibile eliminare se stessi
- Non è possibile auto-modificare il proprio ruolo SuperAdmin
- Alla disattivazione: invalidazione immediata dei token attivi

---

## Gestione ruoli

Accessibile da `/admin/roles`. Richiede il permesso `Roles.Read` per la visualizzazione.

### API Endpoints

| Metodo | Endpoint | Permesso | Descrizione |
|--------|----------|----------|-------------|
| GET | `/api/v1/admin/roles` | `Roles.Read` | Lista ruoli con conteggio utenti |
| GET | `/api/v1/admin/roles/{id}` | `Roles.Read` | Dettaglio ruolo con permessi |
| POST | `/api/v1/admin/roles` | `Roles.Create` | Creazione ruolo |
| PUT | `/api/v1/admin/roles/{id}` | `Roles.Update` | Modifica ruolo e permessi |
| DELETE | `/api/v1/admin/roles/{id}` | `Roles.Delete` | Eliminazione ruolo |
| GET | `/api/v1/admin/permissions` | `Roles.Read` | Lista permessi disponibili |

### Funzionalità

- **Lista**: nome, descrizione, numero utenti, badge "Sistema" per ruoli non eliminabili
- **Creazione**: form con nome, descrizione e matrice permessi raggruppata per area. Opzione "duplica da" per partire da un ruolo esistente
- **Modifica**: matrice permessi con checkbox "seleziona tutti" per area e stato indeterminate. Indicazione del numero di utenti impattati
- **Eliminazione**: bloccata per ruoli di sistema. Non possibile se ci sono utenti assegnati

### Protezioni

- I ruoli di sistema (SuperAdmin, Admin, User) non possono essere eliminati
- I permessi del SuperAdmin non possono essere modificati (mantiene sempre tutti)
- Alla modifica dei ruoli: invalidazione immediata dei token degli utenti impattati

---

## Audit log

Accessibile da `/admin/audit-log`. Richiede il permesso `AuditLog.Read`.

### API Endpoints

| Metodo | Endpoint | Permesso | Descrizione |
|--------|----------|----------|-------------|
| GET | `/api/v1/admin/audit-log` | `AuditLog.Read` | Lista paginata con filtri |
| GET | `/api/v1/admin/audit-log/{id}` | `AuditLog.Read` | Dettaglio singolo evento |
| GET | `/api/v1/admin/audit-log/export` | `AuditLog.Export` | Export CSV |

### Eventi registrati

Il sistema registra automaticamente 18 tipi di azione:

| Azione | Descrizione |
|--------|-------------|
| `UserCreated` | Creazione utente |
| `UserUpdated` | Modifica dati utente |
| `UserDeleted` | Eliminazione utente |
| `UserStatusChanged` | Attivazione/disattivazione |
| `UserRolesChanged` | Modifica ruoli utente |
| `RoleCreated` | Creazione ruolo |
| `RoleUpdated` | Modifica ruolo/permessi |
| `RoleDeleted` | Eliminazione ruolo |
| `LoginSuccess` | Login riuscito |
| `LoginFailed` | Tentativo login fallito |
| `Logout` | Logout |
| `PasswordChanged` | Cambio password |
| `PasswordReset` | Reset password |
| `PasswordResetRequested` | Richiesta reset password |
| `SettingsChanged` | Modifica impostazioni sistema |
| `SystemSeeding` | Seeding iniziale |
| `AccountDeleted` | Eliminazione account |
| `EmailConfirmed` | Conferma email |

Ogni evento include: timestamp, utente, tipo azione, entità coinvolta, dettaglio modifiche (prima/dopo in JSON), IP address, user agent.

### Funzionalità UI

- Tabella paginata con filtri: tipo azione (dropdown), intervallo date, ricerca testuale
- Riga espandibile con dettaglio completo (JSON formattato, IP, user agent)
- Export CSV con i filtri attualmente applicati (limite 10.000 righe, UTF-8 BOM)
- Il log è append-only e non eliminabile

---

## Impostazioni di sistema

Accessibile da `/admin/settings`. Richiede `Settings.Read` per visualizzare, `Settings.Manage` per modificare.

### API Endpoints

| Metodo | Endpoint | Permesso | Descrizione |
|--------|----------|----------|-------------|
| GET | `/api/v1/admin/settings` | `Settings.Read` | Tutte le impostazioni |
| PUT | `/api/v1/admin/settings` | `Settings.Manage` | Aggiornamento batch |

### Impostazioni disponibili

Le impostazioni sono raggruppate per categoria:

| Categoria | Chiave | Tipo | Default | Descrizione |
|-----------|--------|------|---------|-------------|
| Security | `Security.MaxLoginAttempts` | int | `5` | Tentativi login massimi |
| Security | `Security.LockoutDurationMinutes` | int | `15` | Durata blocco (minuti) |
| Security | `Security.MinPasswordLength` | int | `8` | Lunghezza minima password |
| Email | `Email.SendWelcomeEmail` | bool | `false` | Invio email di benvenuto |
| Email | `Email.RequireEmailConfirmation` | bool | `true` | Richiedi conferma email |
| AuditLog | `AuditLog.RetentionMonths` | int | `0` | Mesi di retention (0 = illimitato) |
| General | `General.AppName` | string | `Seed App` | Nome applicazione |
| General | `General.AllowPublicRegistration` | bool | `true` | Registrazione pubblica |

### Funzionalità

- Raggruppate per categoria in card separate
- Controllo appropriato per tipo: toggle (bool), campo numerico (int), campo testo (string)
- Info su chi ha modificato per ultimo e quando
- Dialog di conferma prima del salvataggio
- Cache in-memory con invalidazione automatica al salvataggio (TTL 5 minuti)
- Solo le impostazioni modificate vengono inviate al backend

> **Nota:** le impostazioni sono seminiate nel database al primo avvio. I valori di default sono definiti in `Seed.Domain/Authorization/SystemSettingsDefaults.cs`. Da quel momento vengono lette sempre e solo dal database.

---

## Dashboard di riepilogo

Accessibile da `/admin/dashboard`. Richiede il permesso `Dashboard.ViewStats`.

### API Endpoint

| Metodo | Endpoint | Permesso | Descrizione |
|--------|----------|----------|-------------|
| GET | `/api/v1/admin/dashboard` | `Dashboard.ViewStats` | Statistiche aggregate |

### Contenuto

- **Card conteggi**: utenti totali, attivi, disattivati
- **Card registrazioni**: ultimi 7 e 30 giorni
- **Grafico trend**: line chart SVG con registrazioni giornaliere degli ultimi 30 giorni (area fill con gradiente)
- **Grafico distribuzione**: donut chart SVG con distribuzione utenti per ruolo e legenda
- **Attività recenti**: widget con le ultime 5 attività dall'audit log e link alla sezione completa

I grafici sono componenti SVG custom Angular senza dipendenze esterne.

---

## Stato del sistema

Accessibile da `/admin/system-health`. Richiede il permesso `SystemHealth.Read`.

### API Endpoint

| Metodo | Endpoint | Permesso | Descrizione |
|--------|----------|----------|-------------|
| GET | `/api/v1/admin/system-health` | `SystemHealth.Read` | Stato completo |

### Componenti monitorati

| Componente | Indicatore | Valori |
|------------|-----------|--------|
| Database | Verde/Giallo/Rosso | Healthy / Degraded / Unhealthy |
| Servizio Email | Verde/Giallo | Configured / NotConfigured |

### Informazioni mostrate

- Versione applicazione (da assembly)
- Ambiente di esecuzione (Development/Staging/Production)
- Uptime del server (tempo dall'avvio del processo)
- Utilizzo memoria (Working Set e GC allocated)

Il pulsante "Ricontrolla" aggiorna i dati senza ricaricare la pagina.

---

## Navigazione e accesso condizionale

### Voce "Admin" nel menu

La voce "Admin" nel menu principale è visibile solo se l'utente ha almeno un permesso amministrativo. Un utente con ruolo "User" (senza permessi admin) non vede la voce.

### Sidebar admin

All'interno dell'area admin (`/admin`), la sidebar mostra solo le sezioni per cui l'utente ha i permessi:

| Voce sidebar | Permesso richiesto |
|-------------|-------------------|
| Dashboard | `Dashboard.ViewStats` |
| Utenti | `Users.Read` |
| Ruoli | `Roles.Read` |
| Audit Log | `AuditLog.Read` |
| Impostazioni | `Settings.Read` |
| Stato Sistema | `SystemHealth.Read` |

### Guard e protezioni

- `adminGuard`: verifica che l'utente abbia almeno un permesso admin, altrimenti redirige a `/`
- `permissionGuard(permission)`: verifica il permesso specifico per la rotta, altrimenti redirige a `/admin`
- Accesso via URL diretto a una sezione senza permesso: redirect (frontend) + 403 (API)
- `*hasPermission` directive: nasconde elementi UI in base ai permessi

---

## Architettura tecnica

### Backend

```
Seed.Domain/
├── Authorization/
│   ├── Permissions.cs           # 16 costanti permessi
│   ├── SystemRoles.cs           # 3 ruoli di sistema
│   ├── AuditActions.cs          # 18 tipi di azione audit
│   └── SystemSettingsDefaults.cs # 8 impostazioni default
├── Entities/
│   ├── ApplicationUser.cs       # Estende IdentityUser
│   ├── ApplicationRole.cs       # Estende IdentityRole
│   ├── Permission.cs
│   ├── RolePermission.cs
│   ├── AuditLogEntry.cs
│   └── SystemSetting.cs

Seed.Application/Admin/
├── Users/                       # 7 command + 2 query + DTOs
├── Roles/                       # 3 command + 3 query + DTOs
├── Dashboard/                   # 1 query + DTO
├── AuditLog/                    # 3 query + DTOs
├── Settings/                    # 1 command + 1 query + DTOs
└── SystemHealth/                # 1 query + DTOs

Seed.Infrastructure/
├── Persistence/Seeders/
│   ├── RolesAndPermissionsSeeder.cs
│   ├── SuperAdminSeeder.cs
│   └── SystemSettingsSeeder.cs
├── Services/
│   ├── PermissionService.cs     # Cache + query permessi
│   └── TokenBlacklistService.cs # Invalidazione token

Seed.Api/Controllers/
├── AdminUsersController.cs      # 9 endpoint
├── AdminRolesController.cs      # 6 endpoint
├── AdminDashboardController.cs  # 1 endpoint
├── AdminAuditLogController.cs   # 3 endpoint
├── AdminSettingsController.cs   # 2 endpoint
└── AdminSystemHealthController.cs # 1 endpoint
```

### Frontend

```
frontend/web/projects/
├── app/src/app/pages/admin/
│   ├── admin.routes.ts          # Rotte lazy-loaded
│   ├── layout/                  # Layout con sidebar
│   ├── dashboard/               # Dashboard + grafici SVG
│   ├── users/                   # Lista + dettaglio + dialog
│   ├── roles/                   # Lista + dettaglio + dialog
│   ├── audit-log/               # Lista con filtri + export
│   ├── settings/                # Impostazioni per categoria
│   └── system-health/           # Stato sistema
├── shared-auth/src/lib/
│   ├── models/permissions.ts    # Costanti permessi frontend
│   ├── services/permission.service.ts
│   └── directives/has-permission.directive.ts
```

### Database (tabelle aggiunte)

| Tabella | Descrizione |
|---------|-------------|
| `Permissions` | Definizioni permessi (name, category) |
| `RolePermissions` | Junction table ruoli-permessi |
| `AuditLogEntries` | Registro audit (indici su Timestamp, UserId, Action) |
| `SystemSettings` | Configurazioni chiave-valore (indice su Category) |

Le tabelle Identity standard (`AspNetUsers`, `AspNetRoles`) sono state estese con campi aggiuntivi (vedi sezione Architettura).

### Migration applicate

| Migration | Data | Descrizione |
|-----------|------|-------------|
| `AddPermissionSystem` | 2026-03-18 | Tabelle Permission, RolePermission, campo IsSystemRole |
| `AddAuditLog` | 2026-03-22 | Tabella AuditLogEntries con indici |
| `AddSoftDeleteToUsers` | 2026-03-22 | Campo IsDeleted su ApplicationUser |
| `AddSystemSettings` | 2026-03-23 | Tabella SystemSettings |
