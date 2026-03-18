# Architecture Decision Records (ADR) Guide

ADRs capture the *why* behind significant decisions. Code shows what was
built. Tests show that it works. ADRs explain why this approach was chosen
over the alternatives. They are the institutional memory that prevents
future developers (or agents) from revisiting settled decisions without
context.

## When to Write an ADR

Not every decision needs an ADR. Write one when:

- The decision affects multiple components or layers
- There were genuine alternatives with different trade-offs
- The decision would be non-obvious to someone reading the code later
- Reversing the decision later would be costly
- The human explicitly resolved a decision gate in the implementation plan

Don't write ADRs for routine choices (which assertion library to use,
whether to name a variable X or Y). If you're unsure, err on the side of
writing one — a short ADR costs little and might save someone hours of
archaeology later.

## ADR Structure

ADRs live at `docs/decisions/ADR-<NNN>-<short-title>.md`. Number them
sequentially. The short title should be descriptive enough to identify
the decision from a file listing.

```markdown
# ADR-<NNN>: <Decision Title>

**Date:** YYYY-MM-DD
**Status:** Accepted | Superseded by ADR-<NNN>
**Feature:** FEAT-<N>
**Task:** T-<NN> (from implementation plan)

## Context

What situation or problem prompted this decision? What constraints exist?
Keep it brief — 2-4 sentences that set the scene. Reference the user story
or task that raised the question.

## Options Considered

### Option A: <Name>
- Description of the approach
- Pros: ...
- Cons: ...

### Option B: <Name>
- Description of the approach
- Pros: ...
- Cons: ...

(Add more options if relevant, but two or three is typical.)

## Decision

We chose **Option X** because [concise rationale]. This was decided by
[human / during implementation of T-NN].

## Consequences

What follows from this decision? What becomes easier? What becomes harder?
Are there follow-up actions needed? Note both positive and negative
consequences honestly.
```

## Writing Tips

**Context should be self-contained.** Someone reading the ADR a year from
now shouldn't need to read the full requirements doc to understand the
problem. Give enough context in the ADR itself.

**Be honest about trade-offs.** The point of an ADR is not to justify a
decision after the fact — it's to record the reasoning faithfully. If Option
A was chosen despite a real downside, say so. "We accepted the risk of X
because Y was more important in this context."

**Keep it short.** A good ADR is typically 20-40 lines. If it's getting
long, you're probably explaining too much background or mixing multiple
decisions. Split into separate ADRs if needed.

**Superseding works, deleting doesn't.** If a decision is revisited later
and changed, don't edit the original ADR. Mark it as `Superseded by
ADR-<NNN>` and write a new one that references the original. This preserves
the history.

## Naming Conventions

```
ADR-001-cursor-based-pagination.md
ADR-002-bcrypt-for-password-hashing.md
ADR-003-hard-delete-for-gdpr-compliance.md
```

Use lowercase, hyphen-separated words. The name should be enough to identify
the topic without opening the file.

## Linking ADRs to the Plan

When an ADR is created during execution, reference it in the implementation
plan's task section:

```markdown
**Decisions:**
- Chose cursor-based pagination → ADR-003
```

And in the ADR, reference the task:

```markdown
**Task:** T-04 (from PLAN-001)
```

This bidirectional linking means you can navigate from plan → decision and
from decision → plan without searching.
