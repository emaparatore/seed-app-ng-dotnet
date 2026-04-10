# Task 07: Public plans API — list available plans

## Contesto

### Stato attuale del codice rilevante
- **Domain entities** gia' esistenti: `SubscriptionPlan` (`backend/src/Seed.Domain/Entities/SubscriptionPlan.cs`) e `PlanFeature` (`backend/src/Seed.Domain/Entities/PlanFeature.cs`) con tutte le proprieta' necessarie (Name, Description, MonthlyPrice, YearlyPrice, TrialDays, IsFreeTier, IsDefault, IsPopular, Status, SortOrder, Features collection)
- **EF Core configurations** gia' esistenti: `SubscriptionPlanConfiguration` e `PlanFeatureConfiguration` in `backend/src/Seed.Infrastructure/Persistence/Configurations/`
- **DbSets** gia' registrati in `ApplicationDbContext`: `SubscriptionPlans`, `PlanFeatures`
- **Enum** `PlanStatus` (Active, Inactive, Archived) in `backend/src/Seed.Domain/Enums/PlanStatus.cs`
- **Module toggle** funzionante: `IsPaymentsModuleEnabled()` in `Seed.Shared/Extensions/ConfigurationExtensions.cs`
- Non esiste ancora nessun controller o query handler per i piani pubblici

### Dipendenze e vincoli
- Dipende da T-02 (domain entities) e T-03 (EF Core config + migration) — entrambi completati
- Endpoint pubblico (AllowAnonymous) — nessuna autenticazione richiesta
- Il controller deve funzionare solo quando il payments module e' abilitato (conditional DI registration pattern)
- Seguire i pattern esistenti: `Result<T>` wrapper, sealed record DTOs, MediatR CQRS

## Piano di esecuzione

### Step 1: Creare i DTOs
**File da creare:**
- `backend/src/Seed.Application/Billing/Models/PlanDto.cs`
- `backend/src/Seed.Application/Billing/Models/PlanFeatureDto.cs`

**Approccio:**
```csharp
// PlanFeatureDto.cs
public sealed record PlanFeatureDto(
    Guid Id, string Key, string Description, string? LimitValue, int SortOrder);

// PlanDto.cs
public sealed record PlanDto(
    Guid Id, string Name, string? Description,
    decimal MonthlyPrice, decimal YearlyPrice,
    int TrialDays, bool IsFreeTier, bool IsDefault, bool IsPopular,
    int SortOrder, IReadOnlyList<PlanFeatureDto> Features);
```

### Step 2: Creare la Query e il QueryHandler
**File da creare:**
- `backend/src/Seed.Application/Billing/Queries/GetPlans/GetPlansQuery.cs`
- `backend/src/Seed.Application/Billing/Queries/GetPlans/GetPlansQueryHandler.cs`

**Approccio:**
- `GetPlansQuery` — sealed record che implementa `IRequest<Result<IReadOnlyList<PlanDto>>>`
- `GetPlansQueryHandler` — inietta `ApplicationDbContext`, filtra per `Status == PlanStatus.Active`, include `Features`, ordina per `SortOrder`, mappa a `PlanDto`
- Usare proiezione LINQ diretta (Select) per evitare dipendenza da Mapster (pattern coerente con query semplici)
- MediatR auto-registra l'handler dall'assembly scan in `AddApplication()`

### Step 3: Creare il PlansController
**File da creare:**
- `backend/src/Seed.Api/Controllers/PlansController.cs`

**Approccio:**
```csharp
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/plans")]
[AllowAnonymous]
public class PlansController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPlans(CancellationToken ct)
    {
        var result = await sender.Send(new GetPlansQuery(), ct);
        return result.Succeeded ? Ok(result.Data) : BadRequest(result.Errors);
    }
}
```
- `[AllowAnonymous]` esplicito per chiarezza
- Il controller viene scoperto automaticamente da MVC assembly scan
- Funziona solo se i servizi del payments module sono registrati (dependency injection condizionale)

### Step 4: Unit test per il QueryHandler
**File da creare:**
- `backend/tests/Seed.UnitTests/Billing/Queries/GetPlansQueryHandlerTests.cs`

**Test cases:**
1. `Should_Return_Only_Active_Plans` — inserire piani con Status Active, Inactive, Archived; verificare che solo Active vengano restituiti
2. `Should_Return_Plans_Ordered_By_SortOrder` — inserire piani con SortOrder diversi; verificare ordine corretto
3. `Should_Include_Features_In_Response` — inserire piano con features; verificare che features siano incluse nel DTO
4. `Should_Return_Empty_List_When_No_Active_Plans` — nessun piano attivo; verificare lista vuota (non errore)

**Approccio:** Usare InMemory database provider per `ApplicationDbContext` (pattern gia' usato nei test del webhook handler)

### Step 5: Integration test per l'endpoint
**File da creare:**
- `backend/tests/Seed.IntegrationTests/Billing/PlansControllerTests.cs`

**Test cases:**
1. `GetPlans_ReturnsActivePlans_WhenPaymentsModuleEnabled` — seed dati, GET /api/v1.0/plans, verificare 200 OK con piani attivi
2. `GetPlans_ReturnsEmptyList_WhenNoPlans` — nessun piano, verificare 200 OK con lista vuota

**Approccio:** Usare `WebApplicationFactory` custom con payments module abilitato (riutilizzare o estendere `WebhookWebApplicationFactory` pattern). Seed dati direttamente nel test via scope DbContext.

### Step 6: Verificare build e test
```bash
cd backend && dotnet build Seed.slnx && dotnet test Seed.slnx
```

## Criteri di completamento
- [ ] `PlanDto` e `PlanFeatureDto` creati come sealed records in `Seed.Application/Billing/Models/`
- [ ] `GetPlansQuery` + `GetPlansQueryHandler` creati in `Seed.Application/Billing/Queries/GetPlans/`
- [ ] Handler filtra per Status == Active, include Features, ordina per SortOrder
- [ ] `PlansController` con `[AllowAnonymous] GET /api/v1.0/plans` creato
- [ ] 4 unit test per l'handler passano
- [ ] 2 integration test per l'endpoint passano
- [ ] `dotnet build Seed.slnx` compila senza errori
- [ ] `dotnet test Seed.slnx` passa tutti i test (esistenti + nuovi)

## Risultato

### File creati
- `backend/src/Seed.Application/Billing/Models/PlanDto.cs` — sealed record DTO per il piano
- `backend/src/Seed.Application/Billing/Models/PlanFeatureDto.cs` — sealed record DTO per le feature del piano
- `backend/src/Seed.Application/Billing/Queries/GetPlans/GetPlansQuery.cs` — query MediatR (contratto)
- `backend/src/Seed.Infrastructure/Billing/Queries/GetPlansQueryHandler.cs` — handler MediatR con accesso a DbContext
- `backend/src/Seed.Api/Controllers/PlansController.cs` — controller con `[AllowAnonymous] GET /api/v1.0/plans`
- `backend/tests/Seed.UnitTests/Billing/Queries/GetPlansQueryHandlerTests.cs` — 4 unit test
- `backend/tests/Seed.IntegrationTests/Billing/PlansControllerTests.cs` — 2 integration test

### File modificati
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione manuale del `GetPlansQueryHandler` nel blocco payments module

### Scelte implementative e motivazioni
- **Handler in Infrastructure invece che in Application:** il piano originale prevedeva l'handler in `Seed.Application`, ma Application non ha un riferimento a Infrastructure (e quindi a `ApplicationDbContext`). Seguendo il pattern gia' usato da `StripeWebhookEventHandler`, l'handler e' stato posizionato in `Seed.Infrastructure/Billing/Queries/`. Il contratto (Query + DTOs) resta in Application.
- **Registrazione manuale del handler in DI:** poiche' MediatR scansiona solo l'assembly Application, l'handler in Infrastructure e' registrato manualmente con `services.AddScoped<IRequestHandler<...>, ...>()` dentro il blocco `if (configuration.IsPaymentsModuleEnabled())`. Questo garantisce che l'handler sia disponibile solo quando il modulo pagamenti e' abilitato.
- **Proiezione LINQ diretta (Select):** usata per evitare dipendenza da Mapster, coerente con query semplici.
- **Integration test riusa `WebhookWebApplicationFactory`:** gia' configura il payments module come abilitato, evitando duplicazione.

### Deviazioni dal piano
- Il `GetPlansQueryHandler` e' in `Seed.Infrastructure/Billing/Queries/` invece di `Seed.Application/Billing/Queries/GetPlans/` per rispettare i vincoli architetturali del progetto (Application non referenzia Infrastructure).
