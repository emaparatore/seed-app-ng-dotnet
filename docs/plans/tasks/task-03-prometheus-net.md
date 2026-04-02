# Task 3: Integrare prometheus-net nell'API .NET

## Contesto

- L'API ASP.NET Core 10 è in `backend/src/Seed.Api/`
- `Program.cs` configura middleware in ordine: ExceptionHandler → SerilogRequestLogging → HttpsRedirection → Cors → RateLimiter → Authentication → Authorization → MustChangePassword → MapControllers → MapHealthChecks
- L'API in produzione ascolta su porta 8080 (confermato da healthcheck in `docker-compose.deploy.yml`: `curl -f http://localhost:8080/health/ready`)
- `Seed.Api.csproj` usa .NET 10 (`net10.0`), nessun pacchetto prometheus-net presente
- L'endpoint `/metrics` deve essere accessibile solo dalla rete interna Docker (`app-network`), non esposto pubblicamente — Prometheus farà scrape da `api:8080/metrics`

## Piano di esecuzione

### Step 1: Aggiungere pacchetto NuGet

**File:** `backend/src/Seed.Api/Seed.Api.csproj`

- Aggiungere `<PackageReference Include="prometheus-net.AspNetCore" Version="8.*" />` (ultima versione stabile per .NET 10)
- Verificare la versione esatta con `dotnet add package` per prendere l'ultima compatibile

### Step 2: Configurare middleware e endpoint in Program.cs

**File:** `backend/src/Seed.Api/Program.cs`

1. Aggiungere `using Prometheus;` in cima al file
2. Aggiungere `app.UseHttpMetrics();` dopo `app.UseAuthorization();` — cattura metriche HTTP automatiche (request duration, count per status code, method, endpoint)
3. Aggiungere `app.MapMetrics();` dopo `app.MapHealthChecks(...)` — espone endpoint `/metrics` in formato Prometheus text

Posizionamento `UseHttpMetrics()`:
- Deve essere DOPO `UseRouting()` (implicito) e DOPO `UseAuthorization()` per avere info complete su endpoint e status code
- Deve essere PRIMA di `MapControllers()` per intercettare tutte le request

Posizionamento `MapMetrics()`:
- Alla fine, vicino agli altri endpoint mapped (health checks)

### Step 3: Build e verifica

```bash
cd backend && dotnet build Seed.slnx
```

### Step 4: Run test suite

```bash
cd backend && dotnet test Seed.slnx
```

Verificare che i test esistenti non siano rotti dall'aggiunta del middleware.

## Criteri di completamento

- [ ] Pacchetto `prometheus-net.AspNetCore` aggiunto a `Seed.Api.csproj`
- [ ] `app.UseHttpMetrics()` configurato in `Program.cs` nel punto corretto della pipeline
- [ ] `app.MapMetrics()` configurato in `Program.cs` per esporre `/metrics`
- [ ] Il progetto compila senza errori (`dotnet build Seed.slnx`)
- [ ] Tutti i test passano (`dotnet test Seed.slnx`)
- [ ] Nessuna modifica a file non elencati nel piano

## Risultato

- File modificati:
  - `backend/src/Seed.Api/Seed.Api.csproj` — aggiunto PackageReference `prometheus-net.AspNetCore` versione 8.2.1
  - `backend/src/Seed.Api/Program.cs` — aggiunto `using Prometheus;`, `app.UseHttpMetrics()` dopo `UseMiddleware<MustChangePasswordMiddleware>()`, `app.MapMetrics()` dopo gli endpoint health checks
- Scelte implementative e motivazioni:
  - Versione 8.2.1 di prometheus-net.AspNetCore installata tramite `dotnet add package` (ultima stabile compatibile con .NET 10)
  - `UseHttpMetrics()` posizionato dopo `UseAuthorization()` e `MustChangePasswordMiddleware` ma prima di `MapControllers()` per catturare metriche HTTP complete (status code, method, endpoint)
  - `MapMetrics()` posizionato alla fine, dopo tutti gli health checks, coerente con il pattern di raggruppamento degli endpoint mapped
- Deviazioni dal piano: nessuna
