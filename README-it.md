# Seed App — Angular + .NET Full Stack

Applicazione seed/starter full-stack pronta all'uso, pensata come punto di partenza per nuovi progetti. Include backend ASP.NET Core, frontend Angular con SSR, app mobile .NET MAUI e infrastruttura Docker.

---

## Struttura del repository

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
│   ├── web/          # Angular 21 SPA con SSR
│   │   └── projects/
│   │       ├── app
│   │       ├── shared-ui
│   │       ├── shared-core
│   │       └── shared-auth
│   └── mobile/       # .NET MAUI
├── docker/           # Docker Compose per l'infrastruttura locale
└── scripts/
```

---

## Stack tecnologico

| Area | Tecnologie |
|---|---|
| Backend | ASP.NET Core 10, EF Core 10, PostgreSQL 16, MediatR 14, FluentValidation 12, Mapster 7, JWT, Serilog |
| Frontend web | Angular 21, Angular Material 21, RxJS 7, Vitest 4, SSR con Express |
| Mobile | .NET MAUI |
| Infrastruttura | Docker Compose, PostgreSQL 16, Seq (log aggregation) |

---

## Backend

### Architettura

Clean Architecture divisa in 5 progetti .NET:

- **`Seed.Api`** — Entry point. Controller ASP.NET Core, middleware, configurazione DI. Dipende da Application, Infrastructure e Shared.
- **`Seed.Application`** — Logica applicativa. Handler MediatR (CQRS), validatori FluentValidation, mapping DTO con Mapster. Dipende da Domain e Shared.
- **`Seed.Domain`** — Entità di dominio e business logic pura. Nessuna dipendenza esterna.
- **`Seed.Infrastructure`** — Accesso ai dati. EF Core con Npgsql, ASP.NET Identity, Serilog sinks. Dipende da Application.
- **`Seed.Shared`** — Utilities trasversali condivise tra Application e Api.

### Pattern CQRS

Le nuove funzionalità si implementano come coppie `IRequest` / `IRequestHandler` in `Seed.Application`. I validatori usano FluentValidation; i DTO usano Mapster con mapping config dedicati.

### Autenticazione

JWT Bearer auth configurata in `Seed.Api`. ASP.NET Identity per la gestione utenti in `Seed.Infrastructure`.

### Versioning API

Usa `Asp.Versioning.Mvc`. Seguire le convenzioni esistenti quando si aggiungono nuovi controller.

### Test

- **`Seed.UnitTests`** — xUnit + NSubstitute + FluentAssertions. Copre Application e Domain.
- **`Seed.IntegrationTests`** — xUnit + Testcontainers (PostgreSQL) + `Microsoft.AspNetCore.Mvc.Testing`. Testa l'intero stack Api + Infrastructure.

### Comandi (eseguire da `backend/`)

```bash
# Build
dotnet build Seed.slnx

# Avvia l'API
dotnet run --project src/Seed.Api

# Tutti i test
dotnet test Seed.slnx

# Progetto di test specifico
dotnet test tests/Seed.UnitTests
dotnet test tests/Seed.IntegrationTests

# Test singolo
dotnet test tests/Seed.UnitTests --filter "FullyQualifiedName~MyTestMethod"

# EF Core — aggiungi migrazione
dotnet ef migrations add <NomeMigrazione> \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api

# EF Core — applica migrazioni al database
dotnet ef database update \
  --project src/Seed.Infrastructure \
  --startup-project src/Seed.Api
```

---

## Frontend Web (Angular)

Workspace Angular in `frontend/web/` con 4 progetti:

- **`app`** (`projects/app/`) — Applicazione principale. SSR abilitato con Express.
- **`shared-ui`** — Libreria di componenti UI riusabili (prefisso `lib`).
- **`shared-core`** — Libreria di utilities e servizi core.
- **`shared-auth`** — Libreria per l'autenticazione.

Le librerie `shared-*` vengono compilate con `ng-packagr`. In CI devono essere compilate prima dell'app. Le nuove funzionalità condivise vanno esportate dal `public-api.ts` della libreria appropriata.

### Stato e reattività

Preferire i **signal Angular** (`signal()`) rispetto agli Observable per lo stato locale dei componenti.

### Comandi (eseguire da `frontend/web/`)

```bash
# Installa dipendenze
npm install

# Dev server — http://localhost:4200
npm start

# Build produzione
npm run build

# Tutti i test
npm test

# Test per progetto specifico
ng test app
ng test shared-auth
ng test shared-core
ng test shared-ui

# Avvia il server SSR (dopo il build)
npm run serve:ssr:app

# Scaffold componente (app principale)
ng generate component projects/app/src/app/<nome>

# Scaffold componente (libreria UI)
ng generate component projects/shared-ui/src/lib/<nome>
```

---

## Frontend Mobile (.NET MAUI)

Progetto in `frontend/mobile/` (`Seed.Mobile.csproj`). App cross-platform .NET MAUI come punto di partenza per client mobile nativi.

---

## Infrastruttura Docker

Docker Compose in `docker/` che avvia l'intero stack locale:

| Servizio | Porta | Note |
|---|---|---|
| PostgreSQL 16 | 5432 | DB `seeddb`, user `seed` |
| API (ASP.NET Core) | 5000 | Mappa la porta interna 8080 |
| Web (Angular SSR) | 4200 | Nginx |

```bash
# Avvia tutto lo stack
docker compose up

# Solo il database
docker compose up postgres
```

### Connessione al database (sviluppo locale)

```
Host=localhost;Database=seeddb;Username=seed;Password=seed_password
```

---

## Avvio rapido (sviluppo locale senza Docker)

1. **Database** — avviare solo PostgreSQL con Docker:
   ```bash
   cd docker
   docker compose up postgres
   ```

2. **Backend** — avviare l'API:
   ```bash
   cd backend
   dotnet run --project src/Seed.Api
   ```

3. **Frontend** — avviare il dev server Angular:
   ```bash
   cd frontend/web
   npm install
   npm start
   ```

L'app sarà disponibile su `http://localhost:4200`, l'API su `http://localhost:5000`.

---

## Convenzioni .NET

- `<Nullable>enable</Nullable>` abilitato in tutti i progetti.
- `<ImplicitUsings>enable</ImplicitUsings>` abilitato in tutti i progetti.
