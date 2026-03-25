# Task 20: Frontend — Stato del Sistema

## Contesto

- **Backend completo (T-12):** endpoint `GET /api/v1/admin/system-health` protetto da `SystemHealth.Read`, restituisce `SystemHealthDto` con: Database (status/description), Email (status/description), Version, Environment, Uptime (totalSeconds/formatted), Memory (workingSetMegabytes/gcAllocatedMegabytes).
- **Route già configurata** in `admin.routes.ts` (riga 57-61): path `system-health`, guard `permissionGuard('SystemHealth.Read')`, attualmente carica `AdminPlaceholder`.
- **Sidebar già configurata** in `admin-layout.html`: voce "Stato Sistema" con icona `monitor_heart`.
- **Permesso frontend già definito** in `shared-auth/models/permissions.ts`: `SystemHealth.Read`.
- **Pattern di riferimento:** `settings.ts` (single-component con signals, loading/error/data, skeleton, retry), `settings.service.ts` (service con `AUTH_CONFIG`).

## Piano di esecuzione

### File da creare

1. **`frontend/web/projects/app/src/app/pages/admin/system-health/system-health.models.ts`**
   - Interfacce TypeScript che mappano `SystemHealthDto`: `SystemHealth`, `ComponentStatus`, `Uptime`, `Memory`

2. **`frontend/web/projects/app/src/app/pages/admin/system-health/system-health.service.ts`**
   - Injectable service con `getSystemHealth(): Observable<SystemHealth>`
   - Usa `HttpClient` + `AUTH_CONFIG.apiUrl` + `/admin/system-health`
   - Pattern identico a `SettingsService`

3. **`frontend/web/projects/app/src/app/pages/admin/system-health/system-health.ts`**
   - Componente standalone con inline template e styles (pattern settings.ts)
   - Signals: `loading`, `error`, `data` (SystemHealth | null), `refreshing`
   - OnInit chiama `loadHealth()`
   - Template:
     - Skeleton loading (4 card skeleton) durante caricamento iniziale
     - Error card con pulsante "Riprova" in caso di errore
     - Card per Database: indicatore visuale verde/giallo/rosso basato su status (Healthy/Degraded/Unhealthy), mostra description
     - Card per Email: indicatore verde (Configured) / giallo (NotConfigured), mostra description
     - Card "Informazioni Generali": versione app, ambiente, uptime formattato, memoria (working set + GC)
     - Pulsante "Ricontrolla" nell'header che richiama endpoint e aggiorna dati (mostra spinner durante refresh)
   - Styles: coerenti con settings.ts (skeleton animation, error card, card spacing)

### File da modificare

4. **`frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`** (riga 58)
   - Cambiare `import('./placeholder')` → `import('./system-health/system-health')` con export `SystemHealthComponent`

### Approccio tecnico step-by-step

1. Creare `system-health.models.ts` con le interfacce
2. Creare `system-health.service.ts` seguendo pattern SettingsService
3. Creare `system-health.ts` con componente completo (template + styles inline)
   - Usare `MatCardModule`, `MatButtonModule`, `MatIconModule`, `MatProgressBarModule`
   - Indicatori visuali: pallino colorato (div con border-radius 50% + background color) o `mat-icon` con colore condizionale
   - Mappatura status → colore: `Healthy`/`Configured` → verde (#4caf50), `Degraded` → giallo (#ff9800), `Unhealthy`/`NotConfigured` → rosso (#f44336) per Unhealthy, giallo per NotConfigured
4. Aggiornare `admin.routes.ts` per caricare il nuovo componente
5. Verificare build con `ng build app`

### Test da scrivere/verificare

- Nessun test unitario richiesto per T-20 (il componente è puramente presentazionale con pattern identico alle altre pagine admin già validate). La build verification è sufficiente.
- Verificare che `ng build app` compili senza errori.

## Criteri di completamento

- [x] Card per Database con indicatore visuale verde (Healthy), giallo (Degraded), rosso (Unhealthy)
- [x] Card per Email con indicatore visuale verde (Configured), giallo (NotConfigured)
- [x] Card "Informazioni Generali" con versione, ambiente, uptime, memoria
- [x] Pulsante "Ricontrolla" che richiama l'endpoint e aggiorna i dati
- [x] Skeleton loading durante caricamento iniziale e refresh
- [x] Error card con retry in caso di errore API
- [x] Route aggiornata per caricare il componente reale (non più placeholder)
- [x] `ng build app` compila senza errori

## Risultato

### File creati
- `frontend/web/projects/app/src/app/pages/admin/system-health/system-health.models.ts` — Interfacce TypeScript (`SystemHealth`, `ComponentStatus`, `Uptime`, `Memory`) che mappano esattamente `SystemHealthDto` del backend
- `frontend/web/projects/app/src/app/pages/admin/system-health/system-health.service.ts` — Service injectable con `getSystemHealth()` che chiama `GET /admin/system-health`, pattern identico a `SettingsService`
- `frontend/web/projects/app/src/app/pages/admin/system-health/system-health.ts` — Componente standalone con template e styles inline, signals per loading/error/data/refreshing

### File modificati
- `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Route `system-health` aggiornata da `AdminPlaceholder` a `SystemHealthComponent`

### Scelte implementative e motivazioni
- **Pattern settings.ts:** Seguito fedelmente il pattern con signals (`loading`, `error`, `data`), skeleton loading, error card con retry, stili coerenti (stesse variabili CSS Material, stessa animazione pulse per skeleton)
- **Indicatori visuali:** Usato `span` con `border-radius: 50%` e colori condizionali (verde `#4caf50` per Healthy/Configured, giallo `#ff9800` per Degraded/NotConfigured, rosso `#f44336` per Unhealthy) sia come pallino nel titolo card che come colore testo nello status
- **Refresh separato da loading iniziale:** Il pulsante "Ricontrolla" usa un signal `refreshing` separato da `loading`, mostrando una progress bar indeterminate e un'icona rotante senza nascondere i dati esistenti
- **DecimalPipe:** Usato per formattare i valori di memoria con una cifra decimale (es. "123.4 MB")

### Deviazioni dal piano
- Nessuna deviazione significativa. Il piano è stato seguito esattamente come descritto.
