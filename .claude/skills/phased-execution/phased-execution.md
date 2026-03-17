---
name: phased-execution
description: "Manage multi-phase implementation plans for complex features. Use this skill whenever the user asks to plan a large feature, implement a complex change, or when a generated plan has 4+ distinct phases/steps. Also trigger when the user says 'continua il piano', 'esegui fase N', 'continue the plan', 'execute phase N', 'riprendi il piano', or references a plan file in docs/plans/. Trigger even for indirect signals like 'this is too big for one session', 'let's break this down', 'pianifica [feature]', or when Claude Code itself recognizes that a plan is too large for a single session. This skill handles the full lifecycle: plan generation, structured saving, phase-by-phase execution with context handoff between sessions, and progress tracking."
---

# Phased Execution

Break complex features into independently-executable phases, each completable in a single Claude Code session with clean context handoff between sessions.

## Why this exists

Long plans executed in a single session degrade in quality as the context window fills. This skill solves the problem by:
1. Structuring plans into phases saved to a file
2. Executing one phase per session
3. Writing handoff notes so the next session starts with the right context
4. Tracking progress across sessions

## When to activate

- The user asks to plan a feature and the resulting plan has **4+ logical phases**
- The user explicitly asks to break work into phases
- The user says "continua il piano", "esegui fase N", "execute phase N", or similar
- The user references a file in `docs/plans/`

## Plan lifecycle

```
Generate plan → Save to docs/plans/ → Execute phase 1 → Commit → Handoff note
    ↓                                                          ↓
User opens new session → "continua il piano" → Read plan → Execute phase 2 → ...
```

---

## Phase 1: Generating and saving a plan

When the user asks to plan a complex feature, follow these steps:

### 1. Plan normally

Think through the implementation as you normally would. Group work into logical phases where each phase:
- Touches **at most 3-4 files** (excluding test files and docs)
- Produces a **working, committable state** (no half-implemented features)
- Takes roughly **one Claude Code session** to complete
- Has **clear boundaries** — the next phase doesn't need to undo anything from this one

### 2. Evaluate complexity

After generating the plan, count the phases:
- **1-3 phases:** Execute directly, no need for the phased-execution workflow. Just do the work.
- **4+ phases:** Save the plan as a structured file and enter the phased workflow.

### 3. Save the plan file

Create the file at `docs/plans/<slug>.md` where `<slug>` is a short kebab-case name (e.g., `user-roles`, `order-management`, `password-reset-flow`).

Use this exact template:

```markdown
# Piano: <Feature Name>

**Status:** in-progress
**Fase corrente:** 1
**Creato:** <YYYY-MM-DD>
**Branch:** feature/<branch-name>

## Contesto

<1-2 paragraphs: what this feature is, why it's being built, any key design decisions made upfront.>

---

### Fase 1: <Titolo>
**Status:** todo
**Obiettivo:** <One sentence: what this phase achieves>

**Tasks:**
- [ ] Task description
- [ ] Task description

**Test da aggiungere:**
- <Test case description>

**Docs da aggiornare:**
- <Doc file or "Nessuno">

**Handoff note:** _(written after completion)_

---

### Fase 2: <Titolo>
**Status:** todo
**Obiettivo:** ...

...same structure...
```

**Important rules for the plan file:**
- Write in Italian (matching the user's language preference) unless the user explicitly uses English
- Each phase MUST have the Test and Docs sections filled — think about them during planning, not after
- Tasks should be concrete and actionable, not vague ("Add OrderStatus enum to Domain" not "Set up domain models")
- If a phase depends on a previous one, note it explicitly: "Dipende da: Fase N"

### 4. Confirm with the user

After saving, tell the user:
```
Piano salvato in docs/plans/<slug>.md con N fasi.
Vuoi che inizi con la Fase 1, o vuoi prima rivedere il piano?
```

---

## Phase 2: Executing a phase

This is triggered when the user says things like:
- "esegui fase 1" / "execute phase 1"
- "continua il piano" / "continue the plan"
- "riprendi il piano <slug>"
- "vai con la prossima fase"

### 1. Read the plan file

Look in `docs/plans/` for the active plan:
- If user specifies a plan name, read that file
- If user says "continua il piano" without specifying, check `docs/plans/` for files with `Status: in-progress`. If there's exactly one, use it. If there are multiple, ask which one.

### 2. Identify the current phase

Read the `Fase corrente` field and find the corresponding phase section. Verify its status is `todo` or `in-progress`.

### 3. Execute the phase

Work through the tasks listed in the phase. Follow all existing project conventions from CLAUDE.md:
- Write tests as specified in the phase's "Test da aggiungere" section
- Update docs as specified in the phase's "Docs da aggiornare" section
- Use conventional commits
- Run the relevant test suite to confirm everything passes

### 4. Update the plan file

After completing the phase:

**a) Mark the phase as done:**
```markdown
### Fase N: <Titolo>
**Status:** done
```

**b) Check all task checkboxes:**
```markdown
- [x] Task description
```

**c) Write the handoff note.** This is critical — it's the context bridge for the next session. Include:
- What was created/changed (concrete: file names, class names, endpoints)
- Any decisions made during implementation that affect future phases
- Anything the next phase needs to know that isn't obvious from the code

Example:
```markdown
**Handoff note:** Creata entità Order in Domain con OrderStatus enum (Pending, Confirmed, Shipped, Delivered, Cancelled). Aggiunto OrderConfiguration in Infrastructure con indice su CustomerId. Migrazione 20250315_AddOrdersTable applicata. Nota: ho usato decimal(18,2) per TotalAmount come discusso. La Fase 3 dovrà aggiungere il DTO OrderResponse in Shared che mappa tutti i campi.
```

**d) Update the header:**
```markdown
**Fase corrente:** N+1
```
(or if this was the last phase, change Status to `completed`)

### 5. Commit

Commit with a message that references the plan:
```
feat(<scope>): <description> [piano: <slug> fase N]
```

Example: `feat(api): add orders table and entity [piano: order-management fase 1]`

### 6. Signal end of phase

Tell the user:
```
Fase N completata e committata.
Handoff note scritta nel piano.
→ Per la prossima fase, apri una nuova sessione e di': "continua il piano"
```

If this was the last phase, instead say:
```
Fase N completata — piano <slug> terminato!
Tutte le fasi sono state implementate. Il file del piano è stato aggiornato con status "completed".
```

---

## Handling edge cases

### Phase is too large during execution
If you realize mid-execution that a phase is bigger than expected, STOP. Split it:
1. Complete what you can up to a clean committable state
2. Update the plan file: mark a partial completion, add a new sub-phase (e.g., "Fase 3a", "Fase 3b")
3. Write the handoff note for what was done
4. Signal the user to continue in a new session

### Plan needs revision
If during execution you discover the plan needs changes (e.g., a phase is no longer needed, or tasks should move between phases), update the plan file BEFORE executing. Tell the user what changed and why.

### Multiple plans in progress
It's fine to have multiple plan files in `docs/plans/`. The `Status` field distinguishes them:
- `in-progress` — currently being worked on
- `completed` — all phases done (keep for reference)
- `paused` — temporarily on hold (user can resume later)

### Resuming after code changes outside the plan
If the user made changes between sessions that affect the plan (e.g., a hotfix that touches the same code), read the git log since the last phase commit and note any conflicts in the handoff before proceeding.

---

## Quick reference

| User says | Action |
|-----------|--------|
| "pianifica [feature]" | Generate plan → evaluate complexity → save if 4+ phases |
| "esegui fase N" | Read plan → execute phase N → update → commit → handoff |
| "continua il piano" | Find in-progress plan → execute next todo phase |
| "stato del piano" | Read and summarize plan progress |
| "rivedi il piano" | Show plan, let user request changes before executing |

---

## What NOT to do

- Don't execute multiple phases in one session — the whole point is fresh context per phase
- Don't skip writing the handoff note — it's the most important part for cross-session continuity
- Don't leave the plan file out of sync with reality — if you did it, update the file
- Don't commit the plan file changes separately — include plan updates in the same commit as the phase work
