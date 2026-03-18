# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Repository Structure

This is a full-stack seed/starter application with three main areas:

- `backend/` — ASP.NET Core 10 Web API (.NET solution `Seed.slnx`)
- `frontend/web/` — Angular 21 SPA with SSR (Angular workspace with multiple projects)
- `frontend/mobile/` — .NET MAUI mobile app (`Seed.Mobile.csproj`)
- `docker/` — Docker Compose for local infrastructure (PostgreSQL + API + web)

## Backend (.NET)

**Architecture:** Clean Architecture with 5 projects:
- `Seed.Api` — ASP.NET Core controllers, middleware, entry point. References Application, Infrastructure, Shared.
- `Seed.Application` — MediatR CQRS handlers, FluentValidation validators, Mapster DTOs. References Domain, Shared.
- `Seed.Domain` — Domain entities and business logic. No external dependencies.
- `Seed.Infrastructure` — EF Core with Npgsql (PostgreSQL), ASP.NET Identity, Serilog sinks. References Application.
- `Seed.Shared` — Cross-cutting utilities shared by Application and Api.

**Key packages:** MediatR 14, FluentValidation 12, Mapster 7, EF Core 10, Npgsql EF 10, Asp.Versioning, JWT Bearer auth, MailKit (SMTP email), Serilog (Console + Seq sinks), Swashbuckle/OpenAPI.

**Test projects** (`backend/tests/`):
- `Seed.UnitTests` — xUnit + NSubstitute + FluentAssertions. Tests Application and Domain.
- `Seed.IntegrationTests` — xUnit + Testcontainers.PostgreSql + `Microsoft.AspNetCore.Mvc.Testing`. Tests via Api + Infrastructure.

### Backend Commands

Run from `backend/`:

```bash
# Build
dotnet build Seed.slnx

# Run API
dotnet run --project src/Seed.Api

# Run all tests
dotnet test Seed.slnx

# Run a specific test project
dotnet test tests/Seed.UnitTests
dotnet test tests/Seed.IntegrationTests

# Run a single test
dotnet test tests/Seed.UnitTests --filter "FullyQualifiedName~MyTestMethod"

# EF Core migrations (run from backend/)
dotnet ef migrations add <MigrationName> --project src/Seed.Infrastructure --startup-project src/Seed.Api
dotnet ef database update --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

## Frontend Web (Angular)

Angular workspace at `frontend/web/` with 4 projects:
- `app` — Main application (`projects/app/`). SSR-enabled with Express.
- `shared-ui` — Reusable UI component library (`lib` prefix).
- `shared-core` — Core utilities/services library.
- `shared-auth` — Authentication library.

The three `shared-*` projects are Angular libraries built with `ng-packagr`.

**Key packages:** Angular Material 21, Angular CDK, RxJS 7, Vitest 4 (test runner), Prettier 3.

### Frontend Web Commands

Run from `frontend/web/`:

```bash
# Install dependencies
npm install

# Dev server (http://localhost:4200)
npm start          # or: ng serve

# Build (production)
npm run build      # or: ng build

# Run all tests
npm test           # or: ng test

# Run tests for a specific project
ng test app
ng test shared-auth
ng test shared-core
ng test shared-ui

# Run SSR server (after build)
npm run serve:ssr:app

# Scaffold (run from frontend/web/)
ng generate component projects/app/src/app/<name>
ng generate component projects/shared-ui/src/lib/<name>
```

## Docker / Local Infrastructure

Run from `docker/`:

```bash
docker compose up          # Starts PostgreSQL, API (port 5000), web (port 4200)
docker compose up postgres # Start only the database
```

Database: PostgreSQL 16, DB `seeddb`, user `seed`, password `seed_password`, port `5432`.

Connection string for local development: `Host=localhost;Database=seeddb;Username=seed;Password=seed_password`

## Database Migrations

See `docs/production-migrations.md` for the full production migration strategy.

**Rules for production-safe migrations:**
- **Always safe:** new table, nullable column, column with default value, new index
- **Two-deploy pattern:** rename column, change column type (add new → migrate data → drop old)
- **Three-step pattern:** add NOT NULL column (add nullable → backfill → alter to NOT NULL)
- **Never in a single deploy:** drop a column/table in active use by the running code
- **Large tables (100K+ rows):** use `CREATE INDEX CONCURRENTLY` via raw SQL in migrations
- **Never modify** an existing migration that has been applied to any environment — always create a new one
- **Name descriptively:** `AddOrdersTable`, `AddEmailIndexToUsers`, not `Update1` or `Fix`

**Commands (run from `backend/`):**
```bash
# Add a new migration
dotnet ef migrations add <MigrationName> --project src/Seed.Infrastructure --startup-project src/Seed.Api

# Build migration bundle locally (to verify it compiles)
dotnet ef migrations bundle --project src/Seed.Infrastructure --startup-project src/Seed.Api -o efbundle --force

# Check for pending model changes (CI runs this automatically)
dotnet ef migrations has-pending-model-changes --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

## Development Patterns

- **Backend CQRS:** New features go as MediatR `IRequest`/`IRequestHandler` pairs in `Seed.Application`. Validators use FluentValidation. DTOs use Mapster mapping configs.
- **API versioning:** Uses `Asp.Versioning.Mvc` — follow existing versioning conventions when adding new controllers.
- **Angular libraries:** When adding shared functionality, export it from the appropriate library's `public-api.ts`. Build libraries before the app in CI.
- **Angular signals:** The app uses Angular signals (`signal()`) — prefer signals over observables for local component state.
- **Email service:** SMTP configuration is optional (`Smtp` section in `appsettings.json`). If `Smtp:Host` is set, uses `SmtpEmailService` (MailKit); otherwise falls back to `ConsoleEmailService` (logs to console). See `docs/AUTH_IMPLEMENTATION.md` for details.
- **Nullable references:** All .NET projects have `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.

## Testing Strategy

When completing a code change, evaluate whether tests are needed before considering the task done.

**Always write tests for:**
- New features (handlers, endpoints, services, components with logic)
- Bug fixes (write a test that reproduces the bug, then fix it)
- Changes to business logic or validation rules

**No tests needed for:**
- Pure refactors with no behavioral change (existing tests should still pass)
- Documentation, config, or style-only changes
- Scaffolding or boilerplate with no logic

**Guidelines:**
- Prefer unit tests. Use integration tests only when the change involves database queries, HTTP pipeline, or cross-layer behavior.
- When planning a task, include a "Tests" step that lists which test cases to add — keep it to the essential cases, not exhaustive.
- When fixing a bug, write the failing test first, then apply the fix.
- Follow existing test conventions in the project (naming, structure, patterns). Look at nearby test files for reference.
- Run the relevant test suite after writing tests to confirm they pass.

## Documentation

All project documentation lives in `docs/`. **After completing any code change** (feature, bug fix, refactor that changes behavior), evaluate whether documentation needs updating before considering the task done. This check should be lightweight — not a full audit, just a quick assessment based on what was changed.

**When to update docs:**
- **New feature or behavioral change:** Update the relevant existing doc, or create a new one in `docs/` if no existing doc covers the topic. If a new file is created, add it to the index table in `README.md` and to the list below. When updating an existing doc, check if its description in the index below still matches the content — update it if needed.
- **Bug fix:** If the root cause or resolution would help someone in the future, add it to the "Troubleshooting" section of the relevant doc. If not tied to a specific topic, add it to `docs/troubleshooting.md`.
- **No doc update needed:** Pure refactors with no behavioral change, test-only changes, or trivial fixes (typos, formatting) don't require doc updates.

**When to update CLAUDE.md:**
- New development pattern, library, or architectural change that affects how future code should be written
- New commands, tools, or workflows that Claude should know about
- Changes to conventions (naming, branching, testing) that override existing instructions
- Do NOT update CLAUDE.md for feature-specific details — those belong in `docs/`

Documentation changes should be included in the same PR as the code change.

## Planning Complex Work

Before starting any implementation, evaluate the scope. If the work is too complex for a single session — many files, multiple distinct concerns, risk of context degradation — use the **phased-execution** skill automatically:
1. Generate and save a structured plan to `docs/plans/<slug>.md`
2. STOP — do not start executing in the same session
3. Each phase is executed in a separate session via "continua il piano" or "esegui fase N"

This is not optional for complex work. If in doubt, prefer phased execution over trying to fit everything in one session.

## Branches

Branch naming convention:
- **Feature branches:** `feature/<nome-branch>` — always branch off from `dev`
- **Hotfix branches:** `hotfix/<nome-branch>` — always branch off from `master`

When creating a new branch (e.g. when the user asks to checkout a new branch), always follow this convention:
```bash
# Feature branch
git checkout dev && git pull origin dev && git checkout -b feature/<nome-branch>

# Hotfix branch
git checkout master && git pull origin master && git checkout -b hotfix/<nome-branch>
```

## Commits

Follow [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>(<scope>): <short description>
```

Types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`, `style`, `perf`
Scopes: `api`, `app`, `auth`, `infra`, `ui`, `core`, `docker`, `ci`, `mobile`

Examples:
- `feat(api): add password reset endpoint`
- `fix(app): prevent double submit on login form`
- `docs(auth): add troubleshooting section for token refresh`
- `refactor(infra): extract email service interface`
- `chore(docker): update postgres to 16.2`

Rules:
- Subject line max 72 chars, lowercase, no period at end
- Use imperative mood ("add" not "added")
- When asked to commit, propose the message and wait for user confirmation before executing
- **Do not** add `Co-Authored-By` trailers to commit messages

## Pull Requests

When creating a PR (via `gh pr create`), structure the description as follows:
- **Summary:** What was changed and why (1-3 bullet points)
- **Key decisions:** Design choices worth noting, if any
- **How to test:** Steps or commands to verify the change

Keep the title short (<70 chars). Put details in the body, not the title. The PR description should be useful to a reviewer who has no prior context.


Existing docs:
- `docs/authentication.md` — JWT auth with refresh token rotation, Angular integration, token persistence, password reset flows. Read when touching auth handlers, login/signup UI, or token logic.
- `docs/ci-cd.md` — CI/CD pipelines, branch protection, Docker image publishing to GHCR, deploy workflows. Read when modifying GitHub Actions or deployment strategy.
- `docs/production-migrations.md` — Migration bundles, backup procedures, expand-contract patterns, rollback. Read before creating or modifying any EF Core migration.
- `docs/smtp-configuration.md` — SMTP auto-switch (console fallback), Gmail dev setup, Brevo production, DNS/SPF/DKIM. Read when configuring or debugging email sending.
- `docs/vps-setup-guide.md` — Server setup, Docker, Nginx reverse proxy, Cloudflare CDN/SSL, manual deploy. Read when setting up or troubleshooting a VPS deployment.
- `docs/new-project-deploy-guide.md` — Fork-and-deploy checklist: repo setup, CI/CD updates, VPS config, Cloudflare, GitHub Secrets. Read when deploying a new project from this seed.
- `docs/troubleshooting.md` — Catch-all for issues not covered in topic-specific docs. Add here when a fix isn't tied to a specific topic.
- `docs/plans/` — Directory containing phased implementation plans. Read when the user references a plan or says "continua il piano". See the phased-execution skill for the full workflow.
