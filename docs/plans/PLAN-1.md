# Implementation Plan: FEAT-1 — Admin Dashboard

**Requirements:** `docs/requirements/FEAT-1.md`
**Status:** Not Started
**Created:** 2026-03-18
**Last Updated:** 2026-03-18

---

## Story Coverage

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-001 | Seeding admin iniziale | T-01, T-02, T-03, T-04 | ⏳ Not Started |
| US-002 | Cambio password obbligatorio | T-05 | ⏳ Not Started |
| US-003 | Lista utenti | T-07, T-14, T-15 | ⏳ Not Started |
| US-004 | Promuovere un utente | T-07, T-15 | ⏳ Not Started |
| US-005 | Disattivare un utente | T-07, T-15 | ⏳ Not Started |
| US-006 | Creare un utente | T-07, T-15 | ⏳ Not Started |
| US-007 | Eliminare un utente | T-07, T-15 | ⏳ Not Started |
| US-008 | Modificare un utente | T-07, T-15 | ⏳ Not Started |
| US-009 | Creare un ruolo | T-08, T-16 | ⏳ Not Started |
| US-010 | Modificare permessi ruolo | T-08, T-16 | ⏳ Not Started |
| US-011 | Eliminare un ruolo | T-08, T-16 | ⏳ Not Started |
| US-012 | Consultare audit log | T-09, T-17 | ⏳ Not Started |
| US-013 | Esportare audit log CSV | T-09, T-17 | ⏳ Not Started |
| US-014 | Impostazioni a runtime | T-10, T-18 | ⏳ Not Started |
| US-015 | Dashboard di riepilogo | T-11, T-19 | ⏳ Not Started |
| US-016 | Stato del sistema | T-12, T-20 | ⏳ Not Started |
| US-017 | Accesso condizionale admin | T-05, T-13, T-14 | ⏳ Not Started |

---

## Fase A — Fondazione: Permessi, Ruoli e Seeding

### T-01: Sistema permessi — costanti e entità di dominio

**Stories:** US-001, US-017
**Size:** Small
**Status:** [ ] Not Started

**What to do:**
Creare la definizione centralizzata dei permessi come costanti nel Domain layer. Creare l'entità `Permission` e la tabella di giunzione `RolePermission` per associare permessi ai ruoli. Aggiungere il campo `MustChangePassword` a `ApplicationUser`. Aggiungere il flag `IsSystemRole` a `ApplicationRole`.

**Definition of Done:**
- [ ] Classe statica `Permissions` nel Domain con tutte le 16 costanti (es. `Users.Read`, `Roles.Create`, ecc.) e metodo per ottenere tutti i permessi
- [ ] Entità `Permission` con `Id`, `Name`, `Description`, `Category`
- [ ] Entità `RolePermission` (join table) con `RoleId`, `PermissionId`
- [ ] Campo `MustChangePassword` (bool, default false) su `ApplicationUser`
- [ ] Campo `IsSystemRole` (bool, default false) su `ApplicationRole`
- [ ] Configurazioni EF Core per le nuove entità
- [ ] Migration che crea le tabelle e aggiunge le nuove colonne
- [ ] Unit test per le costanti dei permessi (completezza, formato corretto)
- [ ] Build e migration applicata con successo

---

### T-02: Seeding ruoli e permessi

**Stories:** US-001
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-01

**What to do:**
Creare un seeder che popola il database con i 16 permessi e i 3 ruoli di sistema (SuperAdmin, Admin, User) con le relative associazioni. Il seeder è idempotente: se i dati esistono già, non li duplica.

**Definition of Done:**
- [ ] Seeder che crea i 16 permessi nel database
- [ ] Seeder che crea i 3 ruoli di sistema con `IsSystemRole = true`
- [ ] SuperAdmin ha tutti i 16 permessi assegnati
- [ ] Admin ha tutti i permessi tranne `Settings.Manage` e `Roles.Delete`
- [ ] User non ha permessi admin
- [ ] Il seeder è idempotente (esecuzioni multiple non creano duplicati)
- [ ] Il seeder viene eseguito all'avvio dell'applicazione, dopo le migration
- [ ] Integration test che verifica il seeding completo

---

### T-03: Infrastruttura di autorizzazione basata su permessi

**Stories:** US-001, US-017
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-02

**What to do:**
Implementare un sistema di autorizzazione che verifica i permessi dell'utente corrente. Creare un `IPermissionService` che carica i permessi dell'utente (con cache), un authorization handler ASP.NET che valida le policy basate su permessi, e un attributo/policy per proteggere gli endpoint. Il SuperAdmin bypassa tutti i controlli.

**Definition of Done:**
- [ ] `IPermissionService` con metodo `GetPermissionsAsync(userId)` che restituisce i permessi effettivi (unione dei ruoli)
- [ ] Cache server-side dei permessi per utente (con invalidazione)
- [ ] `PermissionAuthorizationHandler` che verifica se l'utente ha il permesso richiesto
- [ ] `HasPermissionAttribute` o policy factory per decorare gli endpoint (es. `[HasPermission(Permissions.Users.Read)]`)
- [ ] SuperAdmin bypassa tutti i controlli di permesso
- [ ] I permessi dell'utente vengono inclusi nella risposta di login (`LoginResponse` estesa con `permissions[]`)
- [ ] Unit test per `PermissionService` (utente con ruoli multipli, SuperAdmin bypass)
- [ ] Integration test per authorization handler (endpoint protetto, accesso con/senza permesso)

---

### T-04: Seeding SuperAdmin da variabili d'ambiente

**Stories:** US-001
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-02

**What to do:**
Creare un seeder che, al primo avvio, legge le variabili d'ambiente e crea l'utente SuperAdmin. Il seeder è idempotente. L'utente viene creato con `MustChangePassword = true` e il ruolo SuperAdmin assegnato.

**Definition of Done:**
- [ ] Il seeder legge `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`, `SEED_ADMIN_FIRSTNAME`, `SEED_ADMIN_LASTNAME`
- [ ] Se non esiste un utente con ruolo SuperAdmin, ne crea uno con queste credenziali
- [ ] Se un SuperAdmin esiste già, l'operazione viene saltata silenziosamente
- [ ] Il nuovo utente ha `MustChangePassword = true`
- [ ] Il nuovo utente ha il ruolo SuperAdmin assegnato
- [ ] Il seeder viene eseguito all'avvio, dopo il seeding dei ruoli (T-02)
- [ ] Se le variabili d'ambiente non sono configurate, il seeder logga un warning e non crea nulla
- [ ] Integration test che verifica: creazione, idempotenza, flag MustChangePassword

---

## Fase B — Flusso Cambio Password e Audit Log

### T-05: Cambio password obbligatorio (backend + frontend)

**Stories:** US-002, US-017
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03, T-04

**What to do:**
Implementare il flusso di cambio password obbligatorio. Backend: la risposta di login include il flag `mustChangePassword`. Un nuovo endpoint `POST /api/v1/auth/change-password` per il cambio password. Un middleware/filtro che blocca le richieste API (tranne cambio password e logout) se il flag è attivo. Frontend: guard che redirige alla pagina di cambio password, pagina dedicata.

**Definition of Done:**
- [ ] `LoginResponse` include `mustChangePassword: boolean`
- [ ] Endpoint `POST /api/v1/auth/change-password` che accetta `currentPassword` e `newPassword`
- [ ] Il cambio password rimuove il flag `MustChangePassword` dall'utente
- [ ] Middleware che restituisce 403 con codice specifico (es. `PASSWORD_CHANGE_REQUIRED`) per qualsiasi richiesta API se il flag è attivo, eccetto `/auth/change-password` e `/auth/logout`
- [ ] Frontend: guard `mustChangePasswordGuard` che redirige a `/change-password`
- [ ] Frontend: pagina `/change-password` con form (password attuale + nuova password + conferma)
- [ ] La nuova password rispetta le policy di sicurezza esistenti
- [ ] Unit test per il comando ChangePassword
- [ ] Integration test per il middleware di blocco
- [ ] Frontend test per il guard

---

### T-06: Infrastruttura Audit Log

**Stories:** US-012 (parziale — solo backend)
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-01

**What to do:**
Creare l'entità `AuditLogEntry`, il servizio `IAuditLogService`, e integrarlo nei flussi esistenti. Il servizio registra eventi con: timestamp, userId, azione, entità, dettaglio modifiche, IP, user-agent. Creare la migration per la tabella audit log con indici appropriati.

**Definition of Done:**
- [ ] Entità `AuditLogEntry` con: `Id`, `Timestamp`, `UserId` (nullable — per eventi di sistema), `Action` (enum o stringa), `EntityType`, `EntityId`, `Details` (JSON — valori prima/dopo), `IpAddress`, `UserAgent`
- [ ] `IAuditLogService` con metodo `LogAsync(...)` per registrare eventi
- [ ] Enum o costanti per i tipi di azione: `UserCreated`, `UserUpdated`, `UserDeleted`, `UserStatusChanged`, `UserRolesChanged`, `RoleCreated`, `RoleUpdated`, `RoleDeleted`, `LoginSuccess`, `LoginFailed`, `Logout`, `PasswordChanged`, `SettingsChanged`, `SystemSeeding`
- [ ] Migration con tabella `AuditLog` e indici su `Timestamp`, `UserId`, `Action`
- [ ] Integrazione nei flussi di auth esistenti (login ok/ko, logout, cambio password) — retroattiva
- [ ] Integrazione nel seeder dell'admin (T-04) per registrare l'evento di seeding
- [ ] Unit test per `AuditLogService`
- [ ] Integration test per la persistenza degli eventi

---

## Fase C — API Admin: Gestione Utenti e Ruoli

### T-07: API gestione utenti (AdminUsersController)

**Stories:** US-003, US-004, US-005, US-006, US-007, US-008
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-03, T-06

**What to do:**
Creare `AdminUsersController` con tutti gli endpoint di gestione utenti. Ogni endpoint è protetto dal relativo permesso. Tutte le operazioni vengono loggate nell'audit log.

Endpoint:
- `GET /api/v1/admin/users` — lista paginata con filtri (US-003)
- `GET /api/v1/admin/users/{id}` — dettaglio utente (US-003)
- `POST /api/v1/admin/users` — creazione utente (US-006)
- `PUT /api/v1/admin/users/{id}` — modifica dati utente (US-008)
- `DELETE /api/v1/admin/users/{id}` — eliminazione utente (US-007)
- `PUT /api/v1/admin/users/{id}/status` — toggle attivo/disattivo (US-005)
- `PUT /api/v1/admin/users/{id}/roles` — assegnazione ruoli (US-004)
- `POST /api/v1/admin/users/{id}/force-password-change` — forza cambio password (US-008)
- `POST /api/v1/admin/users/{id}/reset-password` — invia link reset password (US-008)

**Definition of Done:**
- [ ] Tutti gli endpoint implementati con MediatR commands/queries
- [ ] Paginazione lato server con `PagedResult<T>` (pageNumber, pageSize, totalCount, items)
- [ ] Ricerca per nome/email, filtri per ruolo/stato/periodo di registrazione
- [ ] Ordinamento per qualsiasi colonna esposta
- [ ] Ogni endpoint protetto dal permesso corretto
- [ ] Regole di protezione: no auto-eliminazione, no eliminazione/disattivazione SuperAdmin, no auto-modifica ruolo SuperAdmin
- [ ] Tutte le operazioni loggate nell'audit log con dettaglio prima/dopo
- [ ] FluentValidation per tutti i comandi
- [ ] Unit test per command handler e validatori
- [ ] Integration test per endpoint (permessi, paginazione, protezioni)

---

### T-08: API gestione ruoli (AdminRolesController)

**Stories:** US-009, US-010, US-011
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03, T-06

**What to do:**
Creare `AdminRolesController` con endpoint per gestione ruoli e permessi. Proteggere ogni endpoint con il permesso appropriato.

Endpoint:
- `GET /api/v1/admin/roles` — lista ruoli con conteggio utenti (US-009, US-010, US-011)
- `GET /api/v1/admin/roles/{id}` — dettaglio ruolo con permessi e utenti assegnati
- `POST /api/v1/admin/roles` — creazione ruolo (US-009)
- `PUT /api/v1/admin/roles/{id}` — modifica ruolo e permessi (US-010)
- `DELETE /api/v1/admin/roles/{id}` — eliminazione ruolo (US-011)
- `GET /api/v1/admin/permissions` — lista di tutti i permessi disponibili (per la matrice UI)

**Definition of Done:**
- [ ] Tutti gli endpoint implementati con MediatR commands/queries
- [ ] Conteggio utenti per ogni ruolo nella lista
- [ ] Creazione ruolo con nome, descrizione e lista permessi
- [ ] Duplicazione ruolo: il frontend invierà i permessi del ruolo da duplicare come dati iniziali
- [ ] Modifica: aggiornamento nome, descrizione e permessi in una singola operazione
- [ ] Eliminazione bloccata per ruoli di sistema (`IsSystemRole`)
- [ ] Modifica permessi SuperAdmin bloccata (mantiene sempre tutti)
- [ ] Invalidazione cache permessi alla modifica dei ruoli
- [ ] Tutte le operazioni loggate nell'audit log
- [ ] FluentValidation per tutti i comandi
- [ ] Unit test e integration test

---

### T-09: API Audit Log (AdminAuditLogController)

**Stories:** US-012, US-013
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-06

**What to do:**
Creare `AdminAuditLogController` con endpoint per consultazione ed esportazione del log.

Endpoint:
- `GET /api/v1/admin/audit-log` — lista paginata con filtri
- `GET /api/v1/admin/audit-log/{id}` — dettaglio singolo evento
- `GET /api/v1/admin/audit-log/export` — export CSV con filtri

**Definition of Done:**
- [ ] Lista paginata con filtri: tipo azione, userId, intervallo date, ricerca testuale (su Details)
- [ ] Ordinamento per timestamp (default: più recenti prima)
- [ ] Dettaglio singolo evento con campo `Details` deserializzato
- [ ] Export CSV che rispetta i filtri applicati, con tutte le colonne significative
- [ ] Endpoint protetti da `AuditLog.Read` (lista e dettaglio) e `AuditLog.Export` (export)
- [ ] Nessun endpoint di modifica o cancellazione (il log è append-only)
- [ ] Integration test per filtri, paginazione e CSV export

---

### T-10: API Impostazioni di Sistema (AdminSettingsController)

**Stories:** US-014
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03, T-06

⚠️ **DECISION REQUIRED: Strategia di storage per le impostazioni**

Le impostazioni di sistema devono essere persistite e sovrascrivere i valori di default. Opzioni:

**Option A — Tabella `SystemSettings` con righe chiave-valore**
- Ogni impostazione è una riga con `Key`, `Value` (stringa), `Type` (per serializzazione), `LastModifiedBy`, `LastModifiedAt`
- Pro: flessibile, aggiungere una nuova impostazione richiede solo una nuova costante
- Con: niente type-safety a livello DB, serializzazione manuale

**Option B — Tabella `SystemSettings` con colonne tipizzate**
- Una sola riga con una colonna per ogni impostazione
- Pro: type-safety a livello DB, query dirette
- Con: aggiungere un'impostazione richiede una migration

**Recommendation:** Option A (chiave-valore) per la flessibilità richiesta da RNF-05 ("estensione senza modifiche strutturali").

**Awaiting decision from:** user

**What to do:**
Creare l'infrastruttura per le impostazioni di sistema: entità, servizio con cache in-memory, seeding dei valori di default, controller con endpoint lettura/modifica.

Endpoint:
- `GET /api/v1/admin/settings` — tutte le impostazioni raggruppate per categoria
- `PUT /api/v1/admin/settings` — aggiornamento batch delle impostazioni modificate

**Definition of Done:**
- [ ] Entità e migration per la tabella settings
- [ ] `ISystemSettingsService` con metodi `GetAllAsync()`, `UpdateAsync(changes)`, `GetValueAsync<T>(key)`
- [ ] Cache in-memory con invalidazione al salvataggio
- [ ] Seeding dei valori di default per tutte le 9 impostazioni
- [ ] Ogni impostazione include: chiave, valore, tipo, categoria, chi/quando ultima modifica
- [ ] Endpoint protetti da `Settings.Read` (lettura) e `Settings.Manage` (modifica)
- [ ] Modifiche loggate nell'audit log con dettaglio prima/dopo
- [ ] Unit test per il servizio (cache, invalidazione)
- [ ] Integration test per gli endpoint

---

### T-11: API Dashboard (AdminDashboardController)

**Stories:** US-015
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-07, T-09

**What to do:**
Creare un endpoint che aggrega le statistiche per la dashboard.

Endpoint:
- `GET /api/v1/admin/dashboard` — statistiche aggregate

**Definition of Done:**
- [ ] Risposta include: totale utenti, utenti attivi, utenti disattivati
- [ ] Registrazioni ultimi 7 giorni e ultimi 30 giorni
- [ ] Dati per grafico trend registrazioni (ultimi 30 giorni, raggruppati per giorno)
- [ ] Distribuzione utenti per ruolo (nome ruolo + conteggio)
- [ ] Ultime 5 attività dal log di audit (compatte)
- [ ] Endpoint protetto da `Dashboard.ViewStats`
- [ ] Query ottimizzate (conteggi aggregati, non caricamento di tutti i record)
- [ ] Integration test

---

### T-12: API System Health (AdminSystemHealthController)

**Stories:** US-016
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-03

**What to do:**
Creare un endpoint che verifica lo stato dei componenti del sistema. Sfruttare gli health check ASP.NET già configurati e aggiungere informazioni extra.

Endpoint:
- `GET /api/v1/admin/system-health` — stato completo del sistema

**Definition of Done:**
- [ ] Stato connessione database (healthy/unhealthy con eventuale errore)
- [ ] Stato servizio email (configurato e raggiungibile / non configurato / errore)
- [ ] Versione applicazione (letta da assembly)
- [ ] Ambiente di esecuzione (Development/Staging/Production)
- [ ] Uptime del server (tempo dall'avvio del processo)
- [ ] Utilizzo memoria del processo
- [ ] Endpoint protetto da `SystemHealth.Read`
- [ ] Integration test

**Notes:**
- Spazio disco rimosso dallo scope: non è possibile leggerlo in modo cross-platform affidabile in tutti gli ambienti di deploy (container, ecc.). Può essere aggiunto in futuro se necessario.

---

## Fase D — Frontend Admin: Layout e Pagine

### T-13: Layout admin, routing e navigazione condizionale

**Stories:** US-017
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-03

**What to do:**
Creare il modulo admin nel frontend (lazy-loaded). Layout dedicato con sidebar. Guard basato sui permessi. Navigazione condizionale: la voce "Admin" appare nel menu principale solo se l'utente ha almeno un permesso admin. Le voci nella sidebar dipendono dai permessi dell'utente.

**Definition of Done:**
- [ ] Rotta `/admin` lazy-loaded con layout dedicato (sidebar + content area)
- [ ] Sidebar con voci: Dashboard, Utenti, Ruoli, Audit Log, Impostazioni, Stato Sistema
- [ ] Ogni voce visibile solo se l'utente ha il permesso di lettura corrispondente
- [ ] Guard `adminGuard` che verifica almeno un permesso admin
- [ ] Guard `permissionGuard` configurabile per singola rotta
- [ ] Voce "Admin" nel menu principale (header/navbar) visibile solo con almeno un permesso admin
- [ ] `PermissionService` nel frontend che espone i permessi come signals e metodo `hasPermission()`
- [ ] Direttiva `*hasPermission` (structural directive) per condizionare elementi UI
- [ ] Redirect a `/admin/dashboard` come rotta default dell'area admin
- [ ] Test per guards e PermissionService

---

### T-14: Frontend — Dashboard admin

**Stories:** US-015, US-017
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-11, T-13

**What to do:**
Implementare la pagina dashboard con card statistiche, grafici e widget ultime attività.

**Definition of Done:**
- [ ] Card con conteggi: utenti totali, attivi, disattivati
- [ ] Card con registrazioni: ultimi 7 e 30 giorni
- [ ] Grafico trend registrazioni (ultimi 30 giorni) — line chart con Angular Material o libreria leggera
- [ ] Grafico distribuzione utenti per ruolo — donut/pie chart
- [ ] Widget ultime 5 attività audit con link a sezione completa
- [ ] Skeleton loading durante il caricamento
- [ ] Responsive: card si riorganizzano su tablet

⚠️ **DECISION REQUIRED: Libreria grafici**

Per i grafici della dashboard (trend registrazioni, distribuzione ruoli):

**Option A — ngx-charts**
- Libreria Angular-native, basata su D3
- Pro: buona integrazione Angular, animazioni fluide
- Con: bundle size significativo (~150KB), manutenzione rallentata

**Option B — Chart.js + ng2-charts**
- Wrapper Angular per Chart.js
- Pro: leggero (~60KB), molto popolare, ben mantenuto, ampia documentazione
- Con: non Angular-native, richiede wrapper

**Option C — Grafici custom con SVG/Canvas**
- Implementazione manuale per i 2 grafici necessari
- Pro: zero dipendenze, dimensione minima
- Con: più tempo di sviluppo, meno funzionalità out-of-the-box

**Recommendation:** Option B (Chart.js + ng2-charts) — buon compromesso tra peso, funzionalità e manutenibilità per 2 semplici grafici.

**Awaiting decision from:** user

---

### T-15: Frontend — Gestione utenti

**Stories:** US-003, US-004, US-005, US-006, US-007, US-008
**Size:** Large
**Status:** [ ] Not Started
**Depends on:** T-07, T-13

**What to do:**
Implementare le pagine di gestione utenti: lista, dettaglio/modifica, creazione.

**Definition of Done:**
- [ ] **Lista utenti**: tabella Angular Material con paginazione server-side, ricerca, filtri (ruolo, stato, periodo), ordinamento
- [ ] Colonne: nome con iniziali/avatar, email, ruoli (chip), stato (badge), data registrazione, ultimo accesso
- [ ] Toggle attiva/disattiva inline (se ha permesso `Users.ToggleStatus`)
- [ ] Pulsante elimina con dialog di conferma (se ha permesso `Users.Delete`)
- [ ] **Pagina dettaglio/modifica**: form con nome, cognome, email; gestione ruoli (add/remove); info account (sola lettura); cronologia attività
- [ ] Pulsanti: salva, reset password forzato, forza cambio password — visibilità basata su permessi
- [ ] **Pagina creazione**: form con nome, cognome, email, password (con auto-generazione), selezione ruoli, checkbox email benvenuto
- [ ] Toast di successo/errore per ogni operazione
- [ ] Skeleton loading per lista e dettaglio
- [ ] Gestione stati vuoti (nessun utente trovato, nessun filtro corrispondente)

---

### T-16: Frontend — Gestione ruoli

**Stories:** US-009, US-010, US-011
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-08, T-13

**What to do:**
Implementare le pagine di gestione ruoli: lista, dettaglio/modifica, creazione.

**Definition of Done:**
- [ ] **Lista ruoli**: tabella con nome, descrizione, numero utenti, badge "Sistema" per ruoli non eliminabili
- [ ] Pulsante elimina disabilitato per ruoli di sistema, con dialog di conferma per gli altri
- [ ] **Pagina dettaglio/modifica**: form nome e descrizione; matrice permessi raggruppata per area
- [ ] Checkbox "seleziona tutti" per area nella matrice permessi
- [ ] Indicazione in tempo reale del numero di utenti impattati
- [ ] SuperAdmin: matrice permessi in sola lettura (sempre tutti attivi)
- [ ] **Pagina creazione**: come modifica; opzione "duplica da" per partire da un ruolo esistente
- [ ] Toast e skeleton loading

---

### T-17: Frontend — Audit Log

**Stories:** US-012, US-013
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-09, T-13

**What to do:**
Implementare la pagina audit log con tabella, filtri e export.

**Definition of Done:**
- [ ] Tabella paginata con colonne: timestamp, utente, azione, entità, riepilogo
- [ ] Filtri: tipo azione (dropdown), utente (autocomplete), intervallo date (date picker), ricerca testuale
- [ ] Riga espandibile con dettaglio modifiche prima/dopo (JSON formattato o tabella diff)
- [ ] Pulsante "Esporta CSV" che scarica i dati con i filtri applicati (se ha permesso `AuditLog.Export`)
- [ ] Skeleton loading e stato vuoto

---

### T-18: Frontend — Impostazioni di Sistema

**Stories:** US-014
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-10, T-13

**What to do:**
Implementare la pagina impostazioni con form raggruppati per categoria.

**Definition of Done:**
- [ ] Impostazioni raggruppate per categoria (card o section)
- [ ] Controllo appropriato per tipo: toggle (boolean), campo numerico (number), campo testo (string)
- [ ] Per ogni impostazione: label, valore corrente, chi l'ha modificata per ultimo e quando
- [ ] Dialog di conferma al salvataggio
- [ ] Con solo `Settings.Read`: tutti i controlli disabilitati
- [ ] Toast successo/errore
- [ ] Skeleton loading

---

### T-19: Frontend — Dashboard (grafici e statistiche)

**Stories:** US-015
**Size:** Medium
**Status:** [ ] Not Started
**Depends on:** T-14

**Notes:** Questo task è stato assorbito da T-14. Mantenuto per tracciabilità.
**Status:** [-] Skipped — incorporato in T-14

---

### T-20: Frontend — Stato del Sistema

**Stories:** US-016
**Size:** Small
**Status:** [ ] Not Started
**Depends on:** T-12, T-13

**What to do:**
Implementare la pagina stato del sistema con indicatori visuali.

**Definition of Done:**
- [ ] Card per ogni componente con indicatore visuale: verde (OK), giallo (warning), rosso (errore)
- [ ] Componenti mostrati: Database, Servizio Email
- [ ] Informazioni generali: versione app, ambiente, uptime, utilizzo memoria
- [ ] Pulsante "Ricontrolla" che richiama l'endpoint e aggiorna i dati
- [ ] Skeleton loading durante il caricamento iniziale e il refresh
