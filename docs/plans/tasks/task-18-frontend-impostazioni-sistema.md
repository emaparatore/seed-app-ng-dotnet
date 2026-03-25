# Task 18: Frontend — Impostazioni di Sistema

## Contesto

### Stato attuale del codice rilevante

**Backend (completato in T-10):**
- `GET /api/v1.0/admin/settings` → `IReadOnlyList<SystemSettingDto>` (richiede `Settings.Read`)
- `PUT /api/v1.0/admin/settings` → `204 NoContent` (richiede `Settings.Manage`)
- `SystemSettingDto`: `{ key, value, type, category, description, modifiedBy, modifiedAt }`
- `UpdateSettingItem`: `{ key, value }` — il PUT accetta `{ items: UpdateSettingItem[] }`
- 8 impostazioni predefinite in 4 categorie: Security (3), Email (2), AuditLog (1), General (2)
- Tipi supportati: `bool`, `int`, `string`
- Validazione lato server: tipo, chiavi esistenti, all-or-nothing semantics

**Frontend admin (completato in T-13/T-14):**
- Route `/admin/settings` già registrata in `admin.routes.ts` con `Settings.Read` permission e placeholder component
- Sidebar con link "Impostazioni" (icona `settings`) già visibile con permesso `Settings.Read`
- Pattern consolidati: signals per stato, `MatSnackBar` per toast, `MatDialog` per conferme, skeleton loading con animazione pulse

### Dipendenze e vincoli
- **T-10:** Backend settings API (completato)
- **T-13:** Admin layout e routing (completato) — route e sidebar già configurati
- Il form deve essere read-only se l'utente ha solo `Settings.Read` ma non `Settings.Manage`
- I valori sono tutti stringhe nel DTO; il frontend deve renderizzare il controllo corretto in base a `type`

## Piano di esecuzione

### File da creare

1. **`frontend/web/projects/app/src/app/pages/admin/settings/settings.models.ts`**
   - Interface `SystemSetting` (mappata da `SystemSettingDto`)
   - Interface `UpdateSettingItem`
   - Interface `SettingsGroup` per raggruppamento per categoria: `{ category: string, settings: SystemSetting[] }`

2. **`frontend/web/projects/app/src/app/pages/admin/settings/settings.service.ts`**
   - `getSettings(): Observable<SystemSetting[]>` → GET `/admin/settings`
   - `updateSettings(items: UpdateSettingItem[]): Observable<void>` → PUT `/admin/settings`
   - Pattern: `inject(HttpClient)`, `inject(AUTH_CONFIG)` come altri servizi admin

3. **`frontend/web/projects/app/src/app/pages/admin/settings/settings.ts`** (component standalone)
   - Template inline + styles inline (come altri componenti admin)
   - Signals: `loading`, `saving`, `settings`, `error`
   - `computed` signal `settingsGroups` che raggruppa per categoria e ordina
   - `computed` signal `canManage` basato su permesso `Settings.Manage`
   - Metodo `save()` che raccoglie i valori modificati e chiama il servizio
   - Dialog di conferma pre-salvataggio tramite `ConfirmDialog` esistente
   - Toast successo/errore con `MatSnackBar`

### File da modificare

4. **`frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`**
   - Cambiare il `loadComponent` della route `settings` dal placeholder al nuovo componente `SettingsComponent`

### Approccio tecnico step-by-step

1. **Creare models** (`settings.models.ts`):
   - `SystemSetting`: key, value, type, category, description, modifiedBy, modifiedAt
   - `UpdateSettingItem`: key, value
   - `SettingsGroup`: category, settings[]

2. **Creare service** (`settings.service.ts`):
   - Seguire pattern identico a `audit-log.service.ts` / `users.service.ts`
   - Base URL: `${apiUrl}/admin/settings`
   - GET e PUT

3. **Creare component** (`settings.ts`):
   - **Layout:** Una `mat-card` per ogni categoria (come le permission groups in role-detail)
   - **Controlli per tipo:**
     - `bool` → `mat-slide-toggle`
     - `int` → `input[type=number]` dentro `mat-form-field`
     - `string` → `input[type=text]` dentro `mat-form-field`
   - **Metadati:** Sotto ogni controllo, mostrare description e "Ultima modifica: {data} da {utente}" se `modifiedAt` presente
   - **Read-only:** Se non ha `Settings.Manage`, tutti i controlli `[disabled]="true"`
   - **Pulsante Salva:** In fondo alla pagina o per ogni card, abilitato solo se ci sono modifiche
   - **Skeleton loading:** Una card con righe skeleton animate (come gli altri componenti)
   - **Dialog conferma:** Prima del salvataggio, "Sei sicuro di voler salvare le modifiche?" via `ConfirmDialog`
   - **Tracking modifiche:** Mantenere mappa `originalValues` per confrontare e inviare solo i cambiati
   - **Material imports:** MatCard, MatSlideToggle, MatFormField, MatInput, MatButton, MatIcon, MatProgressBar, MatSnackBar, MatDialog

4. **Aggiornare route** in `admin.routes.ts`:
   - `loadComponent: () => import('./settings/settings').then(m => m.SettingsComponent)`

### Test da scrivere/verificare

- **Non servono unit test dedicati** per questo task: il componente è prevalentemente UI/form con poca logica. I pattern sono identici agli altri componenti admin già testati.
- **Verifica manuale:** `ng build` deve compilare senza errori.
- **Verifica funzionale:** La pagina deve caricarsi su `/admin/settings`, mostrare le 4 categorie con i controlli corretti, e il salvataggio deve funzionare con dialog di conferma.

## Criteri di completamento

- [x] Route `/admin/settings` carica il nuovo componente (non più il placeholder)
- [x] Impostazioni raggruppate per categoria in card separate
- [x] Controllo appropriato per tipo: `mat-slide-toggle` (bool), campo numerico (int), campo testo (string)
- [x] Per ogni impostazione: label (description), valore corrente, info ultima modifica (chi e quando)
- [x] Dialog di conferma al salvataggio via `ConfirmDialog`
- [x] Con solo `Settings.Read` (senza `Settings.Manage`): tutti i controlli disabilitati, pulsante salva nascosto
- [x] Toast successo/errore con `MatSnackBar`
- [x] Skeleton loading durante il caricamento iniziale
- [x] `ng build` compila senza errori

## Risultato

### File creati
- `frontend/web/projects/app/src/app/pages/admin/settings/settings.models.ts` — Interfacce `SystemSetting`, `UpdateSettingItem`, `SettingsGroup`
- `frontend/web/projects/app/src/app/pages/admin/settings/settings.service.ts` — Service con GET e PUT verso `/admin/settings`
- `frontend/web/projects/app/src/app/pages/admin/settings/settings.ts` — Componente standalone con template e styles inline

### File modificati
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Route `settings` aggiornata dal placeholder al nuovo `SettingsComponent`

### Scelte implementative e motivazioni
- **Template e styles inline** nel componente, coerente con il pattern degli altri componenti admin (confirm-dialog, ecc.)
- **Signals per stato reattivo** (`loading`, `saving`, `settings`, `error`, `currentValues`) + `computed` per `settingsGroups`, `canManage`, `hasChanges` — pattern consolidato nel progetto
- **`currentValues` come `Map<string, string>` in un signal** per tracciare le modifiche rispetto a `originalValues` e calcolare `hasChanges` in modo reattivo
- **`@switch` su `setting.type`** per renderizzare il controllo appropriato: `mat-slide-toggle` per bool, `input[type=number]` per int, `input[type=text]` per string
- **Riuso di `ConfirmDialog`** esistente in `users/confirm-dialog/` — nessun nuovo dialog creato
- **Pulsante salva visibile solo con `Settings.Manage` E modifiche pendenti** — con solo `Settings.Read` tutti i controlli sono disabilitati e il pulsante è nascosto
- **Solo le impostazioni modificate vengono inviate** nel PUT, confrontando `currentValues` con `originalValues`

### Deviazioni dal piano
- Nessuna deviazione significativa dal piano
