---
name: requirements-workflow
description: >
  Guide the full lifecycle from a raw requirements/wishes document to working,
  tested code inside a project. Use this skill whenever the user wants to:
  turn a feature idea or wish-list into structured requirements; create an
  implementation plan from requirements; execute an implementation plan
  task-by-task with traceability; track which user stories are done, which
  tests pass, and what decisions were made. Also trigger when the user says
  things like "plan this feature", "break this down into tasks",
  "implement this requirements doc", "help me go from idea to code",
  "create a plan from these requirements", or refers to a feature document,
  PRD, spec, or wish-list that needs to become working software. Trigger even
  for partial requests like "I have a document with what I want, help me
  organize it" or "what's the next task to implement". This skill covers the
  entire journey: structuring, planning, executing, and documenting.
---

# Requirements-to-Code Workflow

This skill guides the transformation of a raw feature document into working,
tested, documented code. It produces artifacts that are readable by both
humans and coding agents, and keeps a clear trace from every user story
through implementation to passing tests.

## Core Principles

Before diving into the phases, internalize these principles — they inform
every decision throughout the workflow.

**Traceability is the backbone.** Every piece of code and every test must
trace back to a user story. Every user story traces back to a requirement.
If you can't draw the line, something is missing. Use story IDs (like
`US-001`) consistently across all documents so that grep works as a
traceability tool.

**The plan is a living document.** The implementation plan is not a static
spec — it gets updated after every task. Status changes, decisions get
recorded, test results are logged. Anyone (human or agent) reading the plan
at any point should understand exactly what has been done, what remains, and
what decisions shaped the current state.

**Gate decisions before you proceed.** When a task involves an architectural
or design choice that could go multiple ways, stop and surface it. Mark it
clearly, present the options with trade-offs, and wait for the human to
decide. Never silently pick a direction on something that matters.

**Documents live in the repo.** All artifacts go under `docs/` in the
project repository. They are versioned with git, reviewed with the code,
and available to any agent that works on the project later. The recommended
structure is:

```
docs/
  requirements/
    FEAT-<name>.md          ← structured requirements
  plans/
    PLAN-<name>.md          ← implementation plan (living document)
  decisions/
    ADR-<NNN>-<title>.md    ← architecture decision records
```

These paths are conventions, not mandates. Adapt them to the project's
existing structure if one exists.

---

## The Four Phases

The workflow has four phases. You don't always start at Phase 1 — figure out
where the user is and jump in at the right point. Maybe they already have
structured requirements and need a plan. Maybe they have a plan and need
help executing it. Meet them where they are.

### Phase 1 — Structure: From Wishes to Requirements

**Input:** A raw document, conversation, or set of ideas describing what the
user wants.

**Output:** A structured requirements document at
`docs/requirements/FEAT-<name>.md`.

**What to do:**

Read the raw input carefully. Then produce a structured document that
captures what the user actually needs. Read `references/requirements-guide.md`
for detailed guidance on how to structure this document.

The key activities in this phase:

1. **Extract and organize.** Group related desires into functional areas.
   Separate functional requirements (what the system does) from
   non-functional requirements (how well it does it).

2. **Write user stories.** For each functional requirement, write one or more
   user stories with acceptance criteria. Assign each story a stable ID
   (e.g., `US-001`). These IDs will be referenced throughout the entire
   workflow — they are the traceability anchors.

3. **Surface gaps.** The raw document will have implicit assumptions and
   missing pieces. Call them out explicitly: "This implies a notification
   system — is that in scope?" or "What should happen when X fails?" Present
   these as questions, not assumptions.

4. **Identify dependencies.** Note which stories depend on others, and which
   depend on external systems or decisions not yet made.

**Human checkpoint:** Present the structured document to the user. This is a
review gate — the user must confirm or adjust before moving to Phase 2.
Don't rush past this. Getting requirements right saves enormous time later.

---

### Phase 2 — Plan: From Requirements to Implementation Plan

**Input:** A validated requirements document (from Phase 1 or provided by
the user).

**Output:** An implementation plan at `docs/plans/PLAN-<name>.md`.

**What to do:**

Read the requirements document and produce a sequenced plan of
implementation tasks. Read `references/plan-guide.md` for detailed guidance
on structuring the plan.

The key activities in this phase:

1. **Decompose into tasks.** Break the work into tasks that are small enough
   to complete in a single focused session (think: one meaningful PR). Each
   task should have a clear "definition of done" expressed as verifiable
   conditions — typically tests that must pass.

2. **Map stories to tasks.** Every task must reference which user stories it
   advances. Every user story must appear in at least one task. If a story
   isn't covered, the plan is incomplete.

3. **Sequence intelligently.** Order tasks so that dependencies are respected
   and earlier tasks build a foundation for later ones. Prefer an order that
   produces a working (if incomplete) system as early as possible.

4. **Flag decision gates.** If a task requires an architectural or design
   decision that hasn't been made, mark it with a `⚠️ DECISION REQUIRED`
   flag. Describe the decision, list the options you see, note the
   trade-offs of each. These are hard stops — work does not proceed past a
   decision gate until the human resolves it.

5. **Estimate scope.** You don't need precise time estimates, but give a
   rough sense of relative size (small / medium / large) so the human can
   understand the overall effort.

**Human checkpoint:** Present the plan. The user should review the task
sequence, the story coverage, and especially the decision gates. Resolve
any open decisions before proceeding to Phase 3.

---

### Phase 3 — Execute: Task-by-Task Implementation

**Input:** A validated implementation plan.

**Output:** Working code, passing tests, and an updated plan after each task.

**What to do:**

Work through the plan one task at a time, in order. After completing each
task, update the plan document itself to reflect the new state. This is the
phase where Claude Code does the heavy lifting, but the human monitors
progress through the evolving plan document.

For each task:

1. **Read the plan.** Before starting any work, re-read the current state of
   the plan. Check what's been done, what's next, and whether there are any
   unresolved decision gates blocking your path.

2. **Implement.** Write the code, following the project's architecture and
   conventions (check CLAUDE.md if present). Reference the user story IDs in
   code comments where it helps future readers understand *why* something
   exists.

3. **Write and run tests.** Every task's "definition of done" includes tests.
   Write them, run them, make sure they pass. If a test reveals a problem
   with the requirements or the plan, note it — don't silently adjust.

4. **Update the plan.** After completing the task, update the plan document:
   - Change the task status from `[ ]` to `[x]`
   - List the tests that were written and confirm they pass
   - Record any decisions that were made during implementation
   - Note any deviations from the original plan and why
   - If a decision was significant enough, create an ADR
     (see `references/adr-guide.md`)

5. **Stop at decision gates.** If the next task has a `⚠️ DECISION REQUIRED`
   flag, do not proceed. Present the decision to the human with your
   analysis and recommendation, then wait.

**Important behaviors during execution:**

- **One task at a time.** Don't batch multiple tasks. Complete one, update
  the plan, verify tests pass, then move to the next. This keeps the plan
  accurate and gives the human clear progress signals.

- **Commit meaningfully.** Each completed task should correspond to a
  logical commit (or small set of commits). Reference the task ID and
  user story IDs in commit messages.

- **Surface surprises.** If during implementation you discover that a
  task is much larger than expected, that the requirements have a
  contradiction, or that a prior assumption was wrong — stop and
  communicate. Update the plan with what you found before proceeding.

---

### Phase 4 — Close: Review and Documentation

**Input:** A completed plan with all tasks done.

**Output:** A finalized plan document that serves as the feature's
implementation record.

**What to do:**

1. **Verify completeness.** Walk through the requirements document and
   confirm every user story has been implemented and tested. The plan
   should show a clear mapping: story → task(s) → test(s) → passing.

2. **Finalize the plan.** Add a summary section at the top of the plan
   document noting completion date, any scope changes that occurred, and
   pointers to the key ADRs that were created.

3. **Review ADRs.** Make sure all significant decisions made during
   execution have been captured as ADRs. These are the institutional
   memory of *why* things are the way they are.

4. **Present to the human.** Give a concise summary of what was built,
   what changed from the original plan, and what decisions were made.
   The human should be able to read just this summary and understand the
   full story — and then dive into the plan or ADRs for details.

---

## Entering the Workflow Mid-Stream

Users won't always start from Phase 1. Here's how to detect where to begin:

- **User has a vague idea or wish-list** → Start at Phase 1
- **User has structured requirements but no plan** → Start at Phase 2
- **User has a plan document** → Start at Phase 3
- **User says "what's next" or "continue"** → Find the plan, read its
  current state, and resume Phase 3 at the next incomplete task
- **User says "are we done?"** → Jump to Phase 4

If in doubt, ask. A quick "It looks like you have requirements already —
shall I create an implementation plan from these?" is better than guessing
wrong.

---

## Working with Existing Project Conventions

Before creating any documents, check if the project has:
- A `CLAUDE.md` file with operational conventions
- An existing `docs/` structure
- Naming conventions for files, branches, or commits

Respect what's already there. If the project uses a different docs structure,
adapt. If CLAUDE.md defines commit message formats, follow them. The
workflow is a guide, not a straightjacket.

---

## Reference Files

For detailed guidance on document structure and content:

- `references/requirements-guide.md` — How to structure a requirements
  document, write good user stories, and define acceptance criteria
- `references/plan-guide.md` — How to structure an implementation plan,
  define tasks, and set up decision gates
- `references/adr-guide.md` — How to write Architecture Decision Records

Read the relevant reference file when you enter a phase for the first time.
You don't need to read all three upfront.
