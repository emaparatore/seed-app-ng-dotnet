# Seed App - Angular + .NET Full Stack

A battle-tested, production-ready full-stack seed/starter application. Includes authentication, subscriptions, CI/CD pipeline, monitoring, and Docker-based infrastructure — everything you need to launch.

- ASP.NET Core backend (PostgreSQL)
- Angular web frontend (with SSR support)
- Docker setup for local development and test execution

If you want to create a product from this repository, start with [Using This Seed](docs/getting-started/using-this-seed.md) 



## What's already included

The seed ships with working application capabilities, not just an empty stack skeleton:

- Authentication flows: registration, login, confirm email, forgot/reset password, profile, JWT auth
- Admin area: RBAC permissions, users, roles, audit log, settings, system health, dashboard
- Subscription module: pricing, plans, checkout, subscription management, invoice requests, feature gating
- Email integration: SMTP support with console fallback for local development
- Bootstrap and seeding: deployment-time initialization of roles, permissions, admin user, system settings
- Delivery pipeline: CI, Docker image publish, VPS deploy, migrations, seeding, health checks
- Operations docs: monitoring, rollback, troubleshooting, environment backup

For a navigable overview inside the app, open `/features`. Each feature has its own dedicated page with included behavior, related routes, documentation paths, and code areas.

## Prerequisites

- Docker Desktop (or Docker Engine + Compose)
- .NET SDK 10 (only needed for local `dotnet` commands and `docker/TestRunner`)
- Node.js 22 (only needed if you run frontend outside Docker)

## Quick start

```bash
# 1) Prepare env
cd docker
cp .env.example .env

# 2) Start full app (dev mode, default)
docker compose up
```

App URLs:
- Web: `http://localhost:4200`
- API: `http://localhost:5035`
- Seq: `http://localhost:8081`

Feature catalog in the running app:
- Web catalog: `http://localhost:4200/features`

Run all tests with Docker only:

```bash
dotnet run --project docker/TestRunner -- all
```

## Run with Docker (recommended)

### 1) Environment file

From `docker/`:

```bash
cp .env.example .env
```

### 2) Dev mode (default)

File used: `docker/docker-compose.yml`

This mode is optimized for local development:
- API with `dotnet watch`
- Web with `ng serve`
- source mounts for hot reload

```bash
cd docker

# Start
docker compose up

# Logs
docker compose logs -f

# Stop
docker compose down
```

### Services and ports

| Service | Port | Notes |
|---|---|---|
| PostgreSQL 16 | 5432 | DB `seeddb`, user `seed` |
| API (ASP.NET Core) | 5035 | maps internal port 8080 |
| Web | 4200 | `ng serve` (dev) |
| Seq | 8081 | log UI |
| Seq ingestion | 5341 | Serilog endpoint |

### Main environment variables

| Variable | Dev Default | Description |
|---|---|---|
| `POSTGRES_DB` | `seeddb` | PostgreSQL database name |
| `POSTGRES_USER` | `seed` | PostgreSQL username |
| `POSTGRES_PASSWORD` | `seed_password` | PostgreSQL password |
| `ASPNETCORE_ENVIRONMENT` | `Development` | ASP.NET Core environment |
| `ConnectionStrings__DefaultConnection` | `Host=postgres;Database=seeddb;...` | EF Core connection string |
| `JwtSettings__Secret` | `YourSuperSecret...` | JWT signing key |
| `AllowedHosts` | `*` | Allowed hosts |

## Testing

### Quick test commands (Docker-based)

Use the Docker test runner if you want consistent execution without manual DB setup.

```bash
# Frontend tests only
dotnet run --project docker/TestRunner -- frontend

# Backend unit tests only
dotnet run --project docker/TestRunner -- unit

# Backend integration tests only
dotnet run --project docker/TestRunner -- integration

# All backend tests
dotnet run --project docker/TestRunner -- backend

# Everything (default)
dotnet run --project docker/TestRunner -- all
```

### Local tests (without Docker test runner)

Backend (run from `backend/`):

```bash
# All tests
dotnet test Seed.slnx

# Specific projects
dotnet test tests/Seed.UnitTests
dotnet test tests/Seed.IntegrationTests

# Single test
dotnet test tests/Seed.UnitTests --filter "FullyQualifiedName~MyTestMethod"
```

Frontend (run from `frontend/web/`):

```bash
# All frontend tests
npm test

# Per project
ng test app
ng test shared-auth
ng test shared-core
ng test shared-ui
```

## Run without Docker (optional)

If you prefer running services directly on your machine:

1. Start DB only:

```bash
cd docker
cp .env.example .env
docker compose up postgres
```

2. Start backend:

```bash
cd backend
dotnet run --project src/Seed.Api
```

3. Start frontend:

```bash
cd frontend/web
npm install
npm start
```

## CI/CD

The project uses GitHub Actions with this branch strategy:

- `master` (production) ← `dev` (staging) ← `feature/*`
- `hotfix/*` → direct PR to `master` with automatic back-merge to `dev`

Workflows:
- **CI** - Runs on PRs to `dev` or `master`; path-filters backend/frontend jobs, builds, tests, checks NuGet vulnerabilities, verifies EF migrations, and builds a migration bundle (`ci.yml`)
- **Docker Publish** - Runs on pushes to `dev` or `master`, or manual trigger; builds only changed API/web images unless forced, scans them with Trivy, and pushes to GHCR (`docker-publish.yml`)
- **Deploy** - Runs after successful Docker Publish; deploys `dev` to staging and `master` to production on the VPS via SSH and Docker Compose (`deploy.yml`)
- **Hotfix Back-merge** - Auto PR `master` → `dev` after hotfix (`hotfix-backmerge.yml`)
- **Security scans** - Gitleaks and Semgrep workflows provide additional repository/code scanning

Deploy behavior:
- `PROJECT_SLUG` drives image names and the default deploy root `/opt/<PROJECT_SLUG>`
- `DEPLOY_ROOT` can override the VPS deploy root
- staging deploys to `<DEPLOY_ROOT>/staging`, production deploys to `<DEPLOY_ROOT>/production`
- the workflow syncs compose, nginx, monitoring config and scripts to the VPS on every deploy
- `.env` files are never overwritten by CI and must be created manually once per environment
- deploys use immutable SHA tags (`sha-...` for production, `dev-sha-...` for staging), while `latest`/`dev` remain convenience tags

See [docs/operations/ci-cd.md](docs/operations/ci-cd.md) for full documentation.

## Repository structure

```text
seed-app-ng-dotnet/
|-- backend/          # ASP.NET Core 10 Web API
|   |-- src/
|   |   |-- Seed.Api
|   |   |-- Seed.Application
|   |   |-- Seed.Domain
|   |   |-- Seed.Infrastructure
|   |   `-- Seed.Shared
|   `-- tests/
|       |-- Seed.UnitTests
|       `-- Seed.IntegrationTests
|-- frontend/
|   |-- web/          # Angular 21 app with SSR
|   |   `-- projects/
|   |       |-- app
|   |       |-- shared-ui
|   |       |-- shared-core
|   |       `-- shared-auth
|   `-- mobile/       # .NET MAUI
`-- docker/           # Docker Compose and docker-based test runner
```

## Documentation

Detailed documentation is available in the [`docs/`](docs/) folder:

### Bootstrap A New App

| Document | Description |
|---|---|
| [Using This Seed](docs/getting-started/using-this-seed.md) | Seed-specific handoff: what this repository is, how to adopt it, and what to clean up afterwards |
| [VPS Setup Guide](docs/getting-started/vps-setup-guide.md) | Prepare a blank VPS with SSH, Docker, firewall and deploy root |
| [New Project Deploy Guide](docs/getting-started/new-project-deploy-guide.md) | Operational first deploy guide: app config, Cloudflare, SSL, GitHub Actions and smoke tests |
| [CI/CD](docs/operations/ci-cd.md) | GitHub Actions workflows, branch strategy, Docker publish, deploy |

### Module Setup

| Document | Description |
|---|---|
| [SMTP Configuration](docs/modules/smtp-configuration.md) | Email service setup, provider config (Mailpit, Brevo) |
| [Subscription Payments](docs/modules/subscription-payments.md) | Stripe integration, module toggle, webhook flow, plan guards, troubleshooting |
| [Stripe Payments Setup](docs/modules/stripe-payments-setup.md) | End-to-end setup for enabling and operating the Stripe payments module |
| [In-App Documentation Viewer](docs/modules/in-app-documentation.md) | In-app Markdown documentation viewer, generated manifest, included/excluded docs, rendering flow |
| [Authentication](docs/architecture/authentication.md) | JWT auth, refresh tokens, password reset, Angular integration |
| [Admin Dashboard](docs/modules/admin-dashboard.md) | Admin area: RBAC, user/role management, audit log, settings, system health |
| [Bootstrap Console](docs/architecture/bootstrap-console.md) | Production bootstrap runner: config validation, seeding (roles, permissions, admin user), adding custom seeders |

### Operations

| Document | Description |
|---|---|
| [Migration Strategy](docs/architecture/migration-strategy.md) | EF Core migrations (local + production), rollback, backup procedures |
| [Rollback Guide](docs/operations/rollback.md) | Production rollback strategies: image rollback, git revert, DB restore |
| [Monitoring](docs/operations/monitoring.md) | Monitoring stack: Prometheus, Grafana, cAdvisor, Node Exporter, Portainer, alerting |
| [.env Backup](docs/operations/env-backup.md) | Automated daily .env backup via cron, cleanup, restore procedure |
| [Troubleshooting](docs/operations/troubleshooting.md) | Common issues and solutions not tied to a specific topic |

### Compliance And Readiness

| Document | Description |
|---|---|
| [GDPR Compliance Checklist](docs/compliance/gdpr-compliance-checklist.md) | Post-implementation checklist for GDPR compliance: legal text, DPA, data processing register |

## Tech stack

| Area | Technologies |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, PostgreSQL 16, MediatR 14, FluentValidation 12, Mapster 7, JWT, Serilog |
| Frontend web | Angular 21, Angular Material 21, RxJS 7, Vitest 4, SSR with Express |
| Mobile | .NET MAUI |
| Infra | Docker Compose, PostgreSQL 16, Seq |

## Backend notes

Architecture follows Clean Architecture with 5 projects:
- `Seed.Api`: entry point, controllers, middleware, DI
- `Seed.Application`: use cases (CQRS handlers), validation, mapping
- `Seed.Domain`: domain model and business rules
- `Seed.Infrastructure`: EF Core, Identity, logging sinks
- `Seed.Shared`: shared cross-cutting utilities

### CQRS Pattern

New features are implemented as `IRequest` / `IRequestHandler` pairs in `Seed.Application`. Validators use FluentValidation; DTOs use Mapster with dedicated mapping configs.

### Authentication

JWT Bearer auth configured in `Seed.Api`. ASP.NET Identity for user management in `Seed.Infrastructure`. See [docs/architecture/authentication.md](docs/architecture/authentication.md) for full details.

### API Versioning

Uses `Asp.Versioning.Mvc`. Follow existing conventions when adding new controllers.

Commands (run from `backend/`):

```bash
# Build
dotnet build Seed.slnx

# Run API
dotnet run --project src/Seed.Api

# Add migration
dotnet ef migrations add <MigrationName> \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api

# Apply migrations
dotnet ef database update \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api
```

See [docs/architecture/migration-strategy.md](docs/architecture/migration-strategy.md) for the migration strategy and [docs/modules/smtp-configuration.md](docs/modules/smtp-configuration.md) for email service setup.

## Frontend web notes

Workspace: `frontend/web/` with projects:
- `app`
- `shared-ui`
- `shared-core`
- `shared-auth`

Commands (run from `frontend/web/`):

```bash
# Install dependencies
npm install

# Dev server
npm start

# Production build
npm run build

# Start SSR server (after build)
npm run serve:ssr:app
```

## Frontend mobile

Mobile app lives in `frontend/mobile/` (`Seed.Mobile.csproj`).

## .NET conventions

- `<Nullable>enable</Nullable>` enabled
- `<ImplicitUsings>enable</ImplicitUsings>` enabled
