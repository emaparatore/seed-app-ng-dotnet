# Seed App - Angular + .NET Full Stack
A ready-to-use full-stack seed/starter application, designed as a starting point for new projects. Includes: 
- ASP.NET Core backend
- Angular web frontend (with SSR support)
- .NET MAUI mobile app
- Docker setup for local development and test execution

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

Run all tests with Docker only:

```bash
dotnet run --project docker/TestRunner -- all
```

## Prerequisites

- Docker Desktop (or Docker Engine + Compose)
- .NET SDK 10 (only needed for local `dotnet` commands and `docker/TestRunner`)
- Node.js 22 (only needed if you run frontend outside Docker)

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

The project uses GitHub Actions with a branch strategy:

- `master` (production) ← `dev` (staging) ← `feature/*`
- `hotfix/*` → direct PR to `master` with automatic back-merge to `dev`

Workflows:
- **CI** - Build and test on every PR (`ci.yml`)
- **Docker Publish** - Build and push images to ghcr.io on merge (`docker-publish.yml`)
- **Deploy** - Deploy to VPS via SSH with Docker Compose (`deploy.yml`)
- **Hotfix Back-merge** - Auto PR `master` → `dev` after hotfix (`hotfix-backmerge.yml`)

See [docs/ci-cd.md](docs/ci-cd.md) for full documentation.

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

| Document | Description |
|---|---|
| [Authentication](docs/authentication.md) | JWT auth, refresh tokens, password reset, Angular integration |
| [Bootstrap Console](docs/bootstrap-console.md) | Production bootstrap runner: config validation, seeding (roles, permissions, admin user), adding custom seeders |
| [CI/CD](docs/ci-cd.md) | GitHub Actions workflows, branch strategy, Docker publish, deploy |
| [Migration Strategy](docs/migration-strategy.md) | EF Core migrations (local + production), rollback, backup procedures |
| [SMTP Configuration](docs/smtp-configuration.md) | Email service setup, provider config (Gmail, Brevo) |
| [VPS Setup Guide](docs/vps-setup-guide.md) | Full VPS deployment with Docker, Nginx, Cloudflare, SSL |
| [New Project Deploy Guide](docs/new-project-deploy-guide.md) | Checklist for deploying new projects based on this seed |
| [Admin Dashboard](docs/admin-dashboard.md) | Admin area: RBAC, user/role management, audit log, settings, system health |
| [Troubleshooting](docs/troubleshooting.md) | Common issues and solutions not tied to a specific topic |

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

JWT Bearer auth configured in `Seed.Api`. ASP.NET Identity for user management in `Seed.Infrastructure`. See [docs/authentication.md](docs/authentication.md) for full details.

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

See [docs/migration-strategy.md](docs/migration-strategy.md) for the migration strategy and [docs/smtp-configuration.md](docs/smtp-configuration.md) for email service setup.

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
