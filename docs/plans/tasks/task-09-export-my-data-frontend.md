# Task 09: Export dati personali — Frontend

## Contesto

- **Backend già implementato:** endpoint `GET /api/v1/auth/export-my-data` protetto da `[Authorize]`, restituisce JSON con profilo, consensi, ruoli, audit log (`UserDataExportDto`)
- **Pagina profilo esistente:** `projects/app/src/app/pages/profile/profile.ts` — mostra dati utente, ha già pulsante Delete Account con loading state pattern
- **AuthService:** `projects/shared-auth/src/lib/services/auth.service.ts` — non ha ancora metodo `exportMyData()`
- **API URL pattern:** `${this.apiUrl}/auth/export-my-data` (basato su `AUTH_CONFIG.apiUrl`)
- **Pattern di download file:** non esiste ancora nel progetto; serve `Blob` + `URL.createObjectURL` + anchor click

### Dipendenze
- T-08 (backend export) — completato
- Angular Material già importato nel profilo (`MatButtonModule`, `MatCardModule`)

## Piano di esecuzione

### Step 1: Aggiungere metodo `exportMyData()` in AuthService

**File:** `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts`

- Aggiungere metodo:
  ```typescript
  exportMyData(): Observable<object> {
    return this.http.get<object>(`${this.apiUrl}/export-my-data`);
  }
  ```
- Restituisce `Observable<object>` perché il JSON viene poi convertito in Blob per il download

### Step 2: Aggiungere pulsante e logica in Profile component

**File:** `frontend/web/projects/app/src/app/pages/profile/profile.ts`

- Aggiungere signal `exporting = signal(false)` per loading state
- Aggiungere metodo `exportMyData()`:
  1. Set `exporting(true)`
  2. Chiama `authService.exportMyData()`
  3. Converte risposta JSON in `Blob` con `type: 'application/json'`
  4. Crea URL temporaneo con `URL.createObjectURL(blob)`
  5. Crea anchor element, imposta `href` e `download` filename (`my-data-export.json`), click programmatico
  6. Revoca URL con `URL.revokeObjectURL`
  7. Set `exporting(false)` in `finalize()`
  8. Gestisci errore con `error.set(...)`

**File:** `frontend/web/projects/app/src/app/pages/profile/profile.html`

- Aggiungere pulsante "Export My Data" nell'area `mat-card-actions`, prima del pulsante Delete:
  ```html
  <button mat-button (click)="exportMyData()" [disabled]="exporting()">
    {{ exporting() ? 'Exporting...' : 'Export My Data' }}
  </button>
  ```

### Step 3: Aggiungere test unitari

**File:** `frontend/web/projects/app/src/app/pages/profile/profile.spec.ts`

- Aggiungere mock per `exportMyData` nel setup di `authService`
- Test: "should call exportMyData on export button click" — verifica che `authService.exportMyData()` venga chiamato
- Test: "should set error when exportMyData fails" — verifica gestione errore

**File:** `frontend/web/projects/shared-auth/src/lib/services/auth.service.spec.ts`

- Verificare se esiste già un test file e aggiungere test per il nuovo metodo `exportMyData()` (HTTP GET call)

### Step 4: Verificare build e test

```bash
cd frontend/web && ng test app && ng build
```

## Criteri di completamento

- [x] Metodo `exportMyData()` in `AuthService` chiama `GET /auth/export-my-data`
- [x] Pulsante "Export My Data" visibile in pagina profilo
- [x] Click sul pulsante scarica file JSON con dati utente
- [x] Loading state visivo durante il download (`exporting` signal)
- [x] Gestione errore con messaggio visibile
- [x] Test unitario: verifica chiamata al click del pulsante
- [x] `ng test app` e `ng build` passano senza errori

## Risultato

- File modificati/creati:
  - `frontend/web/projects/shared-auth/src/lib/services/auth.service.ts` — aggiunto metodo `exportMyData()` che chiama `GET /auth/export-my-data`
  - `frontend/web/projects/shared-auth/src/lib/services/auth.service.spec.ts` — aggiunto test per `exportMyData()` (HTTP GET), fix payload register test (campi consenso mancanti)
  - `frontend/web/projects/app/src/app/pages/profile/profile.ts` — aggiunto signal `exporting`, metodo `exportMyData()` con Blob download e gestione errore
  - `frontend/web/projects/app/src/app/pages/profile/profile.html` — aggiunto pulsante "Export My Data" prima del pulsante Delete Account
  - `frontend/web/projects/app/src/app/pages/profile/profile.spec.ts` — aggiunti 2 test: click export chiama servizio, gestione errore export
  - `frontend/web/tsconfig.json` — aggiunto source path fallback per `shared-auth` (`./projects/shared-auth/src/public-api.ts`) perché `dist/shared-auth` era read-only e non ricostruibile

- Scelte implementative e motivazioni:
  - Download via `Blob` + `URL.createObjectURL` + anchor click programmatico: pattern standard per download file da JSON response, senza dipendenze esterne
  - JSON formattato con `JSON.stringify(data, null, 2)` per leggibilità del file esportato
  - Filename fisso `my-data-export.json` come da piano
  - Gestione errore sincrona con `error.set()` coerente con il pattern esistente di `deleteAccount()`

- Deviazioni dal piano:
  - Aggiunto source path fallback in `tsconfig.json` per `shared-auth`: necessario perché la directory `dist/shared-auth` era di proprietà root e non riscrivibile, impedendo la ricostruzione della libreria. Il fallback al sorgente è un pattern standard Angular per lo sviluppo locale
  - Fix pre-esistente nel test `auth.service.spec.ts`: il test `register` mancava dei campi `acceptPrivacyPolicy`/`acceptTermsOfService` introdotti in T-05, causando errore di compilazione TypeScript
