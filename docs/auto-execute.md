# Auto-Execute: esecuzione autonoma di task con Claude Code

Script bash per l'esecuzione autonoma di task con Claude Code. Ogni task viene pianificato e eseguito in una sessione separata con contesto fresco, usando il piano principale come memoria condivisa.

## Prerequisiti

- **Claude Code CLI** installato e configurato
- **Git** inizializzato nel progetto
- Un **piano di implementazione** in formato markdown con task e stati (`pending` / `done`)
- Essere su un **feature branch** (lo script si rifiuta di partire su branch protetti)

## Setup

1. Lo script si trova in `scripts/auto-execute.sh`
2. Rendilo eseguibile:

```bash
chmod +x scripts/auto-execute.sh
```

3. Modifica le variabili in testa allo script secondo il tuo progetto:

```bash
PLANS_DIR="docs/plans"                 # cartella dove cercare i piani
TASKS_DIR="docs/plans/tasks"           # cartella per i mini-plan
MAX_TASKS=50                           # limite di sicurezza
PROTECTED_BRANCHES=("main" "master" "dev" "develop")
```

4. Assicurati di essere su un feature branch:

```bash
git checkout -b feature/nome-feature
```

## Quick start

```bash
# Menu interattivo completo (piano, modalita', modello, effort)
./scripts/auto-execute.sh

# Specifica tutto da CLI (nessun menu)
./scripts/auto-execute.sh --plan docs/plans/my-plan.md --model opus --effort medium full

# Mix: specifica il piano, menu per il resto
./scripts/auto-execute.sh --plan docs/plans/my-plan.md

# Mix: specifica modello e modalita', menu per il piano
./scripts/auto-execute.sh --model sonnet --effort high review
```

Se un parametro non viene passato da CLI, lo script mostra un menu interattivo per selezionarlo. I menu supportano navigazione con frecce `↑↓`, tasti `j`/`k` (vim) o `w`/`s` (WASD), selezione diretta per numero (`1`-`9`), e conferma con `Enter`.

## Struttura attesa del piano

Il piano principale deve contenere task numerati con stato. Esempio:

```markdown
# Piano: Admin Dashboard

## Task 1: Setup entita' User - `done`
## Task 2: CRUD ruoli - `pending`
## Task 3: Permessi per ruolo - `pending`
```

Lo script cerca il primo task con stato `pending`, lo pianifica, lo esegue, e lo segna come `done`.

## Parametri CLI

| Parametro | Valori | Default | Descrizione |
|-----------|--------|---------|-------------|
| `--plan <path>` | path a file `.md` | menu selezione | Piano di implementazione da eseguire |
| `--model <model>` | `opus`, `sonnet`, `haiku` | menu (default: `opus`) | Modello Claude da usare |
| `--effort <level>` | `low`, `medium`, `high`, `max` | menu (default: `medium`) | Livello di ragionamento |
| modalita' | `full`, `review`, `yolo`, `yolo-review` | menu (default: `full`) | Modalita' di esecuzione |

Modelli disponibili:

| Nome | Model ID | Descrizione |
|------|----------|-------------|
| `opus` | `claude-opus-4-6` | Piu' capace, piu' lento |
| `sonnet` | `claude-sonnet-4-6` | Bilanciato |
| `haiku` | `claude-haiku-4-5-20251001` | Veloce, meno capace |

Retrocompatibilita': `./auto-execute.sh false` equivale a `review`, `./auto-execute.sh true` equivale a `full`.

## Modalita' di esecuzione

### Full autonomia (default)

Claude pianifica e esegue ogni task senza intervento. Usa `claude -p` (prompt mode) con permessi pre-configurati. Ideale per task ben definiti e indipendenti. Lo script procede fino al completamento di tutti i task o al raggiungimento di `MAX_TASKS`.

### Review

Due gate di conferma per ogni task:

1. **Prima dell'esecuzione** — lo script mostra il contenuto del mini-plan e chiede conferma:
   - **y** → approva ed esegue il task
   - **n** → salta il task e passa al prossimo
   - **q** → interrompe lo script

2. **Dopo il commit** — lo script chiede se continuare col task successivo:
   - **y** → prosegui
   - **q** → ferma l'esecuzione

Consigliato per task con dipendenze dove vuoi verificare il piano prima dell'esecuzione.

### YOLO (solo sandbox)

Claude Code viene lanciato con `--dangerously-skip-permissions`: nessuna whitelist di tool, nessun prompt di conferma. Puo' fare letteralmente qualsiasi cosa — installare pacchetti, eseguire comandi arbitrari, modificare qualsiasi file.

**Sicurezza:** lo script si rifiuta di partire in YOLO mode se non rileva `/.dockerenv` (cioe' non e' dentro un container Docker). Questa modalita' e' usabile solo dentro la sandbox.

### YOLO + Review

Combina YOLO (nessuna restrizione sui permessi) con il review gate (approvi ogni mini-plan). Utile per avere massima flessibilita' di esecuzione ma controllo sul piano.

## Come funziona

Ogni iterazione passa per quattro fasi, ognuna in una sessione Claude separata con contesto fresco. Modello e phase sono scelti per ottimizzare il costo: i task meccanici (update piano, commit message) vanno su haiku, quelli che richiedono ragionamento restano sul modello scelto dall'utente.

### Fase 1: Planning (modello utente)

Claude legge il piano principale, identifica il primo task `pending`, esplora il codice rilevante (con un budget indicativo di ~6-8 file) e genera un mini-plan nella cartella `tasks/`. Il mini-plan contiene file da toccare, approccio step-by-step, test da scrivere, criteri di completamento.

Il mini-plan e' pensato per essere **self-contained**: la fase di execution non rileggera' il piano principale, quindi tutto il contesto necessario deve essere nel mini-plan.

Permessi: lettura, scrittura del mini-plan, `WebFetch` (per consultare URL referenziati dal piano), ricerca con `find`/`grep`/`ls`. Cap turni: 20.

### Fase 2: Execution (modello utente)

Una nuova sessione Claude legge il mini-plan appena generato ed esegue le modifiche. Aggiunge in fondo al mini-plan una sezione `## Risultato` con file modificati, scelte chiave ed eventuali deviazioni dal piano.

**Disciplina test/build**: il prompt istruisce Claude a non entrare in loop iterativi di build/test — puo' fare verifiche puntuali per validare un'assunzione ma non deve "over-verificare", perche' la verifica finale (build + test) la fa lo script nella fase successiva. Se l'esecuzione fallisce la verifica, parte un retry con contesto pulito e gli errori in input.

Permessi: lettura, scrittura, edit, `WebFetch`, git, build tools (dotnet, npm, ng), comandi di filesystem. Cap turni: 40.

### Fase 3: Verify (script, no Claude)

Lo script esegue `dotnet build` + `dotnet test` e/o `npm run build` in base ai path modificati (se nessun path corrisponde a backend o frontend, esegue entrambi). Se tutto passa, si procede alla fase 4. Altrimenti gli errori vengono catturati e Claude riparte in fase **Fix**: modello `sonnet+high`, stessa whitelist di exec, cap turni 25, prompt che forza Claude a partire dagli errori reali invece di ri-esplorare. Sono concessi fino a `MAX_RETRIES` (default 4) cicli fix+verify prima di dichiarare il task fallito.

Se il task fallisce dopo tutti i retry, lo script mostra un menu di recupero interattivo con quattro opzioni:

- **Riprova** — resetta i contatori e riprova il task da capo (nuova fase Plan)
- **Fix manuale** — lo script si mette in pausa; l'utente corregge il codice, poi premi Enter per rieseguire la verifica
- **Salta** — segna il task come fallito e passa al successivo
- **Esci** — interrompe l'esecuzione

### Fase 4: Update piano + Commit (haiku)

Dopo la verifica ok, lo script controlla se Claude ha creato commit durante l'esecuzione (nonostante le istruzioni esplicite di non farlo). Se sì, esegue un `git reset --soft` per annullare quei commit mantenendo le modifiche su disco, poi consolida tutto in un unico commit gestito dallo script.

Quindi due chiamate rapide su **haiku** (non sul modello scelto dall'utente, perché sono task meccanici):

1. **Update piano**: aggiorna lo stato del task nel piano principale (`[ ]` → `[x]`), spunta le checkbox della Definition of Done e aggiunge Implementation Notes dal Risultato del mini-plan.
2. **Commit message**: genera un commit Conventional Commits dal diff staged.

Lo script poi esegue il `git commit`. Queste due chiamate insieme costano ~$0.05 per task invece dei ~$0.27 che costerebbero su opus.

In modalità **Review**, dopo ogni commit lo script chiede conferma prima di passare al task successivo (secondo gate): `y` per continuare, `q` per fermarsi.

### Rate limit handling

Lo script rileva automaticamente i rate limit della Claude API durante tutte le fasi. Vengono riconosciuti due pattern:

- **Quota utente**: messaggio `"You've hit your limit · resets ..."` nel testo risultato
- **Errori API strutturati**: `rate_limit_error` o `overloaded_error` nel JSONL grezzo

Quando viene rilevato un rate limit, lo script prova a estrarre l'orario di reset dal messaggio (es. `"resets 3pm (UTC)"`) per calcolare l'attesa esatta con 60 secondi di margine.

**In YOLO mode:** l'attesa è automatica con countdown visuale. Se l'orario di reset è noto aspetta fino a quello, altrimenti aspetta 10 minuti.

**In modalità interattiva:** viene mostrato un menu con tre opzioni:

- **Riparti alle `HH:mm (TZ)`** — attende fino all'orario di reset e riprova (opzione disponibile solo se l'orario è stato estratto)
- **Attendi** — l'utente specifica quanti minuti aspettare
- **Esci** — ferma lo script

Il rate limit durante planning o exec interrompe lo script (`exit 1`). Durante l'update piano o la generazione del commit message, il rate limit viene loggato e lo script usa un messaggio di commit di fallback invece di uscire.

### Circuit-breaker: `--max-turns`

Ogni sessione Claude ha un cap di turni (un "turno" = Claude parla/chiama tool → lo script esegue → Claude riceve il risultato). Il cap non è una protezione da impalli veri, è un **limite economico**: quando Claude entra in esplorazione iterativa (tipico caso: 10 `dotnet test` in fila per capire perché un'API di libreria si comporta in modo inatteso) il cache-read esplode senza che il beneficio cresca in modo lineare. Il cap forza a interrompere e lasciare che il ciclo esterno (verify → fix con contesto pulito) faccia il lavoro in modo più economico.

**Comportamento per fase exec:** se la sessione raggiunge il cap, lo script tenta fino a 2 **continuazioni automatiche** (3 invocazioni totali = 120 turni). Ogni continuazione riparte con un prompt che dice a Claude di rileggere il mini-plan, verificare i file già modificati e completare solo il lavoro rimanente senza rifare ciò che è già fatto. Se le continuazioni si esauriscono, lo script mostra un prompt interattivo: `r` per riprovare da capo (reset continuazioni), `q` per uscire.

Per plan e fix, se il cap viene raggiunto lo script tratta la sessione come non riuscita e procede al normale ciclo retry/fallimento.

Non si perde lavoro: le modifiche già scritte su disco restano, il retry legge lo stato attuale.

Valori default: plan 20, exec 40, fix 25, update/commit 15.

## Struttura file generata

```
docs/plans/
├── feature-plan.md                    # piano principale (stati aggiornati)
└── tasks/
    ├── task-01-setup-user-entity.md   # mini-plan + risultato
    ├── task-02-crud-ruoli.md
    └── ...

scripts/logs/
├── execution-20260319-143022.log      # log principale dello script
└── claude/
    ├── execution-20260319-143022.jsonl # azioni Claude complete (JSONL)
    └── execution-20260319-143022.log   # riepilogo testuale leggibile
```

## Personalizzazione allowedTools

Lo script pre-configura i tool permessi per un tipico stack .NET + Angular. Per personalizzare, modifica le liste in `run_claude_cmd`.

**Fase PLAN** (solo lettura + WebFetch):

```bash
--allowedTools "Read" "Write" "WebFetch" \
  "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" "Bash(mkdir*)"
```

**Fase EXECUTE / FIX** (operativa, stessa whitelist):

```bash
--allowedTools "Read" "Write" "Edit" "WebFetch" \
  "Bash(git*)" "Bash(dotnet*)" "Bash(npm*)" "Bash(ng*)" \
  "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" \
  "Bash(mkdir*)" "Bash(cp*)" "Bash(mv*)" "Bash(rm*)" \
  "Bash(cd*)" "Bash(echo*)" "Bash(sed*)" "Bash(docker*)"
```

**Fase UPDATE piano** (haiku, read-only + write sul piano):

```bash
--allowedTools "Read" "Write" "Edit"
```

### YOLO mode + disallowedTools

In YOLO lo script usa `--dangerously-skip-permissions` (nessun prompt di conferma) ma combinato con un `--disallowedTools` mirato per evitare costi nascosti:

```bash
--disallowedTools "Task" "Agent" "WebSearch" "TodoWrite"
```

Perche' questi tool sono bloccati anche in YOLO:

- **`Task` / `Agent`**: spawnano sub-sessioni Claude figlie. Ogni sub-agente e' una sessione completa con il suo costo, che si somma al totale in modo difficile da prevedere. In un workflow dove lo script e' gia' l'orchestratore, non ha senso che Claude biforchi ulteriormente.
- **`WebSearch`**: ricerca broad con 10+ risultati, ogni risultato e' uno snippet da qualche kB. Rumoroso e imprevedibile in termini di context inflation. Per documentarsi su API esterne usa `WebFetch` su un URL specifico referenziato dal piano.
- **`TodoWrite`**: utile in sessioni lunghe e interattive, inutile in sessioni brevi e monotematiche come quelle dell'auto-execute.
- **`WebFetch`** resta **abilitato** per consultazione mirata: Claude lo usa se il mini-plan contiene URL di doc ufficiali o se ha bisogno di un riferimento preciso.

Se durante un'esecuzione un task fallisce per permessi mancanti, aggiungi il tool alla whitelist corrispondente. Se un comportamento costa piu' del previsto, aggiungi il tool a `--disallowedTools` in YOLO.

### Modelli per fase

| Fase | Modello | Effort | Motivo |
|---|---|---|---|
| Plan | `claude-sonnet-4-6` (fisso) | medium (fisso) | Il piano principale ha già path, firme, pattern espliciti — sonnet è sufficiente per la trascrizione contestualizzata |
| Exec | utente (default opus) | utente | Richiede implementazione |
| Fix | `claude-sonnet-4-6` (fisso) | high (fisso) | Gli errori sono già in input; reasoning migliore riduce i retry a catena, che costano più del delta medium→high |
| Update piano | `claude-haiku-4-5` | low | Task meccanico: flip checkbox, scrivere 3-5 bullet dal Risultato |
| Commit msg | `claude-haiku-4-5` | low | Task meccanico: generare Conventional Commit dal diff |

Solo la fase Exec usa il modello e l'effort scelti dall'utente. Plan e Fix hanno modello e effort hardcoded per ottimizzare costo/qualità. L'hardcoding di haiku per update e commit evita di pagare opus per task che qualsiasi modello piccolo gestisce bene.

## Logging

Ogni esecuzione genera automaticamente tre file di log con lo stesso timestamp:

```
scripts/logs/
├── execution-{YYYYMMDD-HHMMSS}.log              # log principale dello script
└── claude/
    ├── execution-{YYYYMMDD-HHMMSS}.jsonl         # azioni Claude complete (JSONL)
    └── execution-{YYYYMMDD-HHMMSS}.log           # riepilogo testuale leggibile
```

### Log principale (`logs/execution-*.log`)

Output dello script: avanzamento task, risultati build/test, commit, errori. Visibile anche a terminale.

### Log Claude JSONL (`logs/claude/execution-*.jsonl`)

Ogni azione di Claude in formato stream-json (una riga JSON per evento). Contiene:
- Tool calls (Read, Edit, Write, Bash, Grep, Glob...)
- Input e output di ogni tool
- Testo generato da Claude
- Costi e durata di ogni sessione

Utile per debug approfondito o analisi automatizzata. Richiede `jq` per la lettura:

```bash
# Vedere tutti i tool usati in una sessione
cat logs/claude/execution-*.jsonl | jq 'select(.type=="assistant") | .message.content[]? | select(.type=="tool_use") | {tool: .name, input: .input}'

# Costi totali
cat logs/claude/execution-*.jsonl | jq 'select(.type=="result") | {cost: .cost_usd, duration_s: (.duration_ms/1000)}'
```

### Riepilogo Claude (`logs/claude/execution-*.log`)

Versione leggibile del log JSONL, organizzata per fase. Esempio:

```
=== [Task 1 - Plan] 14:30:05 ===
  > Read: docs/plans/PLAN-1.md
  > Read: backend/src/Seed.Api/Controllers/AdminController.cs
  > Grep: UserRole in backend/src/
  > Write: docs/plans/tasks/task-1-setup.md
  Claude: PLANNED:task-1-setup.md
  --- Result: PLANNED:task-1-setup.md | cost: $0.05 | 12s

=== [Task 1 - Exec] 14:35:22 ===
  > Read: docs/plans/tasks/task-1-setup.md
  > Edit: backend/src/Seed.Domain/Entities/User.cs
  > Bash: cd backend && dotnet build Seed.slnx
  > Write: backend/tests/Seed.UnitTests/UserTests.cs
  Claude: DONE
  --- Result: DONE | cost: $0.12 | 45s
```

Se `jq` non e' installato, il riepilogo mostra solo il conteggio dei tool call e il risultato. Installa `jq` per il dettaglio completo.

Per salvare anche l'output live su un file separato:

```bash
./scripts/auto-execute.sh full 2>&1 | tee esecuzione-live.txt
```

## Sandbox Docker

Container Docker isolato con .NET SDK 10.0, Node.js 22 e Claude Code CLI pre-installati, connesso ai servizi di sviluppo del progetto (PostgreSQL, Seq, Mailpit). Il codice del progetto e' montato via bind mount, quindi ogni modifica persiste sul disco host.

### Quando usare la sandbox

| Modalita' | Locale | Sandbox |
|---|---|---|
| **Review** | Va bene | Meglio per sessioni lunghe |
| **Full autonomia** | Rischioso | Consigliato |
| **YOLO** | Bloccato dallo script | Obbligatorio |

La sandbox e' **obbligatoria** per YOLO mode e **consigliata** per le modalita' autonome (specialmente sessioni lunghe o notturne). I vantaggi rispetto all'esecuzione locale:

- **Isolamento filesystem/rete** — se qualcosa va storto, il danno e' contenuto nel container
- **Ambiente preconfigurato** — .NET SDK, Node.js, git e servizi pronti senza setup locale
- **Nessun accesso al sistema host** — solo il progetto e' montato

### Prerequisiti sandbox

- **Docker Desktop** per Windows (con WSL 2 backend)
- **WSL 2 consigliato** — esegui `docker compose` da WSL per evitare problemi di path translation e compatibilita' shell. Git Bash (MSYS) funziona ma richiede workaround (`MSYS_NO_PATHCONV=1`)
- **Claude Code CLI** autenticato localmente — esegui `claude` almeno una volta sulla macchina host per completare l'autenticazione OAuth. I token vengono salvati in `%USERPROFILE%\.claude`

### Quick start sandbox

```bash
# Dalla root del progetto (consigliato: esegui da WSL)

# 1. Configura CLAUDE_CONFIG_DIR nel file docker/.env
#    Su WSL: CLAUDE_CONFIG_DIR=/mnt/c/Users/<tuo-username>

# 2. Rendi eseguibile lo script
chmod +x scripts/auto-execute.sh

# 3. Avvia servizi dev + sandbox
docker compose --profile sandbox up --build

# 4. Entra nel container (da un altro terminale)
docker exec -it seed-sandbox bash

# Dentro il container:
./scripts/auto-execute.sh                                    # menu interattivo
./scripts/auto-execute.sh --plan docs/plans/my-plan.md full  # diretto
./scripts/auto-execute.sh yolo                               # YOLO mode (solo qui!)

# Piu' terminali sullo stesso container
docker exec -it seed-sandbox bash        # da un altro terminale

# Ferma tutto
docker compose --profile sandbox down
```

### Connettivita' di rete

Il container sandbox si collega alla stessa rete Docker dei servizi dev:

| Servizio | Host (dal container) | Porta |
|----------|---------------------|-------|
| PostgreSQL | `seed-postgres-dev` | 5432 |
| Seq | `seed-seq-dev` | 5341 |
| Mailpit SMTP | `seed-mailpit-dev` | 1025 |

La connection string PostgreSQL e' gia' configurata come variabile d'ambiente nel container:

```
Host=seed-postgres-dev;Database=seeddb;Username=seed;Password=seed_password
```

### Autenticazione Claude Code nella sandbox

L'autenticazione avviene tramite volume mount della cartella `.claude` dall'host al container. Il docker-compose usa la variabile `CLAUDE_CONFIG_DIR` con fallback su `$HOME`:

```yaml
- ${CLAUDE_CONFIG_DIR:-${HOME}}/.claude:/home/claude/.claude
```

**Setup per piattaforma:**

| Piattaforma | Configurazione in `docker/.env` |
|---|---|
| **WSL** (consigliato) | `CLAUDE_CONFIG_DIR=/mnt/c/Users/<tuo-username>` |
| **Windows** (Git Bash / PowerShell) | Non serve — il fallback `$HOME` usa la home utente corrente |

- Non serve `ANTHROPIC_API_KEY`. Claude Code usa OAuth.
- I token vengono letti dal mount in tempo reale. Se scadono, basta ri-autenticarsi sull'host (`claude` da terminale locale) — non serve riavviare il container.

### File sandbox

```
docker/Dockerfile.sandbox             # Immagine: .NET SDK 10.0 + Node 22 + Claude CLI
docker/docker-compose.yml             # Servizio sandbox (profilo "sandbox") + servizi dev
scripts/auto-execute.sh               # Script di esecuzione autonoma
```

## Protezioni di sicurezza

- **Branch protetti**: si rifiuta di partire se il branch corrente e' `main`, `master`, `dev` o `develop`. Configurabile tramite `PROTECTED_BRANCHES`.
- **Limite task**: `MAX_TASKS` impedisce loop infiniti (default: 50).
- **Permessi separati**: la fase plan ha permessi minimi (solo lettura); la fase execute ha permessi operativi ma espliciti.
- **YOLO solo in sandbox**: lo script verifica `/.dockerenv` prima di avviare YOLO mode.
- **Git come safety net**: ogni task viene committato separatamente, rendendo facile il revert di un singolo task.

Le protezioni sui branch remoti (merge, push su branch protetti) vanno configurate lato repository (GitHub/GitLab/etc.).

## Troubleshooting

**Lo script esce subito con errore "branch protetto"**: crea un feature branch con `git checkout -b feature/nome`.

**"Nessun file di plan generato"**: Claude non e' riuscito a generare il mini-plan. Controlla il log per dettagli. Possibili cause: formato del piano non riconosciuto, nessun task `pending` trovato con il pattern atteso.

**Task fallisce per permessi**: aggiungi il tool mancante alla lista `--allowedTools` della fase execute.

**Claude non trova il prossimo task**: verifica che il piano usi esattamente `pending` e `done` come stati e che il formato sia consistente.

**Contesto insufficiente per task complessi**: aggiungi piu' dettaglio nel piano principale o nelle note dei task precedenti. Il mini-plan e' il ponte tra sessioni — piu' e' ricco, meglio Claude esegue.

**Errori di autenticazione Claude Code nella sandbox**: Verifica che `%USERPROFILE%\.claude` contenga file `.json`. Se vuota, esegui `claude` localmente per autenticarti.

**Path translation su Git Bash (MSYS)**: Se vedi errori di path con Docker su Git Bash, prefissa con:

```bash
MSYS_NO_PATHCONV=1 docker exec -it seed-sandbox bash
```

**Container non parte / build fallisce**: Verifica che Docker Desktop sia avviato e che WSL 2 sia il backend. Prova a rebuildare:

```bash
docker compose --profile sandbox build --no-cache
```

**Dimensione immagine grande (~2-3 GB)**: Normale, include .NET SDK + Node.js + Claude CLI. Il primo build richiede qualche minuto.
