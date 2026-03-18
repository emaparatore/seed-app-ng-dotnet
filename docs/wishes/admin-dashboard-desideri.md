# Admin Dashboard — Requisiti Funzionali

## 1. Visione del Prodotto

La seed-app necessita di un'area amministrativa riservata che permetta al proprietario dell'applicazione di gestire utenti, ruoli, configurazioni e monitorare lo stato del sistema. L'area admin deve essere accessibile solo a utenti autorizzati e deve supportare una delega granulare delle responsabilità amministrative.

---

## 2. Concetti Chiave

### Chi è l'Admin?

Al primo avvio dell'applicazione in produzione, il sistema crea automaticamente un utente **SuperAdmin** — il proprietario dell'app. Le credenziali iniziali vengono lette da variabili d'ambiente (configurate in Docker Compose o nel server). Al primo login, il sistema obbliga il SuperAdmin a cambiare la password.

Da quel momento in poi, il SuperAdmin può gestire tutto dall'interno dell'app: visualizzare gli utenti che si registrano, promuoverli assegnando loro dei ruoli, oppure creare nuovi utenti manualmente.

### Come funzionano i Permessi?

Il sistema di autorizzazione si basa su tre concetti:

- **Permesso**: un'azione specifica che si può fare o non fare. Esempio: "visualizzare la lista utenti", "eliminare un utente", "modificare le impostazioni". Ogni permesso è espresso nel formato `Risorsa.Azione` (es. `Users.Read`, `Settings.Manage`). I permessi sono definiti dal sistema e non modificabili dall'utente.

- **Ruolo**: un insieme di permessi raggruppati sotto un nome. Esempio: il ruolo "Moderator" include i permessi per visualizzare utenti e attivare/disattivare account, ma non per eliminarli. I ruoli possono essere predefiniti dal sistema oppure creati dall'admin.

- **Utente**: ogni utente può avere zero, uno o più ruoli. I permessi effettivi dell'utente sono l'unione di tutti i permessi dei suoi ruoli.

### Ruoli predefiniti

L'applicazione parte con tre ruoli di sistema (non eliminabili):

| Ruolo | Descrizione | Cosa può fare |
|-------|-------------|---------------|
| **SuperAdmin** | Proprietario dell'app | Tutto. Accesso illimitato, bypassa qualsiasi controllo. |
| **Admin** | Amministratore delegato | Quasi tutto: gestisce utenti, ruoli e audit log. Non può modificare le impostazioni di sistema né eliminare ruoli. |
| **User** | Utente base | Accede solo alla parte pubblica dell'applicazione. Nessun accesso all'area admin. |

Il SuperAdmin può creare ruoli personalizzati aggiuntivi. Esempio: un "Moderator" che può solo vedere utenti e abilitarli/disabilitarli, o un "Auditor" che può solo consultare i log.

### Esempio pratico

1. L'app viene deployata. Il sistema crea l'utente `admin@example.com` con ruolo SuperAdmin.
2. L'admin accede, cambia la password, entra nella dashboard.
3. Tre utenti si registrano dall'app pubblica: Marco, Laura, Giulia.
4. L'admin va nella sezione Utenti, vede i tre nuovi iscritti.
5. Decide che Laura deve aiutarlo a moderare. Crea un ruolo "Moderator" con permessi: visualizzare utenti, attivare/disattivare account, visualizzare audit log.
6. Assegna il ruolo "Moderator" a Laura.
7. Laura ora vede nel suo menu la voce "Admin". Accedendo, vede solo le sezioni per cui ha i permessi: lista utenti (in sola lettura tranne il toggle attiva/disattiva) e audit log.
8. Marco e Giulia continuano a usare l'app normalmente, senza vedere nulla dell'area admin.

---

## 3. Catalogo Permessi

I permessi disponibili nel sistema, raggruppati per area:

**Gestione Utenti**
- `Users.Read` — Visualizzare la lista utenti e i dettagli di un utente
- `Users.Create` — Creare un nuovo utente manualmente
- `Users.Update` — Modificare i dati di un utente
- `Users.Delete` — Eliminare un utente
- `Users.ManageRoles` — Assegnare o rimuovere ruoli a un utente
- `Users.ToggleStatus` — Attivare o disattivare un account utente

**Gestione Ruoli**
- `Roles.Read` — Visualizzare i ruoli e i permessi ad essi associati
- `Roles.Create` — Creare un nuovo ruolo personalizzato
- `Roles.Update` — Modificare un ruolo e i suoi permessi
- `Roles.Delete` — Eliminare un ruolo (solo se non è di sistema)

**Log di Audit**
- `AuditLog.Read` — Consultare il registro delle attività
- `AuditLog.Export` — Esportare il registro in formato CSV

**Impostazioni**
- `Settings.Read` — Visualizzare le impostazioni di sistema
- `Settings.Manage` — Modificare le impostazioni di sistema

**Dashboard**
- `Dashboard.ViewStats` — Visualizzare la pagina di riepilogo con le statistiche

**Stato del Sistema**
- `SystemHealth.Read` — Visualizzare lo stato di salute dell'applicazione

---

## 4. Requisiti Funzionali

### RF-01: Seeding dell'Admin iniziale

**Descrizione:** Al primo avvio dell'applicazione, se non esiste nessun utente con ruolo SuperAdmin, il sistema ne crea uno automaticamente.

**Regole:**
- Le credenziali vengono lette da variabili d'ambiente (`SEED_ADMIN_EMAIL`, `SEED_ADMIN_PASSWORD`, `SEED_ADMIN_FIRSTNAME`, `SEED_ADMIN_LASTNAME`)
- L'operazione avviene dopo l'applicazione delle migration del database
- Se un SuperAdmin esiste già, l'operazione viene saltata silenziosamente
- Il nuovo utente viene creato con il flag "deve cambiare password al prossimo accesso"
- L'evento viene registrato nel log di audit come operazione di sistema

**Alternativa CLI:** Deve essere possibile creare un admin anche via riga di comando:
```
dotnet run -- seed-admin --email admin@example.com --password <pwd>
```

### RF-02: Cambio password obbligatorio al primo accesso

**Descrizione:** Un utente con il flag "deve cambiare password" viene forzato a cambiare password prima di poter usare l'applicazione.

**Regole:**
- Dopo il login, se il flag è attivo, il sistema reindirizza l'utente alla pagina di cambio password
- Qualsiasi tentativo di navigare ad altre pagine riporta al cambio password
- La nuova password deve rispettare le policy di sicurezza configurate
- Dopo il cambio, il flag viene rimosso e l'utente può accedere normalmente

### RF-03: Dashboard di riepilogo

**Descrizione:** Pagina principale dell'area admin con una panoramica rapida dello stato dell'applicazione.

**Contenuto:**
- Conteggio utenti totali, attivi e disattivati
- Numero di nuove registrazioni negli ultimi 7 e 30 giorni
- Grafico con il trend delle registrazioni nel tempo
- Distribuzione degli utenti per ruolo (grafico a torta o donut)
- Le ultime 5 attività registrate nel log di audit (widget compatto con link alla sezione completa)

**Permesso richiesto:** `Dashboard.ViewStats`

### RF-04: Gestione Utenti

#### RF-04a: Lista utenti

**Descrizione:** Visualizzazione di tutti gli utenti registrati nell'applicazione.

**Funzionalità:**
- Tabella con paginazione lato server
- Ricerca per nome o email
- Filtri combinabili: per ruolo, per stato (attivo/disattivo), per periodo di registrazione
- Ordinamento per qualsiasi colonna
- Per ogni utente: nome con avatar o iniziali, email, ruoli assegnati (visualizzati come chip/badge), stato (attivo/disattivo come badge colorato), data di registrazione, data ultimo accesso

**Azioni disponibili (in base ai permessi dell'utente corrente):**
- Toggle attiva/disattiva direttamente dalla lista (`Users.ToggleStatus`)
- Link al dettaglio/modifica (`Users.Update`)
- Eliminazione con conferma (`Users.Delete`)

**Permesso richiesto per visualizzare:** `Users.Read`

#### RF-04b: Dettaglio e modifica utente

**Descrizione:** Pagina con tutte le informazioni di un utente e possibilità di modifica.

**Sezioni:**
- **Dati personali**: nome, cognome, email — modificabili con permesso `Users.Update`
- **Ruoli**: lista dei ruoli assegnati con possibilità di aggiungere/rimuovere — richiede `Users.ManageRoles`
- **Informazioni account** (sola lettura): data di creazione, ultimo accesso, ultimo cambio password, flag cambio password obbligatorio
- **Cronologia attività**: ultime N azioni compiute da questo utente, estratte dal log di audit

**Azioni:**
- Salva modifiche
- Reset password forzato: genera un link di reset e lo invia via email all'utente (`Users.Update`)
- Forza cambio password al prossimo accesso (`Users.Update`)

**Regole di protezione:**
- Non si può modificare il proprio ruolo SuperAdmin
- Non si può disattivare o eliminare l'utente SuperAdmin originale
- Non si può eliminare se stessi

#### RF-04c: Creazione utente manuale

**Descrizione:** L'admin può creare un nuovo utente senza che questi passi dalla registrazione pubblica.

**Campi:** nome, cognome, email, password temporanea (con opzione di generazione automatica)
**Opzioni:** assegnazione ruoli iniziali, invio email di benvenuto con credenziali
**Regola:** il nuovo utente avrà sempre il flag "deve cambiare password" attivo

**Permesso richiesto:** `Users.Create`

### RF-05: Gestione Ruoli

#### RF-05a: Lista ruoli

**Descrizione:** Visualizzazione di tutti i ruoli esistenti nel sistema.

**Per ogni ruolo:** nome, descrizione, numero di utenti che lo hanno, indicazione se è un ruolo di sistema (non eliminabile)

**Azioni:**
- Modifica (`Roles.Update`)
- Elimina con conferma (`Roles.Delete`) — disabilitato per ruoli di sistema
- Crea nuovo ruolo (`Roles.Create`)

**Permesso richiesto per visualizzare:** `Roles.Read`

#### RF-05b: Dettaglio e modifica ruolo

**Descrizione:** Pagina per configurare quali permessi sono associati a un ruolo.

**Funzionalità:**
- Modifica nome e descrizione del ruolo
- Matrice dei permessi raggruppati per area (es. "Gestione Utenti", "Log di Audit", ecc.)
- Per ogni area: checkbox per ogni permesso, con possibilità di selezionare/deselezionare tutti i permessi dell'area con un click
- Indicazione in tempo reale di quanti utenti saranno impattati dalla modifica

**Regola:** i ruoli di sistema possono essere modificati nel nome e descrizione, ma il SuperAdmin mantiene sempre tutti i permessi.

**Permesso richiesto:** `Roles.Update`

#### RF-05c: Creazione ruolo

**Descrizione:** Creazione di un nuovo ruolo personalizzato.

**Funzionalità:** come la modifica, partendo da zero oppure duplicando un ruolo esistente come punto di partenza.

**Permesso richiesto:** `Roles.Create`

### RF-06: Log di Audit

**Descrizione:** Registro cronologico e immutabile di tutte le attività significative nell'applicazione.

**Eventi registrati:**
- **Autenticazione**: login riuscito, login fallito, logout, cambio password
- **Utenti**: creazione, modifica, eliminazione, cambio stato, assegnazione/rimozione ruoli
- **Ruoli**: creazione, modifica permessi, eliminazione
- **Impostazioni**: qualsiasi modifica alle configurazioni di sistema
- **Sistema**: seeding iniziale dell'admin

**Informazioni per ogni evento:** data e ora, utente che ha eseguito l'azione (o "Sistema" per eventi automatici), tipo di azione, entità coinvolta, dettaglio delle modifiche (valori prima/dopo), indirizzo IP, browser/dispositivo

**Funzionalità UI:**
- Tabella paginata con filtri: tipo di azione, utente, intervallo di date, ricerca testuale
- Dettaglio espandibile per ogni evento con visualizzazione delle modifiche (prima/dopo)
- Esportazione dei dati filtrati in formato CSV

**Regole:**
- Il log è append-only: non esistono funzionalità di modifica o cancellazione degli eventi
- Il log non è eliminabile nemmeno dal SuperAdmin

**Permessi richiesti:** `AuditLog.Read` per visualizzare, `AuditLog.Export` per esportare

### RF-07: Impostazioni di Sistema

**Descrizione:** Configurazioni dell'applicazione modificabili a runtime senza bisogno di rideploy.

**Impostazioni disponibili:**

| Impostazione | Tipo | Descrizione |
|--------------|------|-------------|
| Nome applicazione | Testo | Nome visualizzato nell'app |
| Modalità manutenzione | Sì/No | Quando attiva, gli utenti normali vedono una pagina di manutenzione |
| Registrazione pubblica | Sì/No | Consenti o blocca la registrazione di nuovi utenti |
| Tentativi login massimi | Numero | Dopo N tentativi falliti, l'account viene bloccato temporaneamente |
| Durata blocco account (minuti) | Numero | Per quanto tempo un account resta bloccato |
| Lunghezza minima password | Numero | Requisito minimo per le password |
| Conferma email obbligatoria | Sì/No | Richiedi verifica email alla registrazione |
| Timeout sessione (minuti) | Numero | Dopo quanto tempo di inattività la sessione scade |
| Invio email attivo | Sì/No | Abilita/disabilita l'invio di email dall'applicazione |

**Funzionalità UI:**
- Impostazioni raggruppate per categoria
- Input appropriato al tipo di dato (toggle per Sì/No, campo numerico per numeri, campo testo per stringhe)
- Salvataggio con conferma
- Per ogni impostazione: indicazione di chi l'ha modificata per ultimo e quando

**Regole:**
- Le modifiche alle impostazioni vengono registrate nel log di audit
- Le impostazioni modificate a runtime prevalgono sui valori di configurazione di default
- Viene utilizzato un sistema di cache in memoria per evitare letture continue dal database, con invalidazione automatica al salvataggio

**Permessi richiesti:** `Settings.Read` per visualizzare, `Settings.Manage` per modificare

### RF-08: Stato del Sistema

**Descrizione:** Pagina che mostra lo stato di salute dell'applicazione e dei suoi componenti.

**Informazioni visualizzate:**
- Stato connessione al database (funzionante / errore)
- Stato del servizio email (raggiungibile / non configurato / errore)
- Versione dell'applicazione
- Ambiente di esecuzione (Development / Staging / Production)
- Tempo di attività del server (uptime)
- Utilizzo memoria dell'applicazione
- Spazio disco disponibile

**Funzionalità UI:**
- Indicatore visuale per ogni componente: verde (OK), giallo (warning), rosso (errore)
- Aggiornamento manuale con pulsante "Ricontrolla"

**Permesso richiesto:** `SystemHealth.Read`

---

## 5. Requisiti Non Funzionali

### RNF-01: Sicurezza
- Tutti gli endpoint dell'area admin devono richiedere autenticazione
- Ogni funzionalità è protetta dal relativo permesso: senza il permesso corretto, l'utente non può né vedere l'elemento nella UI né chiamare l'endpoint API
- Il menu di navigazione dell'area admin mostra solo le sezioni per cui l'utente ha almeno il permesso di lettura
- Tutte le azioni distruttive (eliminazione utente, eliminazione ruolo) richiedono una conferma esplicita tramite dialog modale
- Un utente non può auto-promuoversi: non è possibile assegnarsi ruoli o permessi da soli
- Rate limiting sugli endpoint di modifica per prevenire abusi

### RNF-02: Performance
- Le liste (utenti, audit log) utilizzano paginazione lato server
- I permessi dell'utente corrente vengono caricati una volta al login e cachati lato client
- I permessi vengono cachati anche lato server per evitare query ripetute ad ogni richiesta
- Le impostazioni di sistema vengono cachate in memoria con invalidazione al salvataggio
- Gli indici del database devono coprire le query più frequenti sulle tabelle audit log e utenti

### RNF-03: Usabilità
- L'area admin ha un layout dedicato con una sidebar di navigazione
- Il design è ottimizzato per l'uso desktop ma deve funzionare su tablet
- Ogni operazione fornisce feedback visuale: notifiche toast per successo/errore
- Le liste mostrano stati di caricamento (skeleton) e messaggi informativi quando sono vuote
- La navigazione è consistente: breadcrumb o titolo di pagina sempre visibile

### RNF-04: Affidabilità
- Il log di audit è append-only e non può essere manipolato o eliminato
- Il seeding dell'admin è idempotente: esecuzioni multiple non creano duplicati
- Le operazioni critiche (cambio ruoli, cambio permessi) vengono loggati con il dettaglio completo dei valori prima/dopo la modifica

### RNF-05: Manutenibilità
- L'area admin è un modulo isolato e lazy-loaded nel frontend
- I permessi sono definiti in un unico punto nel codice per facilitarne l'estensione
- L'aggiunta di nuovi permessi o impostazioni di sistema non richiede modifiche strutturali

---

## 6. User Stories

### Primo Avvio e Accesso

**US-01: Primo accesso dell'admin**
> Come proprietario dell'app, quando la applicazione viene avviata per la prima volta, voglio che un account admin venga creato automaticamente con le credenziali che ho configurato, così posso accedere immediatamente alla dashboard.

Criteri di accettazione:
- L'utente SuperAdmin viene creato solo se non ne esiste già uno
- Le credenziali provengono da variabili d'ambiente
- Al primo login vengo obbligato a cambiare la password
- Dopo il cambio password posso navigare liberamente nell'area admin

**US-02: Cambio password obbligatorio**
> Come utente con password temporanea, voglio essere obbligato a cambiarla al primo accesso, così il mio account è sicuro fin da subito.

Criteri di accettazione:
- Dopo il login, se il flag è attivo, vengo reindirizzato alla pagina di cambio password
- Non posso navigare altrove finché non cambio la password
- La nuova password deve rispettare le policy di sicurezza
- Dopo il cambio posso usare l'app normalmente

### Gestione Utenti

**US-03: Visualizzare gli utenti registrati**
> Come admin, voglio vedere la lista di tutti gli utenti registrati con le loro informazioni principali, così ho una panoramica di chi usa l'applicazione.

Criteri di accettazione:
- Vedo nome, email, ruoli, stato e date significative per ogni utente
- Posso cercare per nome o email
- Posso filtrare per ruolo, stato e periodo di registrazione
- La lista è paginata e ordinabile
- Richiede il permesso `Users.Read`

**US-04: Promuovere un utente ad un ruolo**
> Come admin, voglio poter assegnare uno o più ruoli a un utente, così posso delegare responsabilità amministrative.

Criteri di accettazione:
- Dalla pagina dettaglio utente posso aggiungere o rimuovere ruoli
- L'utente promosso vedrà la voce "Admin" nel menu alla prossima navigazione
- L'utente promosso vedrà solo le sezioni per cui ha i permessi
- L'operazione viene registrata nel log di audit
- Richiede il permesso `Users.ManageRoles`

**US-05: Disattivare un utente**
> Come admin, voglio poter disattivare un account utente, così posso bloccare l'accesso senza eliminare i dati.

Criteri di accettazione:
- Posso attivare/disattivare un utente direttamente dalla lista
- Un utente disattivato non può effettuare il login
- L'utente SuperAdmin originale non può essere disattivato
- L'operazione viene registrata nel log di audit
- Richiede il permesso `Users.ToggleStatus`

**US-06: Creare un utente manualmente**
> Come admin, voglio poter creare un account utente senza che la persona debba registrarsi, così posso invitare collaboratori direttamente.

Criteri di accettazione:
- Inserisco nome, cognome, email e opzionalmente una password temporanea
- Posso assegnare ruoli iniziali
- Posso scegliere se inviare un'email di benvenuto
- Il nuovo utente avrà il cambio password obbligatorio al primo accesso
- Richiede il permesso `Users.Create`

**US-07: Eliminare un utente**
> Come admin, voglio poter eliminare un account utente, con una conferma esplicita per evitare errori.

Criteri di accettazione:
- L'eliminazione richiede conferma tramite dialog modale
- Non posso eliminare me stesso
- Non posso eliminare il SuperAdmin originale
- L'operazione viene registrata nel log di audit
- Richiede il permesso `Users.Delete`

### Gestione Ruoli

**US-08: Creare un ruolo personalizzato**
> Come admin, voglio creare ruoli personalizzati con permessi specifici, così posso definire livelli di accesso su misura per la mia organizzazione.

Criteri di accettazione:
- Definisco nome e descrizione del ruolo
- Seleziono i permessi da una matrice raggruppata per area
- Posso selezionare/deselezionare tutti i permessi di un'area con un click
- Posso duplicare un ruolo esistente come punto di partenza
- Richiede il permesso `Roles.Create`

**US-09: Modificare i permessi di un ruolo**
> Come admin, voglio modificare i permessi associati a un ruolo, così posso adattare i livelli di accesso nel tempo.

Criteri di accettazione:
- Vedo chiaramente quali permessi sono attivi e quali no
- Vedo quanti utenti saranno impattati dalla modifica
- La modifica viene registrata nel log di audit con il dettaglio prima/dopo
- I ruoli di sistema non possono essere eliminati
- Il ruolo SuperAdmin mantiene sempre tutti i permessi
- Richiede il permesso `Roles.Update`

### Log di Audit

**US-10: Consultare il registro delle attività**
> Come admin, voglio poter consultare il registro cronologico di tutte le attività significative, così posso verificare chi ha fatto cosa e quando.

Criteri di accettazione:
- Vedo gli eventi in ordine cronologico inverso (più recenti prima)
- Posso filtrare per tipo di azione, utente, intervallo di date
- Posso espandere un evento per vedere il dettaglio delle modifiche
- Il log non può essere modificato o cancellato da nessuno
- Richiede il permesso `AuditLog.Read`

**US-11: Esportare il registro delle attività**
> Come admin, voglio poter esportare il registro filtrato in CSV, così posso analizzare i dati offline o archiviarli.

Criteri di accettazione:
- L'export rispetta i filtri attualmente applicati
- Il file CSV contiene tutte le colonne significative
- Richiede il permesso `AuditLog.Export`

### Impostazioni

**US-12: Modificare le impostazioni a runtime**
> Come admin, voglio modificare le impostazioni dell'applicazione senza dover fare un nuovo deploy, così posso reagire rapidamente a esigenze operative.

Criteri di accettazione:
- Vedo tutte le impostazioni raggruppate per categoria
- Ogni impostazione ha un controllo appropriato al suo tipo (toggle, numero, testo)
- Vedo chi ha modificato ogni impostazione per ultimo e quando
- Il salvataggio richiede conferma
- Le modifiche hanno effetto immediato
- Le modifiche vengono registrate nel log di audit
- Richiede `Settings.Read` per visualizzare, `Settings.Manage` per modificare

### Dashboard e Monitoraggio

**US-13: Avere una panoramica rapida dello stato dell'app**
> Come admin, quando accedo alla dashboard, voglio vedere a colpo d'occhio le metriche principali, così capisco subito lo stato dell'applicazione.

Criteri di accettazione:
- Vedo i conteggi utenti (totali, attivi, disattivi)
- Vedo le registrazioni recenti con trend
- Vedo la distribuzione utenti per ruolo
- Vedo le ultime attività dal log di audit
- Richiede il permesso `Dashboard.ViewStats`

**US-14: Controllare lo stato di salute del sistema**
> Come admin, voglio verificare che tutti i componenti del sistema funzionino correttamente, così posso intervenire rapidamente in caso di problemi.

Criteri di accettazione:
- Vedo lo stato di ogni componente con indicatore visuale (verde/giallo/rosso)
- Vedo versione app, ambiente, uptime, memoria e disco
- Posso aggiornare manualmente lo stato con un pulsante
- Richiede il permesso `SystemHealth.Read`

### Navigazione e Accesso

**US-15: Accesso condizionale all'area admin**
> Come utente con almeno un permesso amministrativo, voglio vedere la voce "Admin" nel menu dell'applicazione, così posso accedere alle funzionalità che mi sono state assegnate.

Criteri di accettazione:
- La voce "Admin" appare nel menu solo se ho almeno un permesso admin
- Dentro l'area admin vedo solo le sezioni per cui ho i permessi
- Se provo ad accedere a una sezione senza permesso (es. via URL diretta), vengo reindirizzato
- L'API rifiuta le richieste se non ho il permesso corretto, indipendentemente dalla UI
