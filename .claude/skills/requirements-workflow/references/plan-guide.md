# Implementation Plan Guide

This reference explains how to create and maintain an implementation plan —
the living document that bridges requirements and code. The plan is both a
roadmap (what to do next) and a log (what was done and why).

## Document Structure

The plan has two main sections: a header with metadata and status, and a
sequence of tasks.

### Header

```markdown
# Implementation Plan: FEAT-<N> — <Feature Name>

**Requirements:** `docs/requirements/FEAT-<N>.md`
**Status:** In Progress | Complete
**Created:** YYYY-MM-DD
**Last Updated:** YYYY-MM-DD

## Story Coverage

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-001 | User can register | T-01, T-02 | ✅ Done |
| US-002 | User can log in | T-03 | 🔄 In Progress |
| US-003 | Admin can manage users | T-05, T-06 | ⏳ Not Started |
```

The story coverage table is the traceability matrix. At a glance, anyone can
see which stories are implemented, which are in progress, and which remain.
Update this table as tasks are completed.

### Tasks

Each task is a section in the document. Tasks are numbered sequentially
(T-01, T-02, ...) and ordered by execution sequence.

```markdown
## T-01: Set up domain entities for User and Role

**Stories:** US-001, US-003
**Size:** Small
**Status:** [x] Complete

**What to do:**
Create the User and Role entities in the Domain layer. Include value objects
for Email and Password (hashed). Add domain validation rules.

**Definition of Done:**
- [ ] User entity with Id, Email, PasswordHash, CreatedAt
- [ ] Role entity with Id, Name
- [ ] Value object for Email with format validation
- [ ] Unit tests for entity creation and validation rules
- [ ] All tests pass

**Decisions:**
- Decided to use record types for value objects (simpler, immutable by
  default). See ADR-001.

**Test Results:**
- `UserTests.cs`: 5 tests, all passing
- `EmailValueObjectTests.cs`: 3 tests, all passing

**Notes:**
- PasswordHash will use BCrypt — the actual hashing implementation comes in
  T-02.
```

## Writing Good Tasks

### Size and Scope

A task should be completable in a single focused session. In Claude Code
terms, this means one conversation or one agentic run. The practical test:
can you describe the "definition of done" in 3-7 bullet points? If you need
more, the task is too big — split it.

Tasks that are too small create noise. "Add a property to User" is not a
task — it's a line of code within a task. A good task produces a meaningful,
testable increment.

### Definition of Done

This is the most important part of each task. Every bullet point in the
definition of done should be something that can be verified — ideally by
running a test. Avoid subjective criteria like "code is clean" or "follows
best practices." Instead: "Unit tests for X cover Y scenarios and pass."

The definition of done items start as unchecked (`- [ ]`). As the task is
executed, they get checked off (`- [x]`). When all items are checked, the
task status changes to Complete.

### Story References

Every task must reference at least one user story. This is how traceability
works. When you complete T-01 and it references US-001, you can update the
story coverage table to reflect progress.

A single story might span multiple tasks. That's fine — the story isn't
"done" until all tasks that reference it are complete.

### Task Sequencing

Order tasks so that:

1. **Dependencies flow downward.** If T-03 depends on T-01 and T-02, it
   comes after both. Make dependencies explicit in the task description
   when they exist.

2. **A working system emerges early.** Prefer an order where the first few
   tasks produce something that compiles and runs, even if it's minimal.
   This gives confidence and a base to build on.

3. **Risky decisions come early.** If a task involves a decision gate that
   could change the approach for later tasks, schedule it early. Don't
   build five tasks on an assumption that might be wrong.

## Decision Gates

When a task involves a choice that affects architecture, data model, external
dependencies, or user-facing behavior, mark it as a decision gate:

```markdown
## T-04: Implement pagination for product list

**Stories:** US-007
**Size:** Medium
**Status:** [ ] Not Started

⚠️ **DECISION REQUIRED: Pagination Strategy**

We need to choose a pagination approach for the product list API:

**Option A — Offset-based pagination**
- Simpler to implement
- Works well with SQL OFFSET/LIMIT
- Trade-off: performance degrades on large datasets, inconsistent results
  if data changes between pages

**Option B — Cursor-based pagination**
- More complex implementation
- Consistent results regardless of data mutations
- Trade-off: can't jump to arbitrary page numbers

**Recommendation:** Option B if the product catalog is expected to grow
beyond ~10K items. Option A if it's a small, stable dataset.

**Awaiting decision from:** [human]
```

The `⚠️ DECISION REQUIRED` marker is a hard stop. During execution (Phase 3),
the agent must not proceed past this task until the human has made the
decision. Once decided, update the task:

```markdown
⚠️ **DECISION: Pagination Strategy** → Option B (cursor-based)
Decided on YYYY-MM-DD. Rationale: catalog expected to grow significantly.
See ADR-003 for full context.
```

## Updating the Plan During Execution

After each task is completed, the plan must be updated. This is not optional —
it's the mechanism that makes the plan useful as a monitoring tool.

**What to update:**

1. **Task status:** `[ ] Not Started` → `[x] Complete`
2. **Definition of done items:** Check off completed items
3. **Test results:** List the test files/classes and their status
4. **Decisions section:** Record any decisions made during implementation,
   even small ones
5. **Notes:** Add anything relevant — gotchas, deviations, things the next
   task should be aware of
6. **Story coverage table:** Update the status of affected stories
7. **Last Updated date:** in the header

**What NOT to do:**

- Don't rewrite the original task description after the fact. The plan is a
  record. If something changed, note the change — don't pretend the original
  plan always said that.
- Don't remove decision gates after they're resolved. Change the marker from
  `DECISION REQUIRED` to `DECISION` and record the outcome.

## Handling Plan Changes

Sometimes during execution, you discover that the plan needs to change —
a task is missing, a task needs to be split, the sequence is wrong, or a
new decision gate has appeared.

When this happens:

1. **Note the change explicitly.** Add a section to the affected task
   explaining what changed and why.
2. **Add new tasks** at the end of the sequence with the next available
   number. Don't insert tasks between existing ones (it shifts all the IDs
   that other documents might reference).
3. **If a task becomes unnecessary**, mark it as `[-] Skipped` with a
   reason, rather than deleting it.

## Protected Paths

Some files in the repository are infrastructure-critical: CI/CD pipelines,
Docker configurations, agent instructions, and security settings. Changes
to these files can have outsized blast radius — a bad edit to a workflow
file can disable all CI checks, and a change to agent instructions can
alter behavior across all future sessions.

These paths are **protected**:

```
.github/              — CI/CD workflows, Dependabot, PR templates
docker/docker-compose*.yml — Container orchestration
docker/Dockerfile.*   — Container image definitions
docker/nginx/         — Reverse proxy configuration
```

Note: `.claude/` and `CLAUDE.md` are **not** protected — the workflow
itself needs to update plans, skills, and project instructions. They
remain read-write in both interactive and autonomous mode.

When a task requires modifying any protected path, mark it with
`🔒 INTERACTIVE ONLY`:

```markdown
## T-07: Add Gitleaks secret scanning to CI

**Stories:** US-012
**Size:** Small
**Status:** [ ] Not Started
🔒 **INTERACTIVE ONLY** — modifies `.github/workflows/`

**What to do:**
Add a Gitleaks scanning step to the CI pipeline...
```

This marker means the task must **not** be executed in autonomous/sandbox
mode. It should be done interactively with Claude Code, where the human
can review each change before it's applied.

During Phase 2 (planning), actively scan each task's scope for protected
path modifications. If a task mixes application code with protected path
changes, split it: keep the code change as a normal task and extract the
infrastructure change into a separate `🔒 INTERACTIVE ONLY` task. This
makes it clear which parts of the plan can run autonomously and which
require human presence.

## Quality Checklist

Before presenting the plan to the human, verify:

- Every user story from the requirements is covered by at least one task
- Every task references at least one user story
- Tasks are ordered respecting dependencies
- Decision gates are clearly marked with options and trade-offs
- Each task has a verifiable definition of done
- The story coverage table is complete and consistent
- Tasks that modify protected paths are marked `🔒 INTERACTIVE ONLY`
- No task mixes protected-path changes with application code changes
