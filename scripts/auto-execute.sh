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
MAX_RETRIES=4
LOG_DIR="$(cd "$(dirname "$0")" && pwd)/logs"
EXECUTION_TS="$(date +%Y%m%d-%H%M%S)"
LOG_FILE="$LOG_DIR/execution-${EXECUTION_TS}.log"
CLAUDE_LOG_DIR="$LOG_DIR/claude"
CLAUDE_LOG_JSONL="$CLAUDE_LOG_DIR/execution-${EXECUTION_TS}.jsonl"
CLAUDE_LOG_SUMMARY="$CLAUDE_LOG_DIR/execution-${EXECUTION_TS}.log"
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
  local cols
  cols=$(tput cols 2>/dev/null || echo 80)

  # Tronca opzioni per evitare wrapping (6 char per prefisso " > " / "    ")
  local max_text=$((cols - 7))
  local display_opts=()
  for opt in "${options[@]}"; do
    if [ ${#opt} -gt $max_text ]; then
      display_opts+=("${opt:0:$max_text}")
    else
      display_opts+=("$opt")
    fi
  done

  # Nascondi cursore
  printf "\033[?25l"

  # Header
  echo ""
  echo "========================================="
  echo "  $title"
  echo "========================================="
  echo ""

  # Disegna opzioni
  for i in "${!display_opts[@]}"; do
    if [ $i -eq $selected ]; then
      printf "  \033[7m > %s \033[0m\n" "${display_opts[$i]}"
    else
      printf "    %s\n" "${display_opts[$i]}"
    fi
  done

  # Loop input (frecce, j/k, numeri)
  while true; do
    IFS= read -rsn1 key < /dev/tty
    local moved=false

    case "$key" in
      $'\x1b')
        # Sequenza escape: leggi resto della sequenza
        IFS= read -rsn1 -t 0.2 ch1 < /dev/tty 2>/dev/null
        IFS= read -rsn1 -t 0.2 ch2 < /dev/tty 2>/dev/null
        case "$ch1$ch2" in
          '[A') ((selected > 0)) && ((selected--)); moved=true ;;
          '[B') ((selected < count - 1)) && ((selected++)); moved=true ;;
        esac
        ;;
      k|K|w|W) # Su (vim/wasd)
        ((selected > 0)) && ((selected--)); moved=true
        ;;
      j|J|s|S) # Giu' (vim/wasd)
        ((selected < count - 1)) && ((selected++)); moved=true
        ;;
      [0-9]) # Selezione diretta per numero (1-based)
        local num=$((key))
        if [ $num -ge 1 ] && [ $num -le $count ]; then
          selected=$((num - 1)); moved=true
        fi
        ;;
      '') # Enter
        break
        ;;
    esac

    if [ "$moved" = "true" ]; then
      # Ridisegna: torna su di $count righe
      printf "\033[%dA" "$count"
      for i in "${!display_opts[@]}"; do
        printf "\033[2K"  # Cancella riga
        if [ $i -eq $selected ]; then
          printf "  \033[7m > %s \033[0m\n" "${display_opts[$i]}"
        else
          printf "    %s\n" "${display_opts[$i]}"
        fi
      done
    fi
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

CLAUDE_FLAGS="--verbose --model $CLAUDE_MODEL_ID --effort $CLAUDE_EFFORT"

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
mkdir -p "$LOG_DIR"
mkdir -p "$CLAUDE_LOG_DIR"

# --- Rate limit detection ---
# Pattern noti di rate limit / quota esaurita da Claude API
RATE_LIMIT_PATTERNS=(
  "hit your limit"
  "rate limit"
  "quota exceeded"
  "too many requests"
  "resets .* (UTC)"
  "capacity"
  "overloaded"
)

# Controlla se l'output di Claude indica rate limiting.
# Cerca nei dati JSONL grezzi (non solo nel result estratto).
# Ritorna 0 se rate-limited, 1 se ok.
# Se rate-limited, imposta RATE_LIMIT_MSG con il messaggio rilevato.
check_rate_limit() {
  local jsonl_file="$1"
  local result_text="$2"
  RATE_LIMIT_MSG=""

  # Controlla nel result text
  for pattern in "${RATE_LIMIT_PATTERNS[@]}"; do
    if echo "$result_text" | grep -iqE "$pattern"; then
      RATE_LIMIT_MSG="$result_text"
      return 0
    fi
  done

  # Controlla anche nel JSONL grezzo (potrebbe essere in un messaggio di testo)
  for pattern in "${RATE_LIMIT_PATTERNS[@]}"; do
    local match
    match=$(grep -i "$pattern" "$jsonl_file" 2>/dev/null | head -1)
    if [ -n "$match" ]; then
      RATE_LIMIT_MSG=$(echo "$match" | head -c 200)
      return 0
    fi
  done

  return 1
}

# Calcola i secondi di attesa fino a un orario UTC (es. "3pm", "11am").
# Ritorna i secondi via echo. Se non riesce a parsare, ritorna 0.
calc_wait_seconds() {
  local reset_time="$1"  # es. "3pm", "11am"
  local hour minute ampm

  # Estrai ora e am/pm
  hour=$(echo "$reset_time" | grep -oP '^\d+')
  ampm=$(echo "$reset_time" | grep -oP '[ap]m$')

  if [ -z "$hour" ] || [ -z "$ampm" ]; then
    echo 0
    return
  fi

  # Converti in formato 24h
  if [ "$ampm" = "pm" ] && [ "$hour" -ne 12 ]; then
    hour=$((hour + 12))
  elif [ "$ampm" = "am" ] && [ "$hour" -eq 12 ]; then
    hour=0
  fi

  # Orario attuale in UTC (epoch seconds)
  local now_utc
  now_utc=$(date -u +%s)

  # Costruisci il target come oggi alle $hour:00 UTC
  local today_date
  today_date=$(date -u +%Y-%m-%d)
  local target_utc
  target_utc=$(date -u -d "$today_date $hour:00:00" +%s 2>/dev/null)

  # Fallback per macOS/busybox
  if [ -z "$target_utc" ]; then
    target_utc=$(TZ=UTC date -j -f "%Y-%m-%d %H:%M:%S" "$today_date $hour:00:00" +%s 2>/dev/null)
  fi

  if [ -z "$target_utc" ]; then
    echo 0
    return
  fi

  local diff=$((target_utc - now_utc))

  # Se il target e' gia' passato oggi, vuol dire domani
  if [ $diff -le 0 ]; then
    diff=$((diff + 86400))
  fi

  # Aggiungi 60 secondi di margine
  echo $((diff + 60))
}

# Formatta secondi in "Xh Ym"
format_duration() {
  local secs="$1"
  local hours=$((secs / 3600))
  local mins=$(( (secs % 3600) / 60 ))
  if [ $hours -gt 0 ]; then
    echo "${hours}h ${mins}m"
  else
    echo "${mins}m"
  fi
}

# Attende con countdown visuale, mostrando il tempo rimanente.
wait_with_countdown() {
  local total_secs="$1"
  local label="$2"
  local end_time=$(($(date +%s) + total_secs))

  while true; do
    local now
    now=$(date +%s)
    local remaining=$((end_time - now))
    if [ $remaining -le 0 ]; then
      break
    fi
    printf "\r  %s — riprovo tra %s ...  " "$label" "$(format_duration $remaining)"
    sleep 10
  done
  printf "\r  %s — riparto ora!                        \n" "$label"
}

# Gestisce il rate limit: log, mostra all'utente, chiede come proseguire.
# In YOLO mode aspetta automaticamente senza chiedere.
# Ritorna 0 se si deve riprovare, 1 se si deve uscire.
handle_rate_limit() {
  local label="$1"

  log "RATE LIMIT RILEVATO durante: $label"
  log "Messaggio: $RATE_LIMIT_MSG"

  # Prova a estrarre il tempo di reset dal messaggio (es. "resets 3pm (UTC)")
  local reset_time
  reset_time=$(echo "$RATE_LIMIT_MSG" | grep -oP 'resets?\s+\K[0-9]+[ap]m' | head -1)

  local wait_secs=0
  local reset_label=""
  if [ -n "$reset_time" ]; then
    wait_secs=$(calc_wait_seconds "$reset_time")
    reset_label="$reset_time UTC"
  fi

  echo ""
  echo "=============================================="
  echo "  RATE LIMIT - Claude ha esaurito la quota"
  echo "----------------------------------------------"
  echo "  Fase: $label"
  if [ -n "$reset_label" ]; then
    echo "  Reset previsto: $reset_label (tra $(format_duration $wait_secs))"
  fi
  echo "  Messaggio: $(echo "$RATE_LIMIT_MSG" | head -c 200)"
  echo "=============================================="
  echo ""

  # --- YOLO mode: aspetta automaticamente ---
  if [ "$YOLO_MODE" = "true" ]; then
    if [ $wait_secs -gt 0 ]; then
      log "YOLO: attesa automatica fino a $reset_label ($(format_duration $wait_secs))"
      wait_with_countdown $wait_secs "Rate limit — reset $reset_label"
    else
      log "YOLO: orario di reset non disponibile, attesa 10 minuti"
      wait_with_countdown 600 "Rate limit — attesa 10 minuti"
    fi
    return 0
  fi

  # --- Modalita' interattiva ---
  RATE_LIMIT_CHOICE=0
  if [ $wait_secs -gt 0 ]; then
    select_menu "Claude rate-limited. Cosa fare?" RATE_LIMIT_CHOICE \
      "Riparti alle $reset_label  — attendi $(format_duration $wait_secs) e riprova" \
      "Attendi             — scegli tu quanto aspettare" \
      "Esci                — ferma lo script"
  else
    select_menu "Claude rate-limited (orario reset non disponibile). Cosa fare?" RATE_LIMIT_CHOICE \
      "Attendi 10 minuti   — riprova tra 10 minuti" \
      "Attendi             — scegli tu quanto aspettare" \
      "Esci                — ferma lo script"
  fi

  case $RATE_LIMIT_CHOICE in
    0)
      if [ $wait_secs -gt 0 ]; then
        log "Utente sceglie: riparti alle $reset_label"
        wait_with_countdown $wait_secs "Attesa reset $reset_label"
      else
        log "Utente sceglie: attendi 10 minuti"
        wait_with_countdown 600 "Attesa 10 minuti"
      fi
      return 0
      ;;
    1)
      read -p "Quanti minuti attendere? " wait_min
      wait_min=${wait_min:-10}
      log "Utente sceglie: attendi $wait_min minuti"
      wait_with_countdown $((wait_min * 60)) "Attesa ${wait_min}m"
      return 0
      ;;
    2)
      log "Utente sceglie: uscita dopo rate limit"
      return 1
      ;;
  esac
}

# --- Estrai testo risultato da stream-json ---
extract_claude_result() {
  local jsonl_file="$1"
  local result_line
  result_line=$(grep '"type":"result"' "$jsonl_file" | tail -1)
  if [ -z "$result_line" ]; then
    echo ""
    return
  fi
  if command -v jq &>/dev/null; then
    echo "$result_line" | jq -r '.result // empty' 2>/dev/null
  else
    echo "$result_line" | sed 's/.*"result":"\([^"]*\)".*/\1/' 2>/dev/null
  fi
}

# --- Genera riepilogo testuale da stream-json ---
summarize_claude_phase() {
  local jsonl_file="$1"
  local label="$2"

  echo "=== [$label] $(date +%H:%M:%S) ===" >> "$CLAUDE_LOG_SUMMARY"

  if command -v jq &>/dev/null; then
    jq -r '
      if .type == "assistant" then
        .message.content[]? |
        if .type == "tool_use" then
          if .name == "Bash" then "  > Bash: " + (.input.command // "" | split("\n")[0] | .[0:120])
          elif .name == "Read" then "  > Read: " + (.input.file_path // "")
          elif .name == "Edit" then "  > Edit: " + (.input.file_path // "")
          elif .name == "Write" then "  > Write: " + (.input.file_path // "")
          elif .name == "Grep" then "  > Grep: " + (.input.pattern // "") + " in " + (.input.path // ".")
          elif .name == "Glob" then "  > Glob: " + (.input.pattern // "")
          else "  > " + .name + ": " + (.input | tostring | .[0:100])
          end
        elif .type == "text" and (.text | length) > 0 then
          "  Claude: " + (.text | split("\n")[0] | .[0:150])
        else empty
        end
      elif .type == "result" then
        "  --- Result: " + (.result // "N/A" | .[0:100]) +
        " | cost: $" + (.cost_usd // 0 | tostring) +
        " | " + ((.duration_ms // 0) / 1000 | floor | tostring) + "s"
      else empty
      end
    ' "$jsonl_file" >> "$CLAUDE_LOG_SUMMARY" 2>/dev/null
  else
    local tool_count
    tool_count=$(grep -c '"tool_use"' "$jsonl_file" 2>/dev/null || echo 0)
    local result_line
    result_line=$(grep '"type":"result"' "$jsonl_file" | tail -1)
    local result_text
    result_text=$(echo "$result_line" | sed 's/.*"result":"\([^"]*\)".*/\1/' 2>/dev/null)
    echo "  Tool calls: $tool_count" >> "$CLAUDE_LOG_SUMMARY"
    echo "  Result: $result_text" >> "$CLAUDE_LOG_SUMMARY"
    echo "  (installa jq per un riepilogo dettagliato)" >> "$CLAUDE_LOG_SUMMARY"
  fi

  echo "" >> "$CLAUDE_LOG_SUMMARY"
}

# --- Verifica indipendente: build + test ---
# Imposta VERIFY_ERRORS (vuoto = tutto ok)
run_verify() {
  VERIFY_ERRORS=""
  local CHANGED
  CHANGED=$(git diff --name-only HEAD 2>/dev/null; git ls-files --others --exclude-standard 2>/dev/null)

  local HAS_BACKEND=false
  local HAS_FRONTEND=false
  echo "$CHANGED" | grep -q "^backend/" && HAS_BACKEND=true
  echo "$CHANGED" | grep -q "^frontend/web/" && HAS_FRONTEND=true

  if [ "$HAS_BACKEND" = "false" ] && [ "$HAS_FRONTEND" = "false" ]; then
    HAS_BACKEND=true
  fi

  if [ "$HAS_BACKEND" = "true" ]; then
    log "Verifica backend: dotnet build..."
    local BUILD_OUT
    BUILD_OUT=$(cd backend && dotnet build Seed.slnx 2>&1)
    if [ $? -ne 0 ]; then
      VERIFY_ERRORS+="=== DOTNET BUILD FAILED ===
$(echo "$BUILD_OUT" | tail -30)

"
      log "FAIL: dotnet build"
    else
      log "OK: dotnet build"
      log "Verifica backend: dotnet test..."
      local TEST_OUT
      TEST_OUT=$(cd backend && dotnet test Seed.slnx --no-build 2>&1)
      if [ $? -ne 0 ]; then
        VERIFY_ERRORS+="=== DOTNET TEST FAILED ===
$(echo "$TEST_OUT" | tail -40)

"
        log "FAIL: dotnet test"
      else
        log "OK: dotnet test"
      fi
    fi
  fi

  if [ "$HAS_FRONTEND" = "true" ]; then
    log "Verifica frontend: ng build..."
    local FE_BUILD_OUT
    FE_BUILD_OUT=$(cd frontend/web && npm run build 2>&1)
    if [ $? -ne 0 ]; then
      VERIFY_ERRORS+="=== FRONTEND BUILD FAILED ===
$(echo "$FE_BUILD_OUT" | tail -30)

"
      log "FAIL: frontend build"
    else
      log "OK: frontend build"
    fi
  fi
}

# --- Post-verifica: update stato + commit ---
run_post_success() {
  local task_i="$1"
  local task_file="$2"

  OUTPUT=$(build_exec_cmd "Sei in autonomous execution mode - FASE UPDATE.
Il task e' stato verificato con successo (build e test passano).
Aggiorna lo stato del task corrispondente a $TASKS_DIR/$task_file da 'pending' a 'done' nel piano $PLAN.
Rispondi SOLO: UPDATED" "Task $task_i - Update")
  echo "$OUTPUT" >> "$LOG_FILE"

  # Rate limit durante update → ferma lo script
  if echo "$OUTPUT" | grep -q "RATE_LIMITED"; then
    log "Esecuzione interrotta per rate limit durante update stato task."
    exit 1
  fi

  if git diff --quiet && git diff --cached --quiet && [ -z "$(git ls-files --others --exclude-standard)" ]; then
    log "Nessuna modifica da committare"
  else
    log "=== Task $task_i - Commit ==="
    git add -A

    local TASK_TITLE
    TASK_TITLE=$(grep -m1 '^#' "$TASKS_DIR/$task_file" 2>/dev/null | sed 's/^#\+\s*//')
    local CHANGED_FILES
    CHANGED_FILES=$(git diff --cached --name-only | head -20)

    local commit_temp=$(mktemp)
    claude -p "Genera SOLO un commit message in formato Conventional Commits per queste modifiche.
Contesto task: $TASK_TITLE
File modificati:
$CHANGED_FILES

Regole:
- Formato: <type>(<scope>): <descrizione>
- Types: feat, fix, docs, refactor, test, chore
- Scopes: api, app, auth, infra, ui, core, docker
- Max 72 caratteri, lowercase, no punto finale, imperative mood
- Rispondi SOLO con il commit message, niente altro" \
      --verbose --model claude-haiku-4-5-20251001 --effort low --output-format stream-json > "$commit_temp" 2>> "$LOG_FILE"
    cat "$commit_temp" >> "$CLAUDE_LOG_JSONL"
    summarize_claude_phase "$commit_temp" "Task $task_i - Commit msg"
    COMMIT_MSG=$(extract_claude_result "$commit_temp")

    # Rate limit check sul commit message (il bug originale!)
    if check_rate_limit "$commit_temp" "$COMMIT_MSG"; then
      log "RATE LIMIT rilevato durante generazione commit message. Uso fallback."
      rm -f "$commit_temp"
      COMMIT_MSG=""  # forza il fallback
    else
      rm -f "$commit_temp"
    fi

    if [ -z "$COMMIT_MSG" ] || [ ${#COMMIT_MSG} -gt 100 ]; then
      COMMIT_MSG="feat: complete task $task_i - $(echo "$TASK_TITLE" | head -c 50)"
    fi

    if git commit -m "$COMMIT_MSG"; then
      log "Commit: $COMMIT_MSG"
    else
      log "ERRORE: git commit fallito (exit code $?). Possibili cause: permessi .git/ (chown), git config user.name/email mancante."
      git reset HEAD -- . >/dev/null 2>&1
      log "Staging area ripristinata (git reset)."
    fi
  fi
}

# --- Costruzione opzioni Claude ---
# In YOLO mode: --dangerously-skip-permissions (nessuna restrizione)
# In modalita' normale: whitelist di tool specifici
# Output: stream-json -> JSONL (dettaglio) + summary (leggibile)
# Ritorna solo il testo risultato per la logica dello script
build_plan_cmd() {
  local prompt="$1"
  local label="${2:-plan}"

  while true; do
    local temp=$(mktemp)

    if [ "$YOLO_MODE" = "true" ]; then
      claude -p "$prompt" $CLAUDE_FLAGS --output-format stream-json --dangerously-skip-permissions > "$temp" 2>> "$LOG_FILE"
    else
      claude -p "$prompt" $CLAUDE_FLAGS --output-format stream-json \
        --allowedTools "Read" "Write" "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" "Bash(mkdir*)" > "$temp" 2>> "$LOG_FILE"
    fi

    cat "$temp" >> "$CLAUDE_LOG_JSONL"
    summarize_claude_phase "$temp" "$label"
    local result
    result=$(extract_claude_result "$temp")

    if check_rate_limit "$temp" "$result"; then
      rm -f "$temp"
      if handle_rate_limit "$label"; then
        continue  # riprova
      else
        echo "RATE_LIMITED"
        return
      fi
    fi

    rm -f "$temp"
    echo "$result"
    return
  done
}

build_exec_cmd() {
  local prompt="$1"
  local label="${2:-exec}"

  while true; do
    local temp=$(mktemp)

    if [ "$YOLO_MODE" = "true" ]; then
      claude -p "$prompt" $CLAUDE_FLAGS --output-format stream-json --dangerously-skip-permissions > "$temp" 2>> "$LOG_FILE"
    else
      claude -p "$prompt" $CLAUDE_FLAGS --output-format stream-json \
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
          "Bash(docker*)" > "$temp" 2>> "$LOG_FILE"
    fi

    cat "$temp" >> "$CLAUDE_LOG_JSONL"
    summarize_claude_phase "$temp" "$label"
    local result
    result=$(extract_claude_result "$temp")

    if check_rate_limit "$temp" "$result"; then
      rm -f "$temp"
      if handle_rate_limit "$label"; then
        continue  # riprova
      else
        echo "RATE_LIMITED"
        return
      fi
    fi

    rm -f "$temp"
    echo "$result"
    return
  done
}

# --- Riepilogo configurazione ---
echo ""
echo "========================================="
echo "  Piano:     $(basename "$PLAN")"
echo "  Modalita': $MODE"
echo "  Branch:    $(git branch --show-current)"
echo "  Modello:   $CLAUDE_MODEL ($CLAUDE_MODEL_ID)"
echo "  Effort:    $CLAUDE_EFFORT"
echo "-----------------------------------------"
echo "  Log:       $LOG_FILE"
echo "  Claude:    $CLAUDE_LOG_JSONL"
echo "  Summary:   $CLAUDE_LOG_SUMMARY"
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
5. Crea un file ESCLUSIVAMENTE nella directory $TASKS_DIR/ (path assoluto: $(cd "$TASKS_DIR" 2>/dev/null && pwd || echo "$TASKS_DIR")) con nome task-{numero}-{slug}.md contenente:
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
6. Rispondi SOLO con: PLANNED:{filename} o ALL_COMPLETE" "Task $i - Plan")

  echo "$OUTPUT" >> "$LOG_FILE"
  log "Output plan: $(echo "$OUTPUT" | tail -1)"

  # Rate limit durante il planning → ferma lo script
  if echo "$OUTPUT" | grep -q "RATE_LIMITED"; then
    log "Esecuzione interrotta per rate limit durante planning."
    exit 1
  fi

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

  RETRY=0
  TASK_PASSED=false

  while [ $RETRY -le $MAX_RETRIES ]; do
    if [ $RETRY -eq 0 ]; then
      # --- Prima esecuzione ---
      OUTPUT=$(build_exec_cmd "Sei in autonomous execution mode - FASE EXECUTE.
1. Leggi il mini-plan in $TASKS_DIR/$TASK_FILE
2. Leggi il piano principale in $PLAN per contesto generale
3. Esegui esattamente quello descritto nel mini-plan
4. NON aggiornare lo stato del task nel piano (lo fara' lo script dopo la verifica)
5. Aggiungi in fondo al mini-plan una sezione:
   ## Risultato
   - File modificati/creati
   - Scelte implementative e motivazioni
   - Eventuali deviazioni dal piano e perche'
6. Rispondi SOLO: DONE" "Task $i - Exec")
    else
      # --- Retry: fix degli errori ---
      log "=== Task $i - Fix attempt $RETRY/$MAX_RETRIES ==="
      OUTPUT=$(build_exec_cmd "Sei in autonomous execution mode - FASE FIX.
La verifica automatica (build/test) e' FALLITA dopo l'esecuzione del task.

Mini-plan: $TASKS_DIR/$TASK_FILE
Piano principale: $PLAN

ERRORI DA CORREGGERE:
$VERIFY_ERRORS

Istruzioni:
1. Analizza gli errori sopra
2. Correggi il codice per far passare build e test
3. NON aggiornare lo stato del task nel piano
4. Rispondi SOLO: DONE" "Task $i - Fix $RETRY/$MAX_RETRIES")
    fi

    echo "$OUTPUT" >> "$LOG_FILE"
    log "Risultato esecuzione: $(echo "$OUTPUT" | tail -1)"

    # Rate limit durante esecuzione → ferma lo script
    if echo "$OUTPUT" | grep -q "RATE_LIMITED"; then
      log "Esecuzione interrotta per rate limit durante exec/fix."
      exit 1
    fi

    # --- Verifica indipendente: build + test ---
    log "=== Task $i - Verifica (attempt $((RETRY + 1))/$((MAX_RETRIES + 1))) ==="
    run_verify

    # --- Esito verifica ---
    if [ -z "$VERIFY_ERRORS" ]; then
      log "Verifica PASSATA"
      TASK_PASSED=true
      break
    else
      log "Verifica FALLITA"
      echo "$VERIFY_ERRORS" >> "$LOG_FILE"
      ((RETRY++))
    fi
  done

  # --- Post-verifica ---
  if [ "$TASK_PASSED" = "true" ]; then
    run_post_success "$i" "$TASK_FILE"

    # --- Review gate: conferma prima del prossimo task ---
    if [ "$AUTO_APPROVE" != "true" ]; then
      echo ""
      echo "========================================="
      echo "  Task $i completato e committato."
      echo "  Commit: $COMMIT_MSG"
      echo "========================================="
      read -p "Continuare con il prossimo task? (y/q) " confirm
      case "$confirm" in
        q) log "Interrotto dall'utente dopo task $i."; exit 0 ;;
        *) log "Utente conferma: prosegui" ;;
      esac
    fi
  else
    log "ERRORE: Task $i FALLITO dopo $MAX_RETRIES tentativi di fix."
    log "Ultimi errori:"
    echo "$VERIFY_ERRORS" >> "$LOG_FILE"

    echo ""
    echo "========================================="
    echo "  Task $i FALLITO dopo $MAX_RETRIES tentativi"
    echo "-----------------------------------------"
    echo "$VERIFY_ERRORS" | tail -20
    echo "========================================="

    RECOVERY_CHOICE=0
    select_menu "Cosa vuoi fare?" RECOVERY_CHOICE \
      "Riprova   — resetta i tentativi e riprova il fix" \
      "Manuale   — correggi tu, poi riprova la verifica" \
      "Salta     — segna come failed, passa al prossimo task" \
      "Esci      — ferma l'esecuzione"

    case $RECOVERY_CHOICE in
      0)
        log "Utente sceglie: riprova task $i"
        ((i--))
        continue
        ;;
      1)
        log "Utente sceglie: fix manuale per task $i"
        read -p "Correggi il codice, poi premi Enter per rieseguire la verifica... "
        run_verify
        if [ -z "$VERIFY_ERRORS" ]; then
          log "Verifica post-fix manuale PASSATA"
          run_post_success "$i" "$TASK_FILE"
        else
          log "Verifica post-fix manuale FALLITA"
          echo "$VERIFY_ERRORS" | tail -20
          ((i--))
          continue
        fi
        ;;
      2)
        log "Task $i skippato dall'utente dopo fallimento"
        continue
        ;;
      3)
        log "Interrotto dall'utente dopo fallimento task $i"
        break
        ;;
    esac
  fi

  sleep 3
done

log "=== SUMMARY ==="
log "Log completo:    $LOG_FILE"
log "Claude JSONL:    $CLAUDE_LOG_JSONL"
log "Claude summary:  $CLAUDE_LOG_SUMMARY"
log "Mini-plan in $TASKS_DIR/:"
ls -la "$TASKS_DIR/" | tee -a "$LOG_FILE"
