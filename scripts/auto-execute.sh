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

# --- Runtime state ---
CLAUDE_MODEL=""
CLAUDE_EFFORT=""
PLAN=""
MODE=""

YOLO_MODE=false
AUTO_APPROVE=false
CLAUDE_MODEL_ID=""
CURRENT_BRANCH=""

CLAUDE_FLAGS=()
PLAN_FLAGS=()
EXEC_FLAGS=()
FIX_FLAGS=()
TTY_STATE_SNAPSHOT=""
TERMINAL_RESTORED=false

# ==============================
# FUNCTIONS
# ==============================

restore_terminal_state() {
  if [ "$TERMINAL_RESTORED" = "true" ]; then
    return
  fi
  TERMINAL_RESTORED=true

  printf "\033[?25h" > /dev/tty 2>/dev/null || true

  if [ -n "$TTY_STATE_SNAPSHOT" ]; then
    stty "$TTY_STATE_SNAPSHOT" < /dev/tty 2>/dev/null || stty sane 2>/dev/null || true
  else
    stty sane < /dev/tty 2>/dev/null || stty sane 2>/dev/null || true
  fi
}

handle_signal_exit() {
  local code="$1"
  restore_terminal_state
  exit "$code"
}

initialize_terminal_handling() {
  TERMINAL_RESTORED=false
  TTY_STATE_SNAPSHOT=$(stty -g < /dev/tty 2>/dev/null || true)

  trap 'restore_terminal_state' EXIT
  trap 'handle_signal_exit 130' INT
  trap 'handle_signal_exit 143' TERM
  trap 'handle_signal_exit 129' HUP
}

flush_tty_input() {
  while IFS= read -rsn1 -t 0.01 _discard < /dev/tty; do
    :
  done
}

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

  # Output su /dev/tty — select_menu puo' essere chiamato dentro $()
  # dove stdout e' catturato, rendendo il menu invisibile

  # Evita che input residuo (es. da Ctrl+C / Enter) faccia saltare il menu.
  flush_tty_input

  # Nascondi cursore
  printf "\033[?25l" > /dev/tty

  # Header
  {
    echo ""
    echo "========================================="
    echo "  $title"
    echo "========================================="
    echo ""
  } > /dev/tty

  # Disegna opzioni
  for i in "${!display_opts[@]}"; do
    if [ $i -eq $selected ]; then
      printf "  \033[7m > %s \033[0m\n" "${display_opts[$i]}" > /dev/tty
    else
      printf "    %s\n" "${display_opts[$i]}" > /dev/tty
    fi
  done

  # Loop input (frecce, j/k, numeri)
  while true; do
    if ! IFS= read -rsn1 key < /dev/tty; then
      break
    fi
    local moved=false

    case "$key" in
      $'\x1b')
        # Sequenza escape: leggi resto della sequenza
        local ch1=""
        local ch2=""
        IFS= read -rsn1 -t 0.2 ch1 < /dev/tty 2>/dev/null
        IFS= read -rsn1 -t 0.2 ch2 < /dev/tty 2>/dev/null
        case "$ch1$ch2" in
          '[A'|'OA') ((selected > 0)) && ((selected--)); moved=true ;;
          '[B'|'OB') ((selected < count - 1)) && ((selected++)); moved=true ;;
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
      printf "\033[%dA" "$count" > /dev/tty
      for i in "${!display_opts[@]}"; do
        printf "\033[2K" > /dev/tty  # Cancella riga
        if [ $i -eq $selected ]; then
          printf "  \033[7m > %s \033[0m\n" "${display_opts[$i]}" > /dev/tty
        else
          printf "    %s\n" "${display_opts[$i]}" > /dev/tty
        fi
      done
    fi
  done

  # Mostra cursore
  printf "\033[?25h" > /dev/tty
  echo "" > /dev/tty

  _result=$selected
}

configure_execution_context() {
  # --- Parsing argomenti ---
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
    local plan_files=()
    mapfile -t plan_files < <(find "$PLANS_DIR" -maxdepth 1 -name "*.md" -type f 2>/dev/null | sort)

    if [ ${#plan_files[@]} -eq 0 ]; then
      echo "Nessun piano trovato in $PLANS_DIR/"
      echo "Crea un piano .md e riprova."
      exit 1
    fi

    local plan_labels=()
    for i in "${!plan_files[@]}"; do
      local filename
      local title
      filename=$(basename "${plan_files[$i]}")
      title=$(grep -m1 '^#' "${plan_files[$i]}" 2>/dev/null | sed 's/^#\+\s*//')
      if [ -n "$title" ]; then
        plan_labels+=("$filename - $title")
      else
        plan_labels+=("$filename")
      fi
    done

    local plan_choice=0
    select_menu "Seleziona il piano di esecuzione" plan_choice "${plan_labels[@]}"
    PLAN="${plan_files[$plan_choice]}"
  fi

  if [ ! -f "$PLAN" ]; then
    echo "ERRORE: piano non trovato: $PLAN"
    exit 1
  fi

  # --- Menu selezione modalita' ---
  if [ -z "$MODE" ]; then
    local mode_choice=0
    select_menu "Seleziona la modalita' di esecuzione" mode_choice \
      "Full autonomia  - pianifica e esegue senza intervento" \
      "Review          - approvi ogni mini-plan prima dell'esecuzione" \
      "YOLO            - zero restrizioni (solo sandbox Docker)" \
      "YOLO + Review   - zero restrizioni + approvi ogni plan"

    case $mode_choice in
      0) MODE="full" ;;
      1) MODE="review" ;;
      2) MODE="yolo" ;;
      3) MODE="yolo-review" ;;
    esac
  fi

  # --- Menu selezione modello ---
  if [ -z "$CLAUDE_MODEL" ]; then
    local model_choice=0
    select_menu "Seleziona il modello Claude" model_choice \
      "Opus   - claude-opus-4-6 (piu' capace, piu' lento)" \
      "Sonnet - claude-sonnet-4-6 (bilanciato)"

    case $model_choice in
      0) CLAUDE_MODEL="opus" ;;
      1) CLAUDE_MODEL="sonnet" ;;
    esac
  fi

  # --- Menu selezione effort ---
  if [ -z "$CLAUDE_EFFORT" ]; then
    local effort_choice=0
    select_menu "Seleziona il livello di effort" effort_choice --default 1 \
      "Low    - risposte rapide, meno ragionamento" \
      "Medium - bilanciato" \
      "High   - ragionamento approfondito, piu' lento" \
      "Max    - ragionamento massimo, il piu' lento"

    case $effort_choice in
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
    *)
      echo "ERRORE: modello sconosciuto: $CLAUDE_MODEL"
      echo "Valori ammessi: opus, sonnet"
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

  CLAUDE_FLAGS=(--verbose --model "$CLAUDE_MODEL_ID" --effort "$CLAUDE_EFFORT")
  # Plan: stesso modello/effort scelti dall'utente. Il mini-plan deve essere
  # self-contained (l'exec NON rilegge il main plan), quindi la qualita' di
  # estrazione e' il vero punto critico del flusso. Se l'utente ha scelto opus
  # e' perche' vuole quella qualita'; applicarla al plan e' dove conta di piu'.
  # Exec: sonnet+high - implementazione guidata dal mini-plan, sonnet-high offre
  # il miglior rapporto qualita'/costo per eseguire task gia' specificati.
  # Fix: modello/effort scelti dall'utente - fase remediale dove la qualita' di
  # reasoning richiesta dipende dalla complessita' del progetto, quindi rispetta
  # la scelta dell'utente.
  PLAN_FLAGS=("${CLAUDE_FLAGS[@]}")
  EXEC_FLAGS=(--verbose --model "claude-sonnet-4-6" --effort high)
  FIX_FLAGS=("${CLAUDE_FLAGS[@]}")

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

  mkdir -p "$TASKS_DIR" "$LOG_DIR" "$CLAUDE_LOG_DIR"
}
# --- Logging ---
log() {
  echo "[$(date +%H:%M:%S)] $1" | tee -a "$LOG_FILE"
}

# --- Prompt templates ---
# Funzioni dedicate per i prompt lunghi, cosi' il flow principale resta leggibile.
build_update_prompt() {
  local task_file="$1"
  cat <<EOF
Task verificato con successo (build + test ok).
Aggiorna il task in $PLAN leggendo anche il mini-plan $TASKS_DIR/$task_file (sezione '## Risultato').

Modifiche da fare al task nel piano:
- cambia stato '[ ] Not Started' -> '[x] Done' (se presente)
- spunta tutte le checkbox della Definition of Done: '- [ ]' -> '- [x]'.
  Se un bullet non corrisponde a cosa e' stato realmente fatto, aggiorna il testo.
- aggiungi dopo la Definition of Done una sezione '**Implementation Notes:**'
  con 3-5 bullet dal Risultato del mini-plan
- aggiorna la tabella Story Coverage se lo stato delle storie coperte e' cambiato

NON usare git add/commit/push. Quando hai finito rispondi: UPDATED
EOF
}

build_commit_msg_prompt() {
  local task_title="$1"
  local changed_files="$2"
  cat <<EOF
Genera SOLO un commit message in formato Conventional Commits per queste modifiche.
Contesto task: $task_title
File modificati:
$changed_files

Regole:
- Formato: <type>(<scope>): <descrizione>
- Types: feat, fix, docs, refactor, test, chore
- Scopes: api, app, auth, infra, ui, core, docker
- Max 72 caratteri, lowercase, no punto finale, imperative mood
- Rispondi SOLO con il commit message, niente altro
EOF
}

build_plan_prompt() {
  cat <<EOF
Trova il primo task pending in $PLAN.
Se non ce ne sono, rispondi SOLO: ALL_COMPLETE

IMPORTANTE: il mini-plan che produci deve essere COMPLETAMENTE self-contained.
L'exec NON rileggera' $PLAN ne' altri file del piano. Se ometti una convenzione,
una decisione gia' presa, o una dipendenza, l'exec la violera' silenziosamente.

Procedi in questo ordine:

1. Individua il task pending dal file $PLAN.

2. Identifica il contesto ereditato:
   a. I task elencati in 'Depends on:' del task corrente.
   b. I task con stato Done nella stessa Phase del task corrente.
   c. I task Done (ovunque nel piano) che toccano gli stessi file, namespace,
      entity o servizi del task corrente.
   d. ADR, requirement (FEAT-X.md) o altri doc citati nel task.

3. Per ciascun task identificato al punto 2, estrai la sezione
   '**Implementation Notes:**' VERBATIM (non riassumere) e le righe della
   'Definition of Done' che stabiliscono convenzioni riutilizzabili.

4. Estrai dalla tabella 'Story Coverage' di $PLAN la/le righe relative alle
   storie del task corrente.

5. Esplora il codice rilevante con Read/Grep/Glob per verificare lo stato attuale.

6. Crea il mini-plan in $TASKS_DIR/task-{numero}-{slug}.md con questa struttura
   ESATTA (tutte le sezioni obbligatorie):

   # Task {numero}: {titolo}

   ## Contesto ereditato dal piano
   ### Storie coperte
   {righe della Story Coverage estratte al punto 4}
   ### Dipendenze (da 'Depends on:')
   {per ogni dipendenza: 'T-XX: <titolo> - <Implementation Notes verbatim>'}
   ### Convenzioni da task Done correlati
   {Implementation Notes verbatim dei task identificati al punto 2c, raggruppate
    per task di origine. Max 8 bullet totali, scegli le piu' rilevanti.}
   ### Riferimenti
   {ADR, requirement, doc citati - path + sezione rilevante}

   ## Stato attuale del codice
   - File esistenti rilevanti (path esatti) e loro ruolo
   - Pattern gia' in uso che il task deve seguire

   ## Piano di esecuzione
   - File da creare/modificare (path esatti)
   - Approccio tecnico step-by-step
   - Test da scrivere/verificare (path + nomi metodi)

   ## Criteri di completamento
   - Cosa deve funzionare per considerare il task done (Definition of Done
     del task corrente, copiata verbatim da $PLAN)

Regole:
- NON riassumere le Implementation Notes: copiale verbatim. Se sono troppe,
  scegli le piu' rilevanti ma non parafrasare.
- Se una sezione del contesto ereditato e' vuota (es. nessuna dipendenza),
  scrivi esplicitamente 'Nessuna' - non omettere la sezione.
- Il mini-plan puo' essere lungo: meglio ridondante che incompleto.

Rispondi SOLO: PLANNED:{filename}
EOF
}

build_exec_prompt() {
  local task_file="$1"
  cat <<EOF
Esegui il mini-plan in $TASKS_DIR/$task_file.

Disciplina test/build: puoi eseguire build/test puntuali per validare
un'assunzione specifica, ma NON entrare in loop iterativo - la verifica
finale (build + test) la fa lo script dopo di te. Se il codice ti sembra
corretto, fermati e rispondi DONE: se qualcosa non va lo script ti
ripassera' gli errori in un retry con contesto pulito.

NON fare git add/commit/push (ci pensa lo script).

Al termine aggiungi in fondo al mini-plan una sezione '## Risultato' con
file modificati, scelte chiave ed eventuali deviazioni dal mini-plan. Rispondi SOLO: DONE
EOF
}

build_fix_prompt() {
  local task_file="$1"
  local verify_errors="$2"
  cat <<EOF
La verifica automatica e' FALLITA.
Mini-plan: $TASKS_DIR/$task_file

ERRORI (output reale dalla verifica - sono la fonte di verita', non
rilanciare build/test prima di aver capito e corretto il problema):

$verify_errors

Correggi il codice. NON fare git add/commit/push. Rispondi SOLO: DONE
EOF
}

# --- Rate limit detection ---
# Pattern esatti di errore dalla Claude API / Claude Code CLI.
# IMPORTANTE: il JSONL contiene tutto l'output di Claude, incluso il contenuto
# dei file letti — pattern generici ("capacity", "rate limit") causano falsi positivi.
# Usiamo solo i pattern specifici documentati dall'API Anthropic:
#   - Quota utente:     "You've hit your limit · resets ..."
#   - API rate limit:   {"type":"error","error":{"type":"rate_limit_error",...}}
#   - Server overload:  {"type":"error","error":{"type":"overloaded_error",...}}
RATE_LIMIT_RESULT_REGEX="You've hit your limit"
RATE_LIMIT_JSONL_REGEX='"type":\s*"(rate_limit_error|overloaded_error)"'

# Controlla se l'output di Claude indica rate limiting.
# Due check separati con regex diversi:
#   1. result text: messaggio user-facing ("You've hit your limit")
#   2. JSONL grezzo: errori API strutturati (rate_limit_error, overloaded_error)
# Ritorna 0 se rate-limited, 1 se ok.
# Se rate-limited, imposta RATE_LIMIT_MSG con il messaggio rilevato.
check_rate_limit() {
  local jsonl_file="$1"
  local result_text="$2"
  RATE_LIMIT_MSG=""

  # Controlla nel result text (messaggio user-facing)
  if echo "$result_text" | grep -qF "$RATE_LIMIT_RESULT_REGEX"; then
    RATE_LIMIT_MSG="$result_text"
    return 0
  fi

  # Controlla nel JSONL grezzo per errori API strutturati
  local match
  match=$(grep -E "$RATE_LIMIT_JSONL_REGEX" "$jsonl_file" 2>/dev/null | head -1)
  if [ -n "$match" ]; then
    RATE_LIMIT_MSG=$(echo "$match" | head -c 200)
    return 0
  fi

  return 1
}

# Controlla se l'output di Claude indica max-turns raggiunto.
# Il JSONL result line ha subtype "max_turns" quando claude esaurisce i turni.
# Ritorna 0 se max-turns, 1 se ok.
check_max_turns() {
  local jsonl_file="$1"
  grep '"type":"result"' "$jsonl_file" | tail -1 | grep -q '"subtype":"max_turns"'
}

# Calcola i secondi di attesa fino a un orario UTC (es. "3pm", "11am").
# Ritorna i secondi via echo. Se non riesce a parsare, ritorna 0.
calc_wait_seconds() {
  local reset_time="$1"  # es. "3pm", "11am"
  local hour ampm

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
    printf "\r  %s — riprovo tra %s ...  " "$label" "$(format_duration $remaining)" > /dev/tty
    sleep 10
  done
  printf "\r  %s — riparto ora!                        \n" "$label" > /dev/tty
}

# Gestisce il rate limit: log, mostra all'utente, chiede come proseguire.
# In YOLO mode aspetta automaticamente senza chiedere.
# Ritorna 0 se si deve riprovare, 1 se si deve uscire.
handle_rate_limit() {
  local label="$1"

  log "RATE LIMIT RILEVATO durante: $label"
  log "Messaggio: $RATE_LIMIT_MSG"

  # Prova a estrarre il tempo di reset dal messaggio
  # Formato reale: "resets 3pm (UTC)" oppure "resets 7pm (America/Chicago)"
  local reset_time reset_tz
  reset_time=$(echo "$RATE_LIMIT_MSG" | grep -oP 'resets?\s+\K[0-9]+:?[0-9]*[ap]m' | head -1)
  reset_tz=$(echo "$RATE_LIMIT_MSG" | grep -oP 'resets?\s+[0-9]+:?[0-9]*[ap]m\s+\(\K[^)]+' | head -1)
  reset_tz="${reset_tz:-UTC}"

  local wait_secs=0
  local reset_label=""
  if [ -n "$reset_time" ]; then
    wait_secs=$(calc_wait_seconds "$reset_time")
    reset_label="$reset_time ($reset_tz)"
  fi

  # Output su /dev/tty perche' questa funzione viene chiamata dentro $()
  # e stdout e' catturato — senza /dev/tty il banner e il menu sono invisibili
  {
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
  } > /dev/tty

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
      read -p "Quanti minuti attendere? " wait_min < /dev/tty
      if ! [[ "$wait_min" =~ ^[0-9]+$ ]] || [ "$wait_min" -eq 0 ]; then
        wait_min=10
      fi
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
    # Fallback senza jq: estrai il valore di "result" gestendo escaped quotes
    echo "$result_line" | grep -oP '"result"\s*:\s*"\K(\\.|[^"\\])*' 2>/dev/null | head -1 | sed 's/\\"/"/g; s/\\\\/\\/g'
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
    result_text=$(echo "$result_line" | grep -oP '"result"\s*:\s*"\K(\\.|[^"\\])*' 2>/dev/null | head -1)
    echo "  Tool calls: $tool_count" >> "$CLAUDE_LOG_SUMMARY"
    echo "  Result: $result_text" >> "$CLAUDE_LOG_SUMMARY"
    echo "  (installa jq per un riepilogo dettagliato)" >> "$CLAUDE_LOG_SUMMARY"
  fi

  echo "" >> "$CLAUDE_LOG_SUMMARY"
}

# --- Estrazione smart di output lunghi: head (contesto iniziale) + tail (errori finali) ---
# Per output di build/test, il contesto iniziale (comando lanciato, primi
# errori) e quello finale (summary + ultimi errori) sono entrambi utili.
# Un tail puro perde i primi errori quando l'output e' molto lungo.
# Se l'output e' abbastanza corto, lo ritorna integralmente.
# Uso: smart_truncate <content> <head_lines> <tail_lines>
smart_truncate() {
  local content="$1"
  local head_n="$2"
  local tail_n="$3"
  local total
  total=$(printf '%s\n' "$content" | wc -l)

  if [ "$total" -le $((head_n + tail_n + 2)) ]; then
    printf '%s\n' "$content"
  else
    printf '%s\n' "$content" | head -"$head_n"
    printf '\n... [%d righe omesse dal centro] ...\n\n' $((total - head_n - tail_n))
    printf '%s\n' "$content" | tail -"$tail_n"
  fi
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
$(smart_truncate "$BUILD_OUT" 10 50)

"
      log "FAIL: dotnet build"
    else
      log "OK: dotnet build"
      log "Verifica backend: dotnet test..."
      local TEST_OUT
      TEST_OUT=$(cd backend && dotnet test Seed.slnx --no-build 2>&1)
      if [ $? -ne 0 ]; then
        VERIFY_ERRORS+="=== DOTNET TEST FAILED ===
$(smart_truncate "$TEST_OUT" 20 130)

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
$(smart_truncate "$FE_BUILD_OUT" 10 50)

"
      log "FAIL: frontend build"
    else
      log "OK: frontend build"
      log "Verifica frontend: ng test (vitest run)..."
      local FE_TEST_OUT
      FE_TEST_OUT=$(cd frontend/web && npm test -- --watch=false 2>&1)
      if [ $? -ne 0 ]; then
        VERIFY_ERRORS+="=== FRONTEND TEST FAILED ===
$(smart_truncate "$FE_TEST_OUT" 20 130)

"
        log "FAIL: frontend test"
      else
        log "OK: frontend test"
      fi
    fi
  fi
}

# --- Post-verifica: update stato + commit ---
run_post_success() {
  local task_i="$1"
  local task_file="$2"

  log "=== Task $task_i - Update piano (sonnet) ==="

  # Update piano usa sonnet: la parte meccanica (flip checkbox) la farebbe
  # anche haiku, ma la sintesi delle Implementation Notes dal Risultato
  # beneficia di un modello piu' capace. Il delta di costo e' trascurabile
  # (~$0.01-0.04/task in piu') rispetto alla qualita' guadagnata. Opus qui
  # resta comunque spreco puro.
  local update_temp
  update_temp=$(mktemp)
  local update_prompt
  update_prompt=$(build_update_prompt "$task_file")

  if [ "$YOLO_MODE" = "true" ]; then
    claude -p "$update_prompt" --verbose --model claude-sonnet-4-6 --effort low \
      --output-format stream-json --max-turns 15 \
      --dangerously-skip-permissions \
      --disallowedTools "Task" "Agent" "WebSearch" "WebFetch" "TodoWrite" > "$update_temp" 2>> "$LOG_FILE"
  else
    claude -p "$update_prompt" --verbose --model claude-sonnet-4-6 --effort low \
      --output-format stream-json --max-turns 15 \
      --allowedTools "Read" "Write" "Edit" > "$update_temp" 2>> "$LOG_FILE"
  fi

  cat "$update_temp" >> "$CLAUDE_LOG_JSONL"
  summarize_claude_phase "$update_temp" "Task $task_i - Update"
  local UPDATE_RESULT
  UPDATE_RESULT=$(extract_claude_result "$update_temp")

  if check_rate_limit "$update_temp" "$UPDATE_RESULT"; then
    log "RATE LIMIT durante update piano. Uscita."
    rm -f "$update_temp"
    exit 1
  fi
  rm -f "$update_temp"

  if echo "$UPDATE_RESULT" | grep -qiw "UPDATED"; then
    log "Piano aggiornato"
  else
    log "ATTENZIONE: aggiornamento piano potrebbe non essere riuscito (output: $(echo "$UPDATE_RESULT" | head -c 100))"
  fi

  COMMIT_MSG=""

  if git diff --quiet && git diff --cached --quiet && [ -z "$(git ls-files --others --exclude-standard)" ]; then
    log "Nessuna modifica da committare"
  else
    log "=== Task $task_i - Commit ==="

    # Annulla eventuali commit fatti da Claude durante l'esecuzione
    # confrontando con il punto di partenza del task
    git add -A

    local TASK_TITLE
    TASK_TITLE=$(grep -m1 '^#' "$TASKS_DIR/$task_file" 2>/dev/null | sed 's/^#\+\s*//')
    local CHANGED_FILES
    CHANGED_FILES=$(git diff --cached --name-only | head -20)

    if [ -z "$CHANGED_FILES" ]; then
      log "Nessun file staged dopo git add -A, skip commit"
    else
      local commit_temp
      commit_temp=$(mktemp)
      claude -p "$(build_commit_msg_prompt "$TASK_TITLE" "$CHANGED_FILES")" \
        --verbose --model claude-haiku-4-5-20251001 --effort low --output-format stream-json > "$commit_temp" 2>> "$LOG_FILE"
      cat "$commit_temp" >> "$CLAUDE_LOG_JSONL"
      summarize_claude_phase "$commit_temp" "Task $task_i - Commit msg"
      COMMIT_MSG=$(extract_claude_result "$commit_temp")

      # Rate limit check sul commit message
      if check_rate_limit "$commit_temp" "$COMMIT_MSG"; then
        log "RATE LIMIT rilevato durante generazione commit message. Uso fallback."
        rm -f "$commit_temp"
        COMMIT_MSG=""  # forza il fallback
      else
        rm -f "$commit_temp"
      fi

      # Pulisci il commit message: rimuovi backtick, newline, spazi extra
      COMMIT_MSG=$(echo "$COMMIT_MSG" | tr -d '`' | tr '\n' ' ' | sed 's/^[[:space:]]*//;s/[[:space:]]*$//')

      if [ -z "$COMMIT_MSG" ] || [ ${#COMMIT_MSG} -gt 100 ]; then
        COMMIT_MSG="feat: complete task $task_i - $(echo "$TASK_TITLE" | head -c 50)"
        log "Commit message generato vuoto o troppo lungo, uso fallback: $COMMIT_MSG"
      fi

      if git commit -m "$COMMIT_MSG"; then
        log "Commit: $COMMIT_MSG"
      else
        log "ERRORE: git commit fallito (exit code $?). Possibili cause: permessi .git/ (chown), git config user.name/email mancante."
        git reset HEAD -- . >/dev/null 2>&1
        log "Staging area ripristinata (git reset)."
      fi
    fi
  fi
}

# --- Costruzione opzioni Claude ---
# In YOLO mode: --dangerously-skip-permissions + --disallowedTools per bloccare
#   sub-agent spawning e strumenti inutili (Task/Agent/WebSearch/TodoWrite).
#   WebFetch resta abilitato per consultazione mirata di URL dal PLAN.
# In modalita' normale: whitelist di tool specifici per fase.
#
# --max-turns per phase fa da circuit-breaker contro fix-loop costosi:
#   plan: 20 (esplorazione + scrittura mini-plan)
#   exec: 40 (implementazione)
#   fix:  25 (correzione con errori gia' in input, non serve esplorare)
#
# Output: stream-json -> JSONL (dettaglio) + summary (leggibile)
# Ritorna solo il testo risultato per la logica dello script
#
# Uso: run_claude_cmd <prompt> <label> <phase>
#   phase: "plan" | "exec" | "fix"
run_claude_cmd() {
  local prompt="$1"
  local label="${2:-claude}"
  local phase="${3:-exec}"

  # Cap turni per fase
  local max_turns
  case "$phase" in
    plan) max_turns=35 ;;
    exec) max_turns=40 ;;
    fix)  max_turns=25 ;;
    *)    max_turns=40 ;;
  esac

  # Continuazioni per max-turns: solo exec, max 2 (3 invocazioni totali = 120 turni)
  local max_continuations=2
  local continuation=0
  local current_prompt="$prompt"

  while true; do
    local temp=$(mktemp)

    # Selezione flags per fase: plan=utente, exec=sonnet+high, fix=utente
    local flags=("${CLAUDE_FLAGS[@]}")
    [[ "$phase" == "plan" ]] && flags=("${PLAN_FLAGS[@]}")
    [[ "$phase" == "exec" ]] && flags=("${EXEC_FLAGS[@]}")
    [[ "$phase" == "fix"  ]] && flags=("${FIX_FLAGS[@]}")

    if [ "$YOLO_MODE" = "true" ]; then
      claude -p "$current_prompt" "${flags[@]}" --output-format stream-json \
        --max-turns "$max_turns" \
        --dangerously-skip-permissions \
        --disallowedTools "Task" "Agent" "WebSearch" "TodoWrite" > "$temp" 2>> "$LOG_FILE"
    elif [ "$phase" = "plan" ]; then
      claude -p "$current_prompt" "${flags[@]}" --output-format stream-json \
        --max-turns "$max_turns" \
        --allowedTools "Read" "Write" "WebFetch" \
          "Bash(find*)" "Bash(grep*)" "Bash(cat*)" "Bash(ls*)" "Bash(mkdir*)" > "$temp" 2>> "$LOG_FILE"
    else
      # exec + fix: stessa whitelist (build/test consentiti per validazione mirata)
      claude -p "$current_prompt" "${flags[@]}" --output-format stream-json \
        --max-turns "$max_turns" \
        --allowedTools \
          "Read" "Write" "Edit" "WebFetch" \
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

    # Se max-turns raggiunto durante plan: riprova da capo una volta sola,
    # chiedendo al modello di essere piu' conciso nell'esplorazione.
    # Non e' una continuazione ma un fresh restart con nota aggiuntiva.
    if [[ "$phase" == "plan" ]] && check_max_turns "$temp"; then
      rm -f "$temp"
      if [ "$continuation" -lt 1 ]; then
        ((continuation++))
        log "Max-turns raggiunto durante planning — riprovo (tentativo $continuation/1) con nota di concisione"
        current_prompt="$prompt

NOTA: un'invocazione precedente ha esaurito i turni esplorando troppi file.
Questa volta: leggi solo i file strettamente necessari per il task corrente,
evita letture ridondanti del piano, e scrivi il mini-plan appena hai abbastanza contesto."
        continue
      else
        log "Max-turns esaurito nel planning anche al secondo tentativo — skip task"
        echo ""
        return
      fi
    fi

    # Se max-turns raggiunto durante exec: continua automaticamente fino al limite,
    # poi chiede all'utente se riprovare da capo o uscire.
    if [[ "$phase" == "exec" ]] && check_max_turns "$temp"; then
      rm -f "$temp"
      if [ "$continuation" -lt "$max_continuations" ]; then
        ((continuation++))
        log "Max-turns raggiunto durante exec (continuazione $continuation/$max_continuations)"
        current_prompt="Stai continuando un'esecuzione interrotta per max-turns (tentativo $continuation/$max_continuations).
Mini-plan: $TASKS_DIR/$TASK_FILE

Rileggi il mini-plan, controlla i file gia' modificati e completa il task rimanente.
NON rifare lavoro gia' completato. NON fare git add/commit/push. Rispondi SOLO: DONE"
        continue
      else
        log "Max-turns esaurito dopo $max_continuations continuazioni su task: $TASK_FILE"
        echo "" > /dev/tty
        echo "=========================================" > /dev/tty
        echo "  MAX-TURNS ESAURITO" > /dev/tty
        echo "  Task: $TASK_FILE" > /dev/tty
        echo "  Il task non e' stato completato dopo $max_continuations continuazioni." > /dev/tty
        echo "=========================================" > /dev/tty
        local choice
        read -p "  (r) riprova da capo  (q) esci: " choice < /dev/tty
        echo "" > /dev/tty
        case "$choice" in
          r|R)
            log "Utente: riprova da capo (reset continuazioni)"
            continuation=0
            current_prompt="$prompt"
            continue
            ;;
          *)
            log "Utente: uscita su max-turns esaurito"
            echo "MAX_TURNS_ABORT"
            return
            ;;
        esac
      fi
    fi

    rm -f "$temp"
    echo "$result"
    return
  done
}

# Alias per retrocompatibilita' interna
build_plan_cmd() { run_claude_cmd "$1" "${2:-plan}" "plan"; }
build_exec_cmd() { run_claude_cmd "$1" "${2:-exec}" "exec"; }
build_fix_cmd()  { run_claude_cmd "$1" "${2:-fix}"  "fix"; }

print_runtime_summary() {
  echo ""
  echo "========================================="
  echo "  Piano:     $(basename "$PLAN")"
  echo "  Modalita': $MODE"
  echo "  Branch:    $(git branch --show-current)"
  echo "  Modello:   plan=$CLAUDE_MODEL($CLAUDE_EFFORT)  exec=$CLAUDE_MODEL($CLAUDE_EFFORT)  fix=sonnet(high)"
  echo "-----------------------------------------"
  echo "  Log:       $LOG_FILE"
  echo "  Claude:    $CLAUDE_LOG_JSONL"
  echo "  Summary:   $CLAUDE_LOG_SUMMARY"
  echo "========================================="
  echo ""
}

run_task_planning() {
  local task_i="$1"

  log "=== Task $task_i - Planning ==="

  local output
  output=$(build_plan_cmd "$(build_plan_prompt)" "Task $task_i - Plan")

  echo "$output" >> "$LOG_FILE"
  log "Output plan: $(echo "$output" | tail -1)"

  # Rate limit durante il planning -> ferma lo script
  if echo "$output" | grep -q "RATE_LIMITED"; then
    log "Esecuzione interrotta per rate limit durante planning."
    exit 1
  fi

  if echo "$output" | grep -q "ALL_COMPLETE"; then
    log "Tutti i task completati!"
    return 1
  fi

  TASK_FILE=$(echo "$output" | grep -oP 'PLANNED:\K.*')
  if [ -z "$TASK_FILE" ]; then
    log "ERRORE: nessun file di plan generato, skip"
    return 2
  fi

  return 0
}

review_plan_if_needed() {
  local task_file="$1"
  local confirm

  if [ "$AUTO_APPROVE" = "true" ]; then
    return 0
  fi

  echo ""
  echo "========================================="
  echo "Mini-plan: $TASKS_DIR/$task_file"
  echo "========================================="
  cat "$TASKS_DIR/$task_file"
  echo "========================================="
  read -p "Approvare? (y/n/q) " confirm
  case "$confirm" in
    q)
      log "Interrotto dall'utente."
      exit 0
      ;;
    y)
      log "Plan approvato manualmente"
      return 0
      ;;
    *)
      log "Task skippato dall'utente"
      return 1
      ;;
  esac
}

run_task_execution_with_retries() {
  local task_i="$1"
  local task_file="$2"
  local retry=0

  log "=== Task $task_i - Execution ($task_file) ==="

  while [ $retry -le $MAX_RETRIES ]; do
    local output
    if [ $retry -eq 0 ]; then
      # --- Prima esecuzione ---
      output=$(build_exec_cmd "$(build_exec_prompt "$task_file")" "Task $task_i - Exec")
    else
      # --- Retry: fix degli errori ---
      log "=== Task $task_i - Fix attempt $retry/$MAX_RETRIES ==="
      output=$(build_fix_cmd "$(build_fix_prompt "$task_file" "$VERIFY_ERRORS")" "Task $task_i - Fix $retry/$MAX_RETRIES")
    fi

    echo "$output" >> "$LOG_FILE"
    log "Risultato esecuzione: $(echo "$output" | tail -1)"

    # Rate limit durante esecuzione -> ferma lo script
    if echo "$output" | grep -q "RATE_LIMITED"; then
      log "Esecuzione interrotta per rate limit durante exec/fix."
      exit 1
    fi

    # Max-turns abort (utente ha scelto di uscire) -> ferma lo script
    if echo "$output" | grep -q "MAX_TURNS_ABORT"; then
      log "Esecuzione interrotta per max-turns su task: $task_file"
      exit 1
    fi

    # --- Verifica indipendente: build + test ---
    log "=== Task $task_i - Verifica (attempt $((retry + 1))/$((MAX_RETRIES + 1))) ==="
    run_verify

    # --- Esito verifica ---
    if [ -z "$VERIFY_ERRORS" ]; then
      log "Verifica PASSATA"
      return 0
    fi

    log "Verifica FALLITA"
    echo "$VERIFY_ERRORS" >> "$LOG_FILE"
    ((retry++))
  done

  return 1
}

consolidate_unexpected_commits() {
  local pre_task_head="$1"

  # Se Claude ha committato durante l'esecuzione (nonostante le istruzioni),
  # annulla i commit per consolidare tutto in un unico commit dello script
  local current_head
  current_head=$(git rev-parse HEAD 2>/dev/null)
  if [ "$current_head" != "$pre_task_head" ]; then
    local extra_commits
    extra_commits=$(git rev-list --count "$pre_task_head..HEAD" 2>/dev/null || echo 0)
    log "ATTENZIONE: Claude ha creato $extra_commits commit durante l'esecuzione. Annullo con soft reset per consolidare."
    git reset --soft "$pre_task_head" 2>/dev/null
  fi
}

confirm_continue_after_success_if_needed() {
  local task_i="$1"
  local confirm

  if [ "$AUTO_APPROVE" = "true" ]; then
    return 0
  fi

  echo ""
  echo "========================================="
  echo "  Task $task_i completato e committato."
  echo "  Commit: $COMMIT_MSG"
  echo "========================================="
  read -p "Continuare con il prossimo task? (y/q) " confirm
  case "$confirm" in
    q)
      log "Interrotto dall'utente dopo task $task_i."
      exit 0
      ;;
    *)
      log "Utente conferma: prosegui"
      ;;
  esac
}

# Ritorna:
#   0 => passa al task successivo
#   1 => ripeti lo stesso task (i--)
#   2 => interrompi loop task
handle_failed_task_recovery() {
  local task_i="$1"
  local task_file="$2"

  log "ERRORE: Task $task_i FALLITO dopo $MAX_RETRIES tentativi di fix."
  log "Ultimi errori:"
  echo "$VERIFY_ERRORS" >> "$LOG_FILE"

  echo ""
  echo "========================================="
  echo "  Task $task_i FALLITO dopo $MAX_RETRIES tentativi"
  echo "-----------------------------------------"
  echo "$VERIFY_ERRORS" | tail -20
  echo "========================================="

  local recovery_choice=0
  select_menu "Cosa vuoi fare?" recovery_choice \
    "Riprova   - resetta i tentativi e riprova il fix" \
    "Manuale   - correggi tu, poi riprova la verifica" \
    "Salta     - segna come failed, passa al prossimo task" \
    "Esci      - ferma l'esecuzione"

  case $recovery_choice in
    0)
      log "Utente sceglie: riprova task $task_i"
      return 1
      ;;
    1)
      log "Utente sceglie: fix manuale per task $task_i"
      read -p "Correggi il codice, poi premi Enter per rieseguire la verifica... "
      run_verify
      if [ -z "$VERIFY_ERRORS" ]; then
        log "Verifica post-fix manuale PASSATA"
        run_post_success "$task_i" "$task_file"
        return 0
      fi

      log "Verifica post-fix manuale FALLITA"
      echo "$VERIFY_ERRORS" | tail -20
      return 1
      ;;
    2)
      log "Task $task_i skippato dall'utente dopo fallimento"
      return 0
      ;;
    3)
      log "Interrotto dall'utente dopo fallimento task $task_i"
      return 2
      ;;
  esac
}

run_task_loop() {
  for ((i=1; i<=MAX_TASKS; i++)); do
    run_task_planning "$i"
    case $? in
      0) ;;         # planning ok, task file disponibile
      1) break ;;   # ALL_COMPLETE
      2) continue ;; # planning non valido, passa al prossimo task
    esac

    if ! review_plan_if_needed "$TASK_FILE"; then
      continue
    fi

    # Salva HEAD prima dell'esecuzione per rilevare commit non autorizzati di Claude
    PRE_TASK_HEAD=$(git rev-parse HEAD 2>/dev/null)

    if run_task_execution_with_retries "$i" "$TASK_FILE"; then
      consolidate_unexpected_commits "$PRE_TASK_HEAD"
      run_post_success "$i" "$TASK_FILE"
      confirm_continue_after_success_if_needed "$i"
    else
      handle_failed_task_recovery "$i" "$TASK_FILE"
      case $? in
        0) ;;       # next task
        1) ((i--)); continue ;; # retry stesso task
        2) break ;; # uscita loop
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
}

main() {
  initialize_terminal_handling
  configure_execution_context "$@"

  print_runtime_summary

  if [ "$YOLO_MODE" = "true" ]; then
    log "=========================================="
    log "  YOLO MODE - Permessi totali (sandbox)"
    log "=========================================="
  fi

  log "Avvio esecuzione - Modalita': $MODE - Plan/Exec: $CLAUDE_MODEL($CLAUDE_EFFORT) Fix: sonnet(high)"
  log "Piano: $PLAN"
  log "Branch: $CURRENT_BRANCH"

  run_task_loop
}

# ==============================
# MAIN FLOW
# ==============================
main "$@"
