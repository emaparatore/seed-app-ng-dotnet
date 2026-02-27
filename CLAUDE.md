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

**Key packages:** MediatR 14, FluentValidation 12, Mapster 7, EF Core 10, Npgsql EF 10, Asp.Versioning, JWT Bearer auth, Serilog (Console + Seq sinks), Swashbuckle/OpenAPI.

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

## Development Patterns

- **Backend CQRS:** New features go as MediatR `IRequest`/`IRequestHandler` pairs in `Seed.Application`. Validators use FluentValidation. DTOs use Mapster mapping configs.
- **API versioning:** Uses `Asp.Versioning.Mvc` — follow existing versioning conventions when adding new controllers.
- **Angular libraries:** When adding shared functionality, export it from the appropriate library's `public-api.ts`. Build libraries before the app in CI.
- **Angular signals:** The app uses Angular signals (`signal()`) — prefer signals over observables for local component state.
- **Nullable references:** All .NET projects have `<Nullable>enable</Nullable>` and `<ImplicitUsings>enable</ImplicitUsings>`.
