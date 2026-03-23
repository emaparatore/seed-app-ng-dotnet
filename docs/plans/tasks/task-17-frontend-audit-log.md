# Task 17: Frontend — Audit Log

## Contesto

- **Stato attuale:** La rotta `/admin/audit-log` esiste in `admin.routes.ts` (riga 44-49) ma punta al componente `AdminPlaceholder`. Il backend è completo con 3 endpoint: lista paginata, dettaglio singolo, export CSV.
- **Backend API disponibili:**
  - `GET /api/v1.0/admin/audit-log` — lista paginata (pageNumber, pageSize, actionFilter, userId, dateFrom, dateTo, searchTerm, sortDescending). Permesso: `AuditLog.Read`. Ritorna `PagedResult<AuditLogEntryDto>`.
  - `GET /api/v1.0/admin/audit-log/{id}` — dettaglio entry. Permesso: `AuditLog.Read`. Ritorna `AuditLogEntryDto`.
  - `GET /api/v1.0/admin/audit-log/export` — CSV export con filtri (no paginazione, max 10.000 righe, UTF-8 BOM). Permesso: `AuditLog.Export`.
- **DTO backend (`AuditLogEntryDto`):** Id (Guid), Timestamp (DateTime), UserId (Guid?), Action (string), EntityType (string), EntityId (string?), Details (string? — JSON), IpAddress (string?), UserAgent (string?).
- **Azioni audit definite (18):** UserCreated, UserUpdated, UserDeleted, UserStatusChanged, UserRolesChanged, RoleCreated, RoleUpdated, RoleDeleted, LoginSuccess, LoginFailed, Logout, PasswordChanged, PasswordReset, PasswordResetRequested, SettingsChanged, SystemSeeding, AccountDeleted, EmailConfirmed.
- **Pattern esistenti:** Il componente `UserList` (`pages/admin/users/user-list/`) è il riferimento principale per tabella paginata + filtri + skeleton loading. I service admin (`users.service.ts`, `dashboard.service.ts`) usano `HttpClient` + `AUTH_CONFIG` injection.
- **Dipendenze:** T-09 (backend API ✅) e T-13 (layout/routing ✅) completati.

## Piano di esecuzione

### File da creare

1. **`frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log.models.ts`** — Interfacce TypeScript
   - `AuditLogEntry`: id, timestamp, userId, action, entityType, entityId, details, ipAddress, userAgent
   - `GetAuditLogParams`: pageNumber, pageSize, actionFilter, userId, dateFrom, dateTo, searchTerm, sortDescending (tutti opzionali)
   - `PagedResult<T>` riutilizzare da models esistenti o ridefinire (verificare se esiste già globalmente)

2. **`frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log.service.ts`** — Service per API
   - `getEntries(params: GetAuditLogParams): Observable<PagedResult<AuditLogEntry>>`
   - `getEntryById(id: string): Observable<AuditLogEntry>`
   - `exportCsv(params): Observable<Blob>` — con `responseType: 'blob'` per download file
   - Pattern: `inject(HttpClient)`, `inject(AUTH_CONFIG)`, `@Injectable({ providedIn: 'root' })`

3. **`frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.ts`** — Componente lista (standalone)
   - Signal state: `loading`, `entries`, `error`, `totalCount`, `expandedEntryId`
   - FormControl per filtri: `searchControl` (debounce 300ms), `actionFilterControl` (dropdown con le 18 azioni), `dateFromControl`, `dateToControl`
   - Paginazione server-side: `pageIndex`, `pageSize`
   - Ordinamento: sortDescending (default true, più recenti prima)
   - Riga espandibile: click sulla riga espande dettaglio con `Details` JSON formattato (usa `JSON.parse` + `JSON.stringify(obj, null, 2)`)
   - Export CSV: bottone visibile solo se `hasPermission('AuditLog.Export')`, chiama service e triggera download con `URL.createObjectURL`
   - Inject: `AuditLogService`, `PermissionService`, `DestroyRef`
   - Material imports: MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, MatIconModule, MatDatepickerModule, MatNativeDateModule, ReactiveFormsModule, DatePipe

4. **`frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.html`** — Template
   - Header con titolo "Audit Log" e bottone "Esporta CSV" (condizionale su permesso)
   - Barra filtri: ricerca testuale, dropdown azione, date picker range, bottone "Pulisci filtri"
   - Tabella Material con colonne: timestamp, action, entityType, entityId, userId, expand-toggle
   - Riga espandibile sotto ogni riga: mostra Details (JSON prettified), IpAddress, UserAgent
   - Paginatore con `[length]="totalCount()"` e pageSizeOptions `[10, 25, 50]`
   - Skeleton loading (5 righe), empty state (icona `receipt_long`), error state con retry

5. **`frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.scss`** — Stili
   - Copiare pattern skeleton/error/empty da user-list.scss
   - Stili per riga espandibile (transizione, sfondo leggermente diverso)
   - Stili per blocco JSON pre-formattato (`<pre>` con overflow-x auto)
   - Colori e variabili Material coerenti con il resto dell'admin

6. **`frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.spec.ts`** — Test
   - Test creazione componente
   - Test rendering tabella con dati mock
   - Test skeleton loading visibile quando `loading()` è true
   - Test empty state quando entries vuoto
   - Test error state con bottone retry
   - Test export CSV visibile solo con permesso `AuditLog.Export`
   - Test espansione riga mostra dettagli
   - Test filtri resettano pageIndex
   - Test paginazione chiama loadEntries

### File da modificare

7. **`frontend/web/projects/app/src/app/pages/admin/admin.routes.ts`** — Aggiornare rotta audit-log
   - Cambiare `loadComponent` da placeholder a `audit-log-list/audit-log-list` → `AuditLogList`

### Approccio tecnico step-by-step

1. Creare `audit-log.models.ts` con le interfacce (verificare prima se `PagedResult` esiste già altrove)
2. Creare `audit-log.service.ts` seguendo pattern di `users.service.ts`
3. Creare il componente `audit-log-list` (ts + html + scss) seguendo il pattern di `user-list`
4. Implementare la riga espandibile per il dettaglio JSON (evita necessità di pagina dettaglio separata)
5. Implementare export CSV con download blob
6. Aggiornare `admin.routes.ts` per puntare al nuovo componente
7. Scrivere test (`audit-log-list.spec.ts`)
8. Verificare: `ng test app` e `ng build app`

### Test da scrivere/verificare

- **Unit test** (audit-log-list.spec.ts): ~9 test cases
  - Componente si crea correttamente
  - Tabella renderizza i dati mock
  - Skeleton loading durante caricamento
  - Empty state senza dati
  - Error state con retry
  - Export CSV visibile/nascosto per permesso
  - Espansione riga mostra dettagli
  - Filtri resettano pageIndex a 0
  - Paginazione richiama loadEntries
- **Eseguire** `ng test app` per verificare tutti i test passano
- **Eseguire** `ng build app` per verificare compilazione

## Criteri di completamento

- La rotta `/admin/audit-log` carica il componente `AuditLogList` (non più il placeholder)
- Tabella paginata server-side con colonne: timestamp, azione, tipo entità, ID entità, utente
- Filtri funzionanti: ricerca testuale (debounce), dropdown azione, date range
- Riga espandibile mostra dettaglio con Details JSON formattato, IP e User Agent
- Bottone "Esporta CSV" visibile solo con permesso `AuditLog.Export`, scarica file CSV
- Skeleton loading, empty state e error state implementati
- Almeno 9 test passano in `audit-log-list.spec.ts`
- `ng test app` passa senza errori
- `ng build app` compila senza errori

## Risultato

- File creati:
  - `frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log.models.ts` — Interfacce `AuditLogEntry`, `GetAuditLogParams`, costante `AUDIT_ACTIONS` con le 18 azioni
  - `frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log.service.ts` — Service con `getEntries()`, `getEntryById()`, `exportCsv()` (blob download)
  - `frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.ts` — Componente standalone con signal state, filtri, paginazione, riga espandibile animata, export CSV
  - `frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.html` — Template con filtri (search debounce, dropdown azioni, date range), tabella Material con `multiTemplateDataRows` per righe espandibili, skeleton/empty/error states
  - `frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.scss` — Stili coerenti con user-list (skeleton, error, empty), più stili per riga espandibile, JSON block, action badge
  - `frontend/web/projects/app/src/app/pages/admin/audit-log/audit-log-list/audit-log-list.spec.ts` — 14 test cases (supera i 9 richiesti)
- File modificati:
  - `frontend/web/projects/app/src/app/pages/admin/admin.routes.ts` — Rotta `audit-log` ora punta a `AuditLogList` invece di `AdminPlaceholder`
- Scelte implementative:
  - Riutilizzata `PagedResult<T>` da `users/models/user.models.ts` invece di ridefinirla (esiste già ed è identica al DTO backend)
  - Usata Angular animation `detailExpand` con `multiTemplateDataRows` per la riga espandibile (pattern Material ufficiale)
  - `loadEntries()` reso `public` (non `private`) per facilitare il testing diretto
  - Export CSV usa `URL.createObjectURL` + elemento `<a>` temporaneo per triggera il download
  - 14 test invece dei 9 richiesti: aggiunti test per `formatDetails` (JSON valido e invalido) e test per retry dopo errore
- Deviazioni dal piano: nessuna deviazione significativa
