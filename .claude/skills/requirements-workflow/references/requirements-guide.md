# Requirements Document Guide

This reference explains how to produce a good structured requirements
document from raw input. The goal is a document that is clear enough for a
human to review and precise enough for a coding agent to implement from.

## Document Structure

A requirements document doesn't need a rigid template — its structure should
fit the feature. But it generally covers these areas:

### Overview

A short paragraph (3-5 sentences) explaining what this feature is, why it
exists, and who benefits. This is context for anyone reading the document
later. Write it so that someone unfamiliar with the project can understand
the purpose without reading anything else.

### Functional Requirements

Group these by domain or capability area. Each requirement should be a clear
statement of what the system must do — not how it does it. Use plain
language and avoid implementation details.

Good: "The system must allow users to reset their password via email."
Bad: "Add a POST /api/reset-password endpoint that sends a MailKit email."

The "how" belongs in the implementation plan, not here.

### Non-Functional Requirements

These describe qualities and constraints: performance targets, security
requirements, accessibility standards, compliance needs (like GDPR),
scalability expectations, and so on. Be specific where possible — "fast"
means nothing, "responds within 200ms for 95th percentile" means something.

Not every feature has significant non-functional requirements. Don't force
them if they don't apply.

### User Stories

This is the heart of the document. Each user story captures a discrete
capability from the user's perspective.

**Format:**

```markdown
#### US-<NNN>: <Short descriptive title>

**As a** <role>,
**I want** <capability>,
**So that** <benefit>.

**Acceptance Criteria:**
- [ ] <Criterion 1: a specific, testable condition>
- [ ] <Criterion 2>
- ...

**Notes:** <Optional context, edge cases, or open questions>
```

**Writing good stories:**

- **One capability per story.** If a story has "and" in the "I want" clause,
  it's probably two stories.

- **Acceptance criteria must be testable.** Each criterion should be
  something you can write an automated test for (or at least verify
  mechanically). "The UI looks nice" is not testable. "The form validates
  email format and shows an error message for invalid input" is testable.

- **ID stability matters.** Once a story gets an ID like `US-001`, that ID
  is permanent. It will be referenced in the implementation plan, in commit
  messages, in test names, and in ADRs. Never reuse or renumber IDs.

- **It's OK to have stories of different sizes.** Some stories are small
  ("user can see their profile"). Some are larger ("user can export their
  data in multiple formats"). The implementation plan will handle
  decomposition — the requirements document captures intent.

### Dependencies and Constraints

Note relationships between stories ("US-003 requires US-001 to be complete
first") and external dependencies ("Requires access to the payment gateway
API"). Also note anything explicitly out of scope — this prevents scope
creep and sets clear expectations.

### Open Questions

If the raw input leaves things ambiguous, capture the questions explicitly
rather than making assumptions. Present them as a numbered list so the human
can address them efficiently. Format:

```markdown
### Open Questions

1. **Notifications:** US-005 implies email notifications — is a notification
   system in scope for this feature, or should we use a simple approach for
   now?
2. **Roles:** The document mentions "admin users" but doesn't define what
   admin access means. Which capabilities are admin-only?
```

## Extracting Requirements from Raw Input

Raw input comes in many forms: a conversation, a bullet-point wish-list, a
long narrative document, or even a series of "I want X" statements. Here's
how to handle common patterns:

**Vague desires** → Ask clarifying questions before writing the story. "I
want it to be secure" needs unpacking: secure against what? Authentication?
Authorization? Data encryption at rest?

**Implementation-level requests** → Extract the underlying need. If the user
says "add a Redis cache for the product list", the requirement is probably
"Product list page must load within X ms even with large catalogs." The
Redis cache is a potential solution, not a requirement.

**Missing personas** → If the raw input doesn't specify roles, ask. "Users
can manage products" — which users? All of them? Only admins? This matters
for authorization design.

**Implicit requirements** → Look for things the user didn't say but clearly
expects. If they want user registration, they almost certainly also want
password reset. If they want data export, they might need it in a specific
format. Surface these as questions rather than silently adding stories.

## Quality Checklist

Before presenting the requirements document to the human, verify:

- Every functional requirement has at least one user story
- Every user story has testable acceptance criteria
- Story IDs are unique and sequential
- Dependencies between stories are noted
- Ambiguities are captured as open questions, not as assumptions
- Non-functional requirements are specific and measurable where possible
- The overview makes sense to someone reading it cold
