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

Se un parametro non viene passato da CLI, lo script mostra un menu interattivo per selezionarlo.

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

Dopo ogni mini-plan, lo script mostra il contenuto del piano e chiede conferma:

- **y** → approva ed esegue il task
- **n** → salta il task e passa al prossimo
- **q** → interrompe lo script

Consigliato per task con dipendenze dove vuoi verificare il piano prima dell'esecuzione.

### YOLO (solo sandbox)

Claude Code viene lanciato con `--dangerously-skip-permissions`: nessuna whitelist di tool, nessun prompt di conferma. Puo' fare letteralmente qualsiasi cosa — installare pacchetti, eseguire comandi arbitrari, modificare qualsiasi file.

**Sicurezza:** lo script si rifiuta di partire in YOLO mode se non rileva `/.dockerenv` (cioe' non e' dentro un container Docker). Questa modalita' e' usabile solo dentro la sandbox.

### YOLO + Review

Combina YOLO (nessuna restrizione sui permessi) con il review gate (approvi ogni mini-plan). Utile per avere massima flessibilita' di esecuzione ma controllo sul piano.

## Come funziona

Ogni iterazione si compone di due fasi, ognuna in una sessione Claude separata con contesto fresco.

### Fase 1: Planning

Claude legge il piano principale, identifica il primo task `pending`, esplora il codice esistente e genera un mini-plan dettagliato nella cartella `tasks/`. Il mini-plan contiene:

- Contesto e stato attuale del codice
- Dipendenze e vincoli
- Piano di esecuzione step-by-step con path esatti
- Test da scrivere o verificare
- Criteri di completamento

Permessi limitati: solo lettura del codice e scrittura del mini-plan.

### Fase 2: Execution

Una nuova sessione Claude legge il mini-plan appena generato, esegue le modifiche, aggiorna lo stato nel piano principale a `done`, e committa tutto. In fondo al mini-plan aggiunge una sezione con il risultato effettivo, le scelte implementative, e eventuali deviazioni.

Permessi completi: lettura, scrittura, edit, git, build tools.

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

Lo script pre-configura i tool permessi per un tipico stack .NET + Angular. Per personalizzare, modifica le liste `--allowedTools` nelle due fasi:

**Fase PLAN** (solo lettura):

```bash
--allowedTools "Read" "Write" "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" "Bash(mkdir*)"
```

**Fase EXECUTE** (operativa):

```bash
--allowedTools "Read" "Write" "Edit" \
  "Bash(git*)" "Bash(dotnet*)" "Bash(npm*)" "Bash(ng*)" \
  "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" \
  "Bash(mkdir*)" "Bash(cp*)" "Bash(mv*)" "Bash(rm*)" \
  "Bash(cd*)" "Bash(echo*)" "Bash(sed*)" "Bash(docker*)"
```

Se durante un'esecuzione un task fallisce per permessi mancanti, aggiungi il tool alla lista ed esegui di nuovo.

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
