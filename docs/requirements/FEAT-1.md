# FEAT-1: Admin Dashboard

## Overview

L'applicazione necessita di un'area amministrativa riservata che consenta al proprietario e ai suoi delegati di gestire utenti, ruoli, configurazioni e monitorare lo stato del sistema. L'accesso è regolato da un sistema di permessi granulari basato su ruoli (RBAC). Al primo avvio viene creato automaticamente un SuperAdmin, le cui credenziali sono lette da variabili d'ambiente.

---

## Modello dei Permessi

Il sistema di autorizzazione si basa su tre concetti:

- **Permesso**: azione atomica nel formato `Risorsa.Azione` (es. `Users.Read`). Definiti dal sistema, non modificabili dall'utente.
- **Ruolo**: insieme nominato di permessi. Può essere di sistema (non eliminabile) o personalizzato.
- **Utente**: può avere zero o più ruoli. I permessi effettivi sono l'unione dei permessi di tutti i ruoli assegnati.

### Catalogo Permessi

| Area | Permesso | Descrizione |
|------|----------|-------------|
| Utenti | `Users.Read` | Visualizzare lista e dettaglio utenti |
| Utenti | `Users.Create` | Creare un utente manualmente |
| Utenti | `Users.Update` | Modificare i dati di un utente |
| Utenti | `Users.Delete` | Eliminare un utente |
| Utenti | `Users.ManageRoles` | Assegnare/rimuovere ruoli a un utente |
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

### Ruoli di Sistema

| Ruolo | Descrizione | Permessi |
|-------|-------------|----------|
| **SuperAdmin** | Proprietario dell'app | Tutti. Bypassa qualsiasi controllo. |
| **Admin** | Amministratore delegato | Tutti tranne `Settings.Manage` e `Roles.Delete` |
| **User** | Utente base | Nessun permesso admin. Solo parte pubblica. |

---

## Requisiti Funzionali

### RF-01: Seeding dell'Admin iniziale

Il sistema crea automaticamente un utente SuperAdmin al primo avvio, se non ne esiste già uno. Le credenziali vengono lette da variabili d'ambiente (`SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`, `SEED_ADMIN_FIRSTNAME`, `SEED_ADMIN_LASTNAME`). L'operazione è idempotente e avviene dopo le migration. Il nuovo utente ha il flag "deve cambiare password" attivo. L'evento viene registrato nel log di audit.

### RF-02: Cambio password obbligatorio al primo accesso

Un utente con il flag "deve cambiare password" viene reindirizzato alla pagina di cambio password dopo il login. Non può navigare altrove finché non completa l'operazione. La nuova password rispetta le policy di sicurezza. Dopo il cambio, il flag viene rimosso.

### RF-03: Dashboard di riepilogo

Pagina principale dell'area admin con: conteggio utenti (totali, attivi, disattivati), nuove registrazioni (7/30 giorni), grafico trend registrazioni, distribuzione utenti per ruolo, ultime 5 attività dal log di audit.

### RF-04: Gestione Utenti

- **Lista**: tabella paginata lato server con ricerca, filtri (ruolo, stato, periodo), ordinamento. Azioni inline: toggle status, link a dettaglio, elimina con conferma.
- **Dettaglio/Modifica**: dati personali, gestione ruoli, info account (sola lettura), cronologia attività. Azioni: salva, reset password forzato, forza cambio password.
- **Creazione manuale**: nome, cognome, email, password temporanea (con auto-generazione), ruoli iniziali, email di benvenuto opzionale. Flag cambio password sempre attivo.

Protezioni: non si può modificare il proprio ruolo SuperAdmin, disattivare/eliminare il SuperAdmin originale, eliminare se stessi.

### RF-05: Gestione Ruoli

- **Lista**: nome, descrizione, numero utenti, indicazione ruolo di sistema. Azioni: modifica, elimina (disabilitato per ruoli di sistema), crea.
- **Dettaglio/Modifica**: matrice permessi raggruppati per area, select/deselect per area, conteggio utenti impattati in tempo reale.
- **Creazione**: come modifica, con opzione di duplicare un ruolo esistente.

Protezione: il SuperAdmin mantiene sempre tutti i permessi.

### RF-06: Log di Audit

Registro cronologico e immutabile. Eventi registrati: autenticazione (login ok/ko, logout, cambio password), utenti (CRUD, cambio stato, ruoli), ruoli (CRUD, modifica permessi), impostazioni, sistema (seeding). Ogni evento include: timestamp, utente, tipo azione, entità, dettaglio modifiche (prima/dopo), IP, browser. UI: tabella paginata con filtri, dettaglio espandibile, export CSV. Il log è append-only e non eliminabile.

### RF-07: Impostazioni di Sistema

Configurazioni modificabili a runtime: nome app, modalità manutenzione, registrazione pubblica, tentativi login max, durata blocco, lunghezza min password, conferma email, timeout sessione, invio email attivo. UI: raggruppate per categoria, controlli appropriati al tipo, chi/quando ultima modifica, salvataggio con conferma. Cache in memoria con invalidazione al salvataggio.

### RF-08: Stato del Sistema

Pagina con: stato DB, stato email, versione app, ambiente, uptime, utilizzo memoria, spazio disco. Indicatore visuale verde/giallo/rosso per ogni componente. Pulsante "Ricontrolla" per aggiornamento manuale.

---

## Requisiti Non Funzionali

### RNF-01: Sicurezza

- Tutti gli endpoint admin richiedono autenticazione + permesso specifico
- Il menu admin mostra solo le sezioni per cui l'utente ha permesso di lettura
- Azioni distruttive richiedono conferma modale
- Un utente non può auto-promuoversi (assegnarsi ruoli/permessi)
- Rate limiting sugli endpoint di modifica

### RNF-02: Performance

- Liste con paginazione lato server
- Permessi utente cachati lato client (al login) e lato server (per richiesta)
- Impostazioni cachate in memoria con invalidazione al salvataggio
- Indici DB sulle query frequenti (audit log, utenti)

### RNF-03: Usabilità

- Layout admin dedicato con sidebar di navigazione
- Ottimizzato per desktop, funzionale su tablet
- Feedback visuale: toast per successo/errore
- Skeleton loading e messaggi per liste vuote
- Breadcrumb o titolo pagina sempre visibile

### RNF-04: Affidabilità

- Audit log append-only e non manipolabile
- Seeding admin idempotente
- Operazioni critiche loggate con dettaglio prima/dopo

### RNF-05: Manutenibilità

- Area admin come modulo isolato e lazy-loaded nel frontend
- Permessi definiti in un unico punto nel codice
- Estensione di permessi/impostazioni senza modifiche strutturali

---

## User Stories

### US-001: Primo accesso dell'admin

**As a** proprietario dell'app,
**I want** che un account SuperAdmin venga creato automaticamente al primo avvio con le credenziali configurate,
**So that** posso accedere immediatamente alla dashboard.

**Acceptance Criteria:**
- [ ] Se non esiste un SuperAdmin, il sistema ne crea uno leggendo `SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`, `SEED_ADMIN_FIRSTNAME`, `SEED_ADMIN_LASTNAME` dalle variabili d'ambiente
- [ ] Se un SuperAdmin esiste già, l'operazione viene saltata senza errori
- [ ] Il nuovo utente ha il flag `MustChangePassword = true`
- [ ] L'operazione avviene dopo le migration del database
- [ ] L'evento viene registrato nel log di audit come operazione di sistema

### US-002: Cambio password obbligatorio

**As a** utente con password temporanea,
**I want** essere obbligato a cambiarla al primo accesso,
**So that** il mio account è sicuro fin da subito.

**Acceptance Criteria:**
- [ ] Dopo il login, se `MustChangePassword` è attivo, il sistema reindirizza alla pagina di cambio password
- [ ] Qualsiasi tentativo di navigare altrove riporta alla pagina di cambio password
- [ ] La nuova password rispetta le policy di sicurezza configurate
- [ ] Dopo il cambio, il flag viene rimosso e la navigazione è libera
- [ ] L'API rifiuta qualsiasi richiesta (tranne cambio password) se il flag è attivo

### US-003: Visualizzare gli utenti registrati

**As a** admin con permesso `Users.Read`,
**I want** vedere la lista di tutti gli utenti con informazioni principali,
**So that** ho una panoramica di chi usa l'applicazione.

**Acceptance Criteria:**
- [ ] Tabella paginata lato server con colonne: nome/avatar, email, ruoli (badge), stato (badge colorato), data registrazione, ultimo accesso
- [ ] Ricerca per nome o email
- [ ] Filtri combinabili: per ruolo, per stato, per periodo di registrazione
- [ ] Ordinamento per qualsiasi colonna
- [ ] Senza permesso `Users.Read`, l'endpoint restituisce 403 e la UI non mostra la sezione

### US-004: Promuovere un utente a un ruolo

**As a** admin con permesso `Users.ManageRoles`,
**I want** assegnare o rimuovere ruoli a un utente,
**So that** posso delegare responsabilità amministrative.

**Acceptance Criteria:**
- [ ] Dalla pagina dettaglio utente posso aggiungere/rimuovere ruoli
- [ ] L'utente promosso vede la voce "Admin" nel menu alla prossima navigazione
- [ ] L'utente promosso vede solo le sezioni per cui ha i permessi
- [ ] L'operazione viene registrata nel log di audit con dettaglio prima/dopo
- [ ] Non è possibile rimuovere il ruolo SuperAdmin dal SuperAdmin originale

### US-005: Disattivare un utente

**As a** admin con permesso `Users.ToggleStatus`,
**I want** attivare/disattivare un account utente,
**So that** posso bloccare l'accesso senza eliminare i dati.

**Acceptance Criteria:**
- [ ] Toggle attivo/disattivo direttamente dalla lista utenti
- [ ] Un utente disattivato non può effettuare il login (il login restituisce errore specifico)
- [ ] Il SuperAdmin originale non può essere disattivato
- [ ] L'operazione viene registrata nel log di audit

### US-006: Creare un utente manualmente

**As a** admin con permesso `Users.Create`,
**I want** creare un account utente senza registrazione pubblica,
**So that** posso invitare collaboratori direttamente.

**Acceptance Criteria:**
- [ ] Campi: nome, cognome, email, password temporanea (con opzione auto-generazione)
- [ ] Possibilità di assegnare ruoli iniziali
- [ ] Opzione per inviare email di benvenuto con credenziali
- [ ] Il nuovo utente ha il flag `MustChangePassword` attivo
- [ ] Email duplicata restituisce errore di validazione

### US-007: Eliminare un utente

**As a** admin con permesso `Users.Delete`,
**I want** eliminare un account utente con conferma esplicita,
**So that** posso rimuovere account non più necessari.

**Acceptance Criteria:**
- [ ] L'eliminazione richiede conferma tramite dialog modale
- [ ] Non si può eliminare se stessi
- [ ] Non si può eliminare il SuperAdmin originale
- [ ] L'operazione viene registrata nel log di audit
- [ ] I dati dell'utente vengono rimossi dal sistema

### US-008: Modificare i dati di un utente

**As a** admin con permesso `Users.Update`,
**I want** modificare nome, cognome ed email di un utente,
**So that** posso correggere o aggiornare i dati.

**Acceptance Criteria:**
- [ ] Dalla pagina dettaglio utente posso modificare nome, cognome, email
- [ ] Reset password forzato: genera un link di reset e lo invia via email
- [ ] Forza cambio password al prossimo accesso (attiva il flag)
- [ ] Le modifiche vengono registrate nel log di audit con dettaglio prima/dopo

### US-009: Creare un ruolo personalizzato

**As a** admin con permesso `Roles.Create`,
**I want** creare ruoli personalizzati con permessi specifici,
**So that** posso definire livelli di accesso su misura.

**Acceptance Criteria:**
- [ ] Definisco nome e descrizione del ruolo
- [ ] Seleziono permessi da una matrice raggruppata per area
- [ ] Posso selezionare/deselezionare tutti i permessi di un'area con un click
- [ ] Posso duplicare un ruolo esistente come punto di partenza
- [ ] Nome ruolo duplicato restituisce errore di validazione

### US-010: Modificare i permessi di un ruolo

**As a** admin con permesso `Roles.Update`,
**I want** modificare i permessi associati a un ruolo,
**So that** posso adattare i livelli di accesso nel tempo.

**Acceptance Criteria:**
- [ ] Vedo chiaramente quali permessi sono attivi e quali no
- [ ] Vedo quanti utenti saranno impattati dalla modifica
- [ ] La modifica viene registrata nel log di audit con dettaglio prima/dopo
- [ ] I ruoli di sistema non possono essere eliminati
- [ ] Il ruolo SuperAdmin mantiene sempre tutti i permessi (modifica ignorata/bloccata)

### US-011: Eliminare un ruolo

**As a** admin con permesso `Roles.Delete`,
**I want** eliminare un ruolo personalizzato,
**So that** posso rimuovere livelli di accesso non più necessari.

**Acceptance Criteria:**
- [ ] L'eliminazione richiede conferma
- [ ] I ruoli di sistema (SuperAdmin, Admin, User) non possono essere eliminati
- [ ] Gli utenti che avevano il ruolo perdono i permessi associati
- [ ] L'operazione viene registrata nel log di audit

### US-012: Consultare il registro delle attività

**As a** admin con permesso `AuditLog.Read`,
**I want** consultare il registro cronologico delle attività,
**So that** posso verificare chi ha fatto cosa e quando.

**Acceptance Criteria:**
- [ ] Eventi in ordine cronologico inverso (più recenti prima)
- [ ] Filtri: tipo di azione, utente, intervallo di date, ricerca testuale
- [ ] Dettaglio espandibile per ogni evento con modifiche prima/dopo
- [ ] Il log non può essere modificato o cancellato da nessuno
- [ ] La tabella è paginata lato server

### US-013: Esportare il registro in CSV

**As a** admin con permesso `AuditLog.Export`,
**I want** esportare il registro filtrato in CSV,
**So that** posso analizzare i dati offline o archiviarli.

**Acceptance Criteria:**
- [ ] L'export rispetta i filtri attualmente applicati
- [ ] Il file CSV contiene tutte le colonne significative
- [ ] Il download avviene come file con nome significativo (es. `audit-log-2026-03-18.csv`)

### US-014: Modificare le impostazioni a runtime

**As a** admin con permesso `Settings.Manage`,
**I want** modificare le impostazioni dell'app senza rideploy,
**So that** posso reagire rapidamente a esigenze operative.

**Acceptance Criteria:**
- [ ] Impostazioni raggruppate per categoria con controlli appropriati al tipo
- [ ] Ogni impostazione mostra chi l'ha modificata per ultimo e quando
- [ ] Il salvataggio richiede conferma
- [ ] Le modifiche hanno effetto immediato (cache invalidata)
- [ ] Le modifiche vengono registrate nel log di audit con dettaglio prima/dopo
- [ ] Con solo `Settings.Read` posso visualizzare ma non modificare

### US-015: Dashboard di riepilogo

**As a** admin con permesso `Dashboard.ViewStats`,
**I want** vedere a colpo d'occhio le metriche principali,
**So that** capisco subito lo stato dell'applicazione.

**Acceptance Criteria:**
- [ ] Conteggi utenti: totali, attivi, disattivati
- [ ] Registrazioni recenti: ultimi 7 e 30 giorni con trend
- [ ] Distribuzione utenti per ruolo (grafico)
- [ ] Ultime 5 attività dal log di audit (widget compatto con link alla sezione completa)

### US-016: Controllare lo stato di salute del sistema

**As a** admin con permesso `SystemHealth.Read`,
**I want** verificare che tutti i componenti funzionino,
**So that** posso intervenire rapidamente in caso di problemi.

**Acceptance Criteria:**
- [ ] Stato di ogni componente con indicatore visuale (verde/giallo/rosso): database, servizio email
- [ ] Informazioni: versione app, ambiente, uptime, utilizzo memoria, spazio disco
- [ ] Pulsante "Ricontrolla" per aggiornamento manuale

### US-017: Accesso condizionale all'area admin

**As a** utente con almeno un permesso amministrativo,
**I want** vedere la voce "Admin" nel menu,
**So that** posso accedere alle funzionalità che mi sono state assegnate.

**Acceptance Criteria:**
- [ ] La voce "Admin" appare solo se l'utente ha almeno un permesso admin
- [ ] Dentro l'area admin, la sidebar mostra solo le sezioni per cui l'utente ha i permessi
- [ ] Accesso via URL diretta a una sezione senza permesso restituisce redirect (frontend) e 403 (API)
- [ ] I permessi dell'utente sono inclusi nella risposta di login e cachati lato client

---

## Dipendenze tra User Stories

```
US-001 (seeding admin) ── prerequisito per tutto
US-002 (cambio password) ── dipende da US-001
US-017 (navigazione admin) ── dipende dal sistema permessi (prerequisito per tutte le US frontend)

US-003..US-008 (gestione utenti) ── dipendono da US-017
US-009..US-011 (gestione ruoli) ── dipendono da US-017
US-012..US-013 (audit log) ── dipendono da US-017, ma l'infrastruttura di logging è prerequisito per US-001
US-014 (impostazioni) ── dipende da US-017
US-015 (dashboard) ── dipende da US-003, US-012 (usa dati utenti e audit)
US-016 (system health) ── dipende da US-017
```

---

## Open Questions

1. **Eliminazione utenti — soft delete o hard delete?** Il documento dice "eliminare", ma per un audit log completo potrebbe essere preferibile un soft delete (flag `IsDeleted`). Questo impatta anche la possibilità di "riattivare" un utente eliminato per errore.

2. **Invalidazione sessioni alla disattivazione**: quando un utente viene disattivato (US-005), le sue sessioni attive (JWT) devono essere invalidate immediatamente o il blocco si applica solo al prossimo tentativo di refresh token?

3. **Invalidazione sessioni al cambio ruoli**: quando i ruoli di un utente cambiano (US-004), i permessi aggiornati si riflettono immediatamente o al prossimo login/refresh?

4. **CLI per seed-admin** (menzionato in RF-01): è una priorità o può essere rimandato? L'approccio via variabili d'ambiente copre il caso principale.

5. **Data retention per audit log**: serve una policy di retention (es. eliminazione automatica dopo N mesi) o il log cresce indefinitamente?

6. **Impostazioni di sistema — valori iniziali**: da dove vengono i valori di default delle impostazioni (RF-07)? Da `appsettings.json`? Hardcoded? Da migration?
