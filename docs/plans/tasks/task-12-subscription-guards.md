# Task 12: Subscription guards — backend authorization

## Contesto ereditato dal piano

### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-010 | Subscription guard su endpoint | T-12 | ⏳ Not Started |

Requisito di riferimento (da FEAT-3.md, RF-6):

> Il sistema deve fornire un meccanismo di autorizzazione basato sul piano dell'utente:
> - Attributo/policy `[RequiresPlan("Pro")]` per proteggere endpoint specifici
> - Attributo/policy `[RequiresFeature("api-access")]` per proteggere in base a feature del piano
> - Middleware per verificare che la subscription sia attiva (non scaduta, non canceled)
> - Quando il modulo Payments è disabilitato, i guard passano sempre

### Dipendenze (da 'Depends on:')

T-03: EF Core configuration and migration

**Implementation Notes (T-03 verbatim):**
- Followed existing `RefreshTokenConfiguration` pattern for structure and style (sealed class, file-scoped namespace)
- Enum properties converted to string in DB via `HasConversion<string>()` for readability
- Unique index on `StripeSubscriptionId` uses `HasFilter("\"StripeSubscriptionId\" IS NOT NULL")` to handle nullable correctly in PostgreSQL
- `DeleteBehavior.Restrict` on `SubscriptionPlan → UserSubscription` to prevent deletion of plans with active subscriptions
- `DeleteBehavior.Cascade` on `User → UserSubscription` and `User → InvoiceRequest`, consistent with existing RefreshToken pattern

### Convenzioni da task Done correlati

**Da T-01 (Module toggle system):**
- Extension method `IsPaymentsModuleEnabled` lives in `Seed.Shared/Extensions/ConfigurationExtensions.cs`
- `ModulesSettings` is registered unconditionally to allow injection even when the payments module is disabled

**Da T-02 (Domain entities):**
- Created 5 enums in `Seed.Domain/Enums/`: PlanStatus, SubscriptionStatus, CustomerType, InvoiceRequestStatus, BillingInterval
- `SubscriptionStatus` enum has: Active, Trialing, PastDue, Canceled, Expired
- Navigation properties to parent entities initialized with `= null!` (EF Core recommended pattern)

**Da T-07 (Public plans API):**
- Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application.
- Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block, since MediatR only scans the Application assembly.

**Da T-09 (Subscription management):**
- All 3 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
- Unit tests use InMemory DB for `ApplicationDbContext` and NSubstitute for `IPaymentGateway`/`IAuditService`, following the pattern established in T-08

### Riferimenti

- `docs/requirements/FEAT-3.md` — sezione RF-6 (Subscription guards backend)
- `docs/requirements/FEAT-3.md` — sezione DA-1 (Modulo attivabile via configurazione: "Quando disabilitato... I subscription guards backend passano sempre")

## Stato attuale del codice

### Pattern di autorizzazione esistente (permission-based)

Il progetto ha già un sistema di autorizzazione custom basato su permission claims:

- **`backend/src/Seed.Api/Authorization/HasPermissionAttribute.cs`** — Attributo `[HasPermission("permission")]` che estende `AuthorizeAttribute` con policy name `Permission:{permission}`
- **`backend/src/Seed.Api/Authorization/PermissionRequirement.cs`** — `IAuthorizationRequirement` con proprietà `Permission`
- **`backend/src/Seed.Api/Authorization/PermissionAuthorizationPolicyProvider.cs`** — `DefaultAuthorizationPolicyProvider` custom che intercetta policy con prefisso `Permission:` e crea `AuthorizationPolicy` con `PermissionRequirement`
- **`backend/src/Seed.Api/Authorization/PermissionAuthorizationHandler.cs`** — `AuthorizationHandler<PermissionRequirement>` che verifica le permission dell'utente, con bypass per SuperAdmin

I due servizi sono registrati in `backend/src/Seed.Api/Program.cs` (righe 82-83):
```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

### Entità domain rilevanti

- **`backend/src/Seed.Domain/Entities/UserSubscription.cs`** — Id, UserId, PlanId, Status (SubscriptionStatus enum), StripeSubscriptionId, StripeCustomerId, CurrentPeriodStart/End, TrialEnd, CanceledAt, navigation props: User, Plan
- **`backend/src/Seed.Domain/Entities/SubscriptionPlan.cs`** — Id, Name, Description, prices, Stripe IDs, TrialDays, IsFreeTier, IsDefault, IsPopular, Status (PlanStatus), Features (ICollection<PlanFeature>), Subscriptions
- **`backend/src/Seed.Domain/Entities/PlanFeature.cs`** — Id, PlanId, Key (string), Description, LimitValue, SortOrder
- **`backend/src/Seed.Domain/Enums/SubscriptionStatus.cs`** — Active, Trialing, PastDue, Canceled, Expired

### Module toggle

- **`backend/src/Seed.Shared/Extensions/ConfigurationExtensions.cs`** — `IsPaymentsModuleEnabled()` extension method
- **`backend/src/Seed.Shared/Configuration/ModulesSettings.cs`** — `ModulesSettings` con `Payments` property
- DI conditional block in `backend/src/Seed.Infrastructure/DependencyInjection.cs` (riga 78): `if (configuration.IsPaymentsModuleEnabled()) { ... }`

### DbContext

- **`backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs`** — ha DbSet per UserSubscription, SubscriptionPlan, PlanFeature

## Piano di esecuzione

### Approccio architetturale

Seguire lo stesso pattern dell'autorizzazione permission-based già presente (attribute → requirement → policy provider → handler), adattandolo per plan/feature checks. L'`ISubscriptionAccessService` nel layer Application incapsula la query DB, rendendo gli handler testabili.

### Step 1: Creare `ISubscriptionAccessService` in Application layer

**File:** `backend/src/Seed.Application/Common/Interfaces/ISubscriptionAccessService.cs`

Interfaccia con metodi:
```csharp
Task<bool> UserHasActivePlanAsync(Guid userId, string[] planNames, CancellationToken ct = default);
Task<bool> UserHasFeatureAsync(Guid userId, string featureKey, CancellationToken ct = default);
```

Questi metodi verificano che l'utente abbia una subscription con Status = Active o Trialing, e che il piano o la feature corrispondano.

### Step 2: Implementare `SubscriptionAccessService` in Infrastructure

**File:** `backend/src/Seed.Infrastructure/Billing/Services/SubscriptionAccessService.cs`

- Inietta `ApplicationDbContext`
- `UserHasActivePlanAsync`: query UserSubscription con Status in (Active, Trialing), join con Plan, verifica Plan.Name in planNames
- `UserHasFeatureAsync`: query UserSubscription con Status in (Active, Trialing), join con Plan → Features, verifica Feature.Key == featureKey
- Usa `AnyAsync` per efficienza

### Step 3: Creare requirements e attributi in Seed.Api

**File:** `backend/src/Seed.Api/Authorization/RequiresPlanAttribute.cs`
- Attributo `[RequiresPlan("Pro", "Enterprise")]` che estende `AuthorizeAttribute`
- Policy name: `Plan:{planName1},{planName2}`

**File:** `backend/src/Seed.Api/Authorization/RequiresFeatureAttribute.cs`
- Attributo `[RequiresFeature("api-access")]` che estende `AuthorizeAttribute`
- Policy name: `Feature:{featureKey}`

**File:** `backend/src/Seed.Api/Authorization/PlanRequirement.cs`
- `IAuthorizationRequirement` con `string[] PlanNames`

**File:** `backend/src/Seed.Api/Authorization/FeatureRequirement.cs`
- `IAuthorizationRequirement` con `string FeatureKey`

### Step 4: Creare authorization handlers

**File:** `backend/src/Seed.Api/Authorization/RequiresPlanAuthorizationHandler.cs`
- `AuthorizationHandler<PlanRequirement>`
- Se `ModulesSettings.Payments.Enabled == false` → `context.Succeed()` (pass always)
- Altrimenti chiama `ISubscriptionAccessService.UserHasActivePlanAsync()`
- Se true → `context.Succeed()`, altrimenti lascia fallire (HTTP 403)

**File:** `backend/src/Seed.Api/Authorization/RequiresFeatureAuthorizationHandler.cs`
- `AuthorizationHandler<FeatureRequirement>`
- Se `ModulesSettings.Payments.Enabled == false` → `context.Succeed()` (pass always)
- Altrimenti chiama `ISubscriptionAccessService.UserHasFeatureAsync()`
- Se true → `context.Succeed()`

Entrambi gli handler iniettano `IOptions<ModulesSettings>` per il check del module toggle e `ISubscriptionAccessService` per il check DB.

### Step 5: Aggiornare PermissionAuthorizationPolicyProvider

**File:** `backend/src/Seed.Api/Authorization/PermissionAuthorizationPolicyProvider.cs`

Aggiungere gestione dei prefissi `Plan:` e `Feature:` oltre all'esistente `Permission:`:
- `Plan:Pro,Enterprise` → `PlanRequirement(["Pro", "Enterprise"])`
- `Feature:api-access` → `FeatureRequirement("api-access")`

### Step 6: Registrare in DI

**File:** `backend/src/Seed.Api/Program.cs`

Aggiungere dopo la riga 83:
```csharp
builder.Services.AddScoped<IAuthorizationHandler, RequiresPlanAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, RequiresFeatureAuthorizationHandler>();
```

Nota: questi handler vanno registrati SEMPRE (non solo quando payments module è abilitato), perché il check "module disabled → pass" è interno all'handler.

**File:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`

Registrare `ISubscriptionAccessService`:
- FUORI dal blocco `IsPaymentsModuleEnabled()` con un'implementazione che ritorna sempre `true` quando il modulo è disabilitato, OPPURE
- DENTRO il blocco con l'implementazione reale, e registrare un `NullSubscriptionAccessService` (che ritorna sempre true) fuori dal blocco come fallback

Approccio preferito: registrare `SubscriptionAccessService` dentro il blocco `IsPaymentsModuleEnabled()`, e un `AlwaysAllowSubscriptionAccessService` fuori (come else). Questo mantiene il pattern del module toggle consistente.

### Step 7: Scrivere unit tests

**File:** `backend/tests/Seed.UnitTests/Authorization/RequiresPlanAuthorizationHandlerTests.cs`

Test cases:
1. `ModuleDisabled_ShouldSucceed` — module disabled → always passes
2. `NoUser_ShouldNotSucceed` — no authenticated user → not succeed
3. `UserWithActivePlan_ShouldSucceed` — user has matching active plan
4. `UserWithTrialingPlan_ShouldSucceed` — user has matching trialing plan
5. `UserWithWrongPlan_ShouldNotSucceed` — user has different plan
6. `UserWithNoSubscription_ShouldNotSucceed` — no subscription

**File:** `backend/tests/Seed.UnitTests/Authorization/RequiresFeatureAuthorizationHandlerTests.cs`

Test cases:
1. `ModuleDisabled_ShouldSucceed`
2. `NoUser_ShouldNotSucceed`
3. `UserWithFeature_ShouldSucceed`
4. `UserWithoutFeature_ShouldNotSucceed`

**File:** `backend/tests/Seed.UnitTests/Billing/Services/SubscriptionAccessServiceTests.cs`

Test cases:
1. `UserHasActivePlan_WithMatchingPlan_ReturnsTrue`
2. `UserHasActivePlan_WithNonMatchingPlan_ReturnsFalse`
3. `UserHasActivePlan_WithCanceledSubscription_ReturnsFalse`
4. `UserHasFeature_WithMatchingFeature_ReturnsTrue`
5. `UserHasFeature_WithNonMatchingFeature_ReturnsFalse`
6. `UserHasFeature_WithExpiredSubscription_ReturnsFalse`

Totale: ~16 test

Pattern test: InMemoryDatabase per i service tests, NSubstitute per i handler tests (mock `ISubscriptionAccessService` e `IOptions<ModulesSettings>`).

## Criteri di completamento

(Definition of Done copiata verbatim da PLAN-5.md)

- [x] `[RequiresPlan]` attribute works on controllers/actions
- [x] `[RequiresFeature]` attribute works on controllers/actions
- [x] Guards pass always when module is disabled
- [x] Guards check subscription status (active/trialing only)
- [x] HTTP 403 returned with descriptive message
- [x] Unit tests for authorization handlers (module enabled vs disabled, various subscription states)

## Risultato

### File creati

- `backend/src/Seed.Application/Common/Interfaces/ISubscriptionAccessService.cs` — interfaccia con `UserHasActivePlanAsync` e `UserHasFeatureAsync`
- `backend/src/Seed.Infrastructure/Billing/Services/SubscriptionAccessService.cs` — implementazione reale con query EF Core (AnyAsync)
- `backend/src/Seed.Infrastructure/Billing/Services/AlwaysAllowSubscriptionAccessService.cs` — fallback che ritorna sempre true quando il modulo è disabilitato
- `backend/src/Seed.Api/Authorization/PlanRequirement.cs` — `IAuthorizationRequirement` con `string[] PlanNames`
- `backend/src/Seed.Api/Authorization/FeatureRequirement.cs` — `IAuthorizationRequirement` con `string FeatureKey`
- `backend/src/Seed.Api/Authorization/RequiresPlanAttribute.cs` — attributo `[RequiresPlan("Pro")]` con policy `Plan:{names}`
- `backend/src/Seed.Api/Authorization/RequiresFeatureAttribute.cs` — attributo `[RequiresFeature("api-access")]` con policy `Feature:{key}`
- `backend/src/Seed.Api/Authorization/RequiresPlanAuthorizationHandler.cs` — handler che bypassa se module disabled, altrimenti chiama service
- `backend/src/Seed.Api/Authorization/RequiresFeatureAuthorizationHandler.cs` — handler analogo per feature
- `backend/tests/Seed.UnitTests/Billing/Services/SubscriptionAccessServiceTests.cs` — 7 test con InMemoryDatabase
- `backend/tests/Seed.UnitTests/Authorization/RequiresPlanAuthorizationHandlerTests.cs` — 6 test con NSubstitute
- `backend/tests/Seed.UnitTests/Authorization/RequiresFeatureAuthorizationHandlerTests.cs` — 4 test con NSubstitute (totale: 17 test nuovi + 1 esistente = 18 passati)

### File modificati

- `backend/src/Seed.Api/Authorization/PermissionAuthorizationPolicyProvider.cs` — aggiunto handling prefissi `Plan:` e `Feature:`
- `backend/src/Seed.Api/Program.cs` — registrati `RequiresPlanAuthorizationHandler` e `RequiresFeatureAuthorizationHandler`
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrato `SubscriptionAccessService` (dentro il blocco payments) e `AlwaysAllowSubscriptionAccessService` (else)
- `backend/tests/Seed.UnitTests/Seed.UnitTests.csproj` — aggiunto `FrameworkReference` a `Microsoft.AspNetCore.App` e `ProjectReference` a `Seed.Api` per testare gli handler

### Scelte chiave

- **Approccio `else` per fallback**: invece di registrare il service fuori dal blocco con bypass interno, si è usato il pattern `if/else` per mantenere la separazione netta tra modulo abilitato/disabilitato (consistente con il pattern email service già presente nel file)
- **`FrameworkReference` nel test project**: aggiunto `Microsoft.AspNetCore.App` per permettere ai test unitari di testare direttamente gli handler di `Seed.Api` senza dover ricorrere a integration tests
- **Nessuna deviazione dal piano**: tutti gli step sono stati implementati come descritto
