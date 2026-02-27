# Seed App — Angular + .NET Full Stack

A ready-to-use full-stack seed/starter application, designed as a starting point for new projects. Includes an ASP.NET Core backend, an Angular frontend with SSR, a .NET MAUI mobile app, and Docker infrastructure.

---

## Repository Structure

```
seed-app-ng-dotnet/
├── backend/          # ASP.NET Core 10 Web API
│   ├── src/
│   │   ├── Seed.Api
│   │   ├── Seed.Application
│   │   ├── Seed.Domain
│   │   ├── Seed.Infrastructure
│   │   └── Seed.Shared
│   └── tests/
│       ├── Seed.UnitTests
│       └── Seed.IntegrationTests
├── frontend/
│   ├── web/          # Angular 21 SPA with SSR
│   │   └── projects/
│   │       ├── app
│   │       ├── shared-ui
│   │       ├── shared-core
│   │       └── shared-auth
│   └── mobile/       # .NET MAUI
├── docker/           # Docker Compose for local infrastructure
└── scripts/
```

---

## Tech Stack

| Area | Technologies |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, PostgreSQL 16, MediatR 14, FluentValidation 12, Mapster 7, JWT, Serilog |
| Frontend web | Angular 21, Angular Material 21, RxJS 7, Vitest 4, SSR with Express |
| Mobile | .NET MAUI |
| Infrastructure | Docker Compose, PostgreSQL 16, Seq (log aggregation) |

---

## Backend

### Architecture

Clean Architecture split into 5 .NET projects:

- **`Seed.Api`** — Entry point. ASP.NET Core controllers, middleware, DI configuration. Depends on Application, Infrastructure, and Shared.
- **`Seed.Application`** — Application logic. MediatR handlers (CQRS), FluentValidation validators, DTO mapping with Mapster. Depends on Domain and Shared.
- **`Seed.Domain`** — Domain entities and pure business logic. No external dependencies.
- **`Seed.Infrastructure`** — Data access. EF Core with Npgsql, ASP.NET Identity, Serilog sinks. Depends on Application.
- **`Seed.Shared`** — Cross-cutting utilities shared between Application and Api.

### CQRS Pattern

New features are implemented as `IRequest` / `IRequestHandler` pairs in `Seed.Application`. Validators use FluentValidation; DTOs use Mapster with dedicated mapping configs.

### Authentication

JWT Bearer auth configured in `Seed.Api`. ASP.NET Identity for user management in `Seed.Infrastructure`.

### API Versioning

Uses `Asp.Versioning.Mvc`. Follow existing conventions when adding new controllers.

### Tests

- **`Seed.UnitTests`** — xUnit + NSubstitute + FluentAssertions. Covers Application and Domain.
- **`Seed.IntegrationTests`** — xUnit + Testcontainers (PostgreSQL) + `Microsoft.AspNetCore.Mvc.Testing`. Tests the full Api + Infrastructure stack.

### Commands (run from `backend/`)

```bash
# Build
dotnet build Seed.slnx

# Run the API
dotnet run --project src/Seed.Api

# All tests
dotnet test Seed.slnx

# Specific test project
dotnet test tests/Seed.UnitTests
dotnet test tests/Seed.IntegrationTests

# Single test
dotnet test tests/Seed.UnitTests --filter "FullyQualifiedName~MyTestMethod"

# EF Core — add migration
dotnet ef migrations add <MigrationName> \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api

# EF Core — apply migrations to database
dotnet ef database update \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api
```

---

## Frontend Web (Angular)

Angular workspace at `frontend/web/` with 4 projects:

- **`app`** (`projects/app/`) — Main application. SSR enabled with Express.
- **`shared-ui`** — Reusable UI component library (`lib` prefix).
- **`shared-core`** — Core utilities and services library.
- **`shared-auth`** — Authentication library.

The `shared-*` libraries are compiled with `ng-packagr`. In CI they must be built before the app. New shared functionality should be exported from the appropriate library's `public-api.ts`.

### State and Reactivity

Prefer **Angular signals** (`signal()`) over Observables for local component state.

### Commands (run from `frontend/web/`)

```bash
# Install dependencies
npm install

# Dev server — http://localhost:4200
npm start

# Production build
npm run build

# All tests
npm test

# Tests for a specific project
ng test app
ng test shared-auth
ng test shared-core
ng test shared-ui

# Start SSR server (after build)
npm run serve:ssr:app

# Scaffold component (main app)
ng generate component projects/app/src/app/<name>

# Scaffold component (UI library)
ng generate component projects/shared-ui/src/lib/<name>
```

---

## Frontend Mobile (.NET MAUI)

Project at `frontend/mobile/` (`Seed.Mobile.csproj`). A cross-platform .NET MAUI app as a starting point for native mobile clients.

---

## Docker Infrastructure

Docker Compose in `docker/` that starts the full local stack:

| Service | Port | Notes |
|---|---|---|
| PostgreSQL 16 | 5432 | DB `seeddb`, user `seed` |
| API (ASP.NET Core) | 5000 | Maps internal port 8080 |
| Web (Angular SSR) | 4200 | Nginx |

```bash
# Start the full stack
docker compose up

# Database only
docker compose up postgres
```

### Database connection (local development)

```
Host=localhost;Database=seeddb;Username=seed;Password=seed_password
```

---

## Quick Start (local development without Docker)

1. **Database** — start only PostgreSQL with Docker:
   ```bash
   cd docker
   docker compose up postgres
   ```

2. **Backend** — start the API:
   ```bash
   cd backend
   dotnet run --project src/Seed.Api
   ```

3. **Frontend** — start the Angular dev server:
   ```bash
   cd frontend/web
   npm install
   npm start
   ```

The app will be available at `http://localhost:4200`, the API at `http://localhost:5000`.

---

## .NET Conventions

- `<Nullable>enable</Nullable>` enabled in all projects.
- `<ImplicitUsings>enable</ImplicitUsings>` enabled in all projects.
