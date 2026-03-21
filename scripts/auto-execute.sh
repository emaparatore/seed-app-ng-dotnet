#!/bin/bash
# auto-execute.sh - Esecuzione autonoma dei task con Claude Code
# Uso:
#   ./auto-execute.sh                              → menu interattivo completo
#   ./auto-execute.sh --plan docs/plans/my-plan.md → menu modalita' + modello
#   ./auto-execute.sh --plan docs/plans/my-plan.md --model sonnet yolo → tutto da CLI
#   ./auto-execute.sh --model opus --effort high review → menu solo piano
#
# Modalita':
#   full        → full autonomia (auto-approve) [default]
#   review      → approvi ogni mini-plan prima dell'esecuzione
#   yolo        → zero restrizioni (solo in sandbox Docker)
#   yolo-review → YOLO + review di ogni plan
#
# Modelli (--model):
#   opus    → claude-opus-4-6 [default]
#   sonnet  → claude-sonnet-4-6
#   haiku   → claude-haiku-4-5-20251001
#
# Effort (--effort):
#   low, medium [default], high, max
#
# Log salvato automaticamente in execution-{timestamp}.log

PLANS_DIR="docs/plans"
TASKS_DIR="docs/plans/tasks"
MAX_TASKS=50
LOG_FILE="execution-$(date +%Y%m%d-%H%M%S).log"
PROTECTED_BRANCHES=("main" "master" "dev" "develop")

# --- Arrow-key menu selector ---
# Uso: select_menu "Titolo" result_var "opzione1" "opzione2" ...
# Opzionale: default index (0-based) come ultimo argomento numerico dopo --default
select_menu() {
  local title="$1"
  local -n _result="$2"
  shift 2

  local default_idx=0
  local options=()
  while [[ $# -gt 0 ]]; do
    if [[ "$1" == "--default" ]]; then
      default_idx="$2"
      shift 2
    else
      options+=("$1")
      shift
    fi
  done

  local selected=$default_idx
  local count=${#options[@]}

  # Nascondi cursore
  printf "\033[?25l"

  # Header
  echo ""
  echo "========================================="
  echo "  $title"
  echo "========================================="
  echo ""

  # Disegna opzioni
  for i in "${!options[@]}"; do
    if [ $i -eq $selected ]; then
      printf "  \033[7m > %s \033[0m\n" "${options[$i]}"
    else
      printf "    %s\n" "${options[$i]}"
    fi
  done

  # Loop input
  while true; do
    # Leggi un tasto
    IFS= read -rsn1 key
    if [[ "$key" == $'\x1b' ]]; then
      read -rsn2 seq
      case "$seq" in
        '[A') # Freccia su
          ((selected > 0)) && ((selected--))
          ;;
        '[B') # Freccia giu'
          ((selected < count - 1)) && ((selected++))
          ;;
      esac
    elif [[ "$key" == "" ]]; then
      # Enter premuto
      break
    fi

    # Ridisegna: torna su di $count righe
    printf "\033[%dA" "$count"
    for i in "${!options[@]}"; do
      printf "\033[2K"  # Cancella riga
      if [ $i -eq $selected ]; then
        printf "  \033[7m > %s \033[0m\n" "${options[$i]}"
      else
        printf "    %s\n" "${options[$i]}"
      fi
    done
  done

  # Mostra cursore
  printf "\033[?25h"
  echo ""

  _result=$selected
}

# --- Default modello e effort ---
CLAUDE_MODEL=""
CLAUDE_EFFORT=""

# --- Parsing argomenti ---
PLAN=""
MODE=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --plan)
      PLAN="$2"
      shift 2
      ;;
    --model)
      CLAUDE_MODEL="$2"
      shift 2
      ;;
    --effort)
      CLAUDE_EFFORT="$2"
      shift 2
      ;;
    full|review|yolo|yolo-review)
      MODE="$1"
      shift
      ;;
    # Retrocompatibilita': vecchi argomenti posizionali
    false)
      MODE="review"
      shift
      ;;
    true)
      MODE="full"
      shift
      ;;
    *)
      echo "Argomento sconosciuto: $1"
      echo "Uso: ./auto-execute.sh [--plan <path>] [--model <model>] [--effort <effort>] [full|review|yolo|yolo-review]"
      exit 1
      ;;
  esac
done

# --- Menu selezione piano ---
if [ -z "$PLAN" ]; then
  PLAN_FILES=($(find "$PLANS_DIR" -maxdepth 1 -name "*.md" -type f 2>/dev/null | sort))

  if [ ${#PLAN_FILES[@]} -eq 0 ]; then
    echo "Nessun piano trovato in $PLANS_DIR/"
    echo "Crea un piano .md e riprova."
    exit 1
  fi

  # Costruisci le label per il menu
  PLAN_LABELS=()
  for i in "${!PLAN_FILES[@]}"; do
    FILENAME=$(basename "${PLAN_FILES[$i]}")
    TITLE=$(grep -m1 '^#' "${PLAN_FILES[$i]}" 2>/dev/null | sed 's/^#\+\s*//')
    if [ -n "$TITLE" ]; then
      PLAN_LABELS+=("$FILENAME — $TITLE")
    else
      PLAN_LABELS+=("$FILENAME")
    fi
  done

  PLAN_CHOICE=0
  select_menu "Seleziona il piano di esecuzione" PLAN_CHOICE "${PLAN_LABELS[@]}"
  PLAN="${PLAN_FILES[$PLAN_CHOICE]}"
fi

# Verifica che il piano esista
if [ ! -f "$PLAN" ]; then
  echo "ERRORE: piano non trovato: $PLAN"
  exit 1
fi

# --- Menu selezione modalita' ---
if [ -z "$MODE" ]; then
  MODE_CHOICE=0
  select_menu "Seleziona la modalita' di esecuzione" MODE_CHOICE \
    "Full autonomia  — pianifica e esegue senza intervento" \
    "Review          — approvi ogni mini-plan prima dell'esecuzione" \
    "YOLO            — zero restrizioni (solo sandbox Docker)" \
    "YOLO + Review   — zero restrizioni + approvi ogni plan"

  case $MODE_CHOICE in
    0) MODE="full" ;;
    1) MODE="review" ;;
    2) MODE="yolo" ;;
    3) MODE="yolo-review" ;;
  esac
fi

# --- Menu selezione modello ---
if [ -z "$CLAUDE_MODEL" ]; then
  MODEL_CHOICE=0
  select_menu "Seleziona il modello Claude" MODEL_CHOICE \
    "Opus   — claude-opus-4-6 (piu' capace, piu' lento)" \
    "Sonnet — claude-sonnet-4-6 (bilanciato)" \
    "Haiku  — claude-haiku-4-5 (veloce, meno capace)"

  case $MODEL_CHOICE in
    0) CLAUDE_MODEL="opus" ;;
    1) CLAUDE_MODEL="sonnet" ;;
    2) CLAUDE_MODEL="haiku" ;;
  esac
fi

# --- Menu selezione effort ---
if [ -z "$CLAUDE_EFFORT" ]; then
  EFFORT_CHOICE=0
  select_menu "Seleziona il livello di effort" EFFORT_CHOICE --default 1 \
    "Low    — risposte rapide, meno ragionamento" \
    "Medium — bilanciato" \
    "High   — ragionamento approfondito, piu' lento" \
    "Max    — ragionamento massimo, il piu' lento"

  case $EFFORT_CHOICE in
    0) CLAUDE_EFFORT="low" ;;
    1) CLAUDE_EFFORT="medium" ;;
    2) CLAUDE_EFFORT="high" ;;
    3) CLAUDE_EFFORT="max" ;;
  esac
fi

# --- Risolvi model ID dal nome breve ---
case "$CLAUDE_MODEL" in
  opus)   CLAUDE_MODEL_ID="claude-opus-4-6" ;;
  sonnet) CLAUDE_MODEL_ID="claude-sonnet-4-6" ;;
  haiku)  CLAUDE_MODEL_ID="claude-haiku-4-5-20251001" ;;
  *)
    echo "ERRORE: modello sconosciuto: $CLAUDE_MODEL"
    echo "Valori ammessi: opus, sonnet, haiku"
    exit 1
    ;;
esac

# --- Valida effort ---
case "$CLAUDE_EFFORT" in
  low|medium|high|max) ;;
  *)
    echo "ERRORE: effort sconosciuto: $CLAUDE_EFFORT"
    echo "Valori ammessi: low, medium, high, max"
    exit 1
    ;;
esac

CLAUDE_FLAGS="--model $CLAUDE_MODEL_ID --reasoning-effort $CLAUDE_EFFORT"

# --- Deriva YOLO_MODE e AUTO_APPROVE dalla modalita' ---
case "$MODE" in
  full)
    YOLO_MODE=false
    AUTO_APPROVE=true
    ;;
  review)
    YOLO_MODE=false
    AUTO_APPROVE=false
    ;;
  yolo)
    YOLO_MODE=true
    AUTO_APPROVE=true
    ;;
  yolo-review)
    YOLO_MODE=true
    AUTO_APPROVE=false
    ;;
esac

# --- Safety check: branch protetti ---
CURRENT_BRANCH=$(git branch --show-current)
for branch in "${PROTECTED_BRANCHES[@]}"; do
  if [ "$CURRENT_BRANCH" = "$branch" ]; then
    echo "ERRORE: sei su branch protetto '$CURRENT_BRANCH'. Crea un feature branch prima."
    exit 1
  fi
done

# --- Safety check: YOLO solo in sandbox ---
if [ "$YOLO_MODE" = "true" ] && [ ! -f /.dockerenv ]; then
  echo "=============================================="
  echo "  ERRORE: YOLO mode va eseguito SOLO"
  echo "  dentro la sandbox Docker!"
  echo ""
  echo "  Uso:"
  echo "    docker compose --profile sandbox up --build"
  echo "    docker exec -it seed-sandbox bash"
  echo "    ./auto-execute.sh yolo"
  echo "=============================================="
  exit 1
fi

# --- Logging ---
log() {
  echo "[$(date +%H:%M:%S)] $1" | tee -a "$LOG_FILE"
}

mkdir -p "$TASKS_DIR"

# --- Costruzione opzioni Claude ---
# In YOLO mode: --dangerously-skip-permissions (nessuna restrizione)
# In modalita' normale: whitelist di tool specifici
build_plan_cmd() {
  local prompt="$1"
  if [ "$YOLO_MODE" = "true" ]; then
    claude -p "$prompt" $CLAUDE_FLAGS --dangerously-skip-permissions 2>&1
  else
    claude -p "$prompt" $CLAUDE_FLAGS \
      --allowedTools "Read" "Write" "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" "Bash(mkdir*)" 2>&1
  fi
}

build_exec_cmd() {
  local prompt="$1"
  if [ "$YOLO_MODE" = "true" ]; then
    claude -p "$prompt" $CLAUDE_FLAGS --dangerously-skip-permissions 2>&1
  else
    claude -p "$prompt" $CLAUDE_FLAGS \
      --allowedTools \
        "Read" "Write" "Edit" \
        "Bash(git*)" \
        "Bash(dotnet*)" \
        "Bash(npm*)" \
        "Bash(ng*)" \
        "Bash(find*)" \
        "Bash(grep*)" \
        "Bash(cat*)" \
        "Bash(ls*)" \
        "Bash(mkdir*)" \
        "Bash(cp*)" \
        "Bash(mv*)" \
        "Bash(rm*)" \
        "Bash(cd*)" \
        "Bash(echo*)" \
        "Bash(sed*)" \
        "Bash(docker*)" 2>&1
  fi
}

# --- Riepilogo configurazione ---
echo ""
echo "========================================="
echo "  Piano:     $(basename "$PLAN")"
echo "  Modalita': $MODE"
echo "  Branch:    $(git branch --show-current)"
echo "  Modello:   $CLAUDE_MODEL ($CLAUDE_MODEL_ID)"
echo "  Effort:    $CLAUDE_EFFORT"
echo "========================================="
echo ""

if [ "$YOLO_MODE" = "true" ]; then
  log "=========================================="
  log "  YOLO MODE - Permessi totali (sandbox)"
  log "=========================================="
fi
log "Avvio esecuzione - Modalita': $MODE - Modello: $CLAUDE_MODEL ($CLAUDE_EFFORT)"
log "Piano: $PLAN"
log "Branch: $CURRENT_BRANCH"

for i in $(seq 1 $MAX_TASKS); do
  log "=== Task $i - Planning ==="

  OUTPUT=$(build_plan_cmd "Sei in autonomous execution mode - FASE PLAN.
1. Leggi il piano in $PLAN
2. Trova il primo task con stato 'pending'
3. Se non ce ne sono, rispondi SOLO: ALL_COMPLETE
4. Esplora il codice esistente rilevante
5. Crea un file in $TASKS_DIR/ con nome task-{numero}-{slug}.md contenente:
   # Task {numero}: {titolo}
   ## Contesto
   - Stato attuale del codice rilevante
   - Dipendenze e vincoli
   ## Piano di esecuzione
   - File da creare/modificare (path esatti)
   - Approccio tecnico step-by-step
   - Test da scrivere/verificare
   ## Criteri di completamento
   - Cosa deve funzionare per considerare il task done
6. Rispondi SOLO con: PLANNED:{filename} o ALL_COMPLETE")

  echo "$OUTPUT" >> "$LOG_FILE"
  log "Output plan: $(echo "$OUTPUT" | tail -1)"

  if echo "$OUTPUT" | grep -q "ALL_COMPLETE"; then
    log "Tutti i task completati!"
    break
  fi

  TASK_FILE=$(echo "$OUTPUT" | grep -oP 'PLANNED:\K.*')

  if [ -z "$TASK_FILE" ]; then
    log "ERRORE: nessun file di plan generato, skip"
    continue
  fi

  # --- Review gate ---
  if [ "$AUTO_APPROVE" != "true" ]; then
    echo ""
    echo "========================================="
    echo "Mini-plan: $TASKS_DIR/$TASK_FILE"
    echo "========================================="
    cat "$TASKS_DIR/$TASK_FILE"
    echo "========================================="
    read -p "Approvare? (y/n/q) " confirm
    case "$confirm" in
      q) log "Interrotto dall'utente."; exit 0 ;;
      y) log "Plan approvato manualmente" ;;
      *) log "Task skippato dall'utente"; continue ;;
    esac
  fi

  log "=== Task $i - Execution ($TASK_FILE) ==="

  OUTPUT=$(build_exec_cmd "Sei in autonomous execution mode - FASE EXECUTE.
1. Leggi il mini-plan in $TASKS_DIR/$TASK_FILE
2. Leggi il piano principale in $PLAN per contesto generale
3. Esegui esattamente quello descritto nel mini-plan
4. Verifica i criteri di completamento (build, test, lint)
5. Aggiorna lo stato del task a 'done' in $PLAN
6. Aggiungi in fondo al mini-plan una sezione:
   ## Risultato
   - File modificati/creati
   - Scelte implementative e motivazioni
   - Eventuali deviazioni dal piano e perche'
7. Committa tutto con messaggio semantico
8. Rispondi SOLO: DONE")

  echo "$OUTPUT" >> "$LOG_FILE"
  log "Risultato: $(echo "$OUTPUT" | tail -1)"

  sleep 3
done

log "=== SUMMARY ==="
log "Log completo: $LOG_FILE"
log "Mini-plan in $TASKS_DIR/:"
ls -la "$TASKS_DIR/" | tee -a "$LOG_FILE"
