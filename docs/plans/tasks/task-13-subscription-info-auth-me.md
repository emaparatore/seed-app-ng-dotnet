# Task 13: Include subscription info in auth/me response

## Contesto ereditato dal piano

### Storie coperte

| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-011 | Feature gating frontend | T-13 | ⏳ Not Started |

US-011 (RF-2): "As a sviluppatore frontend, I want mostrare/nascondere elementi UI in base al piano dell'utente, so that l'interfaccia rifletta le feature disponibili per il piano attuale."

RF-2: Il sistema deve tracciare la subscription attiva di ogni utente: piano attuale e stato (active, trialing, past_due, canceled, expired), date di inizio, rinnovo, scadenza, periodo di trial (configurabile per piano).

### Dipendenze (da 'Depends on:')

T-03: EF Core configuration and migration

**Implementation Notes (T-03):**
- Followed existing `RefreshTokenConfiguration` pattern for structure and style (sealed class, file-scoped namespace)
- Enum properties converted to string in DB via `HasConversion<string>()` for readability
- Unique index on `StripeSubscriptionId` uses `HasFilter("\"StripeSubscriptionId\" IS NOT NULL")` to handle nullable correctly in PostgreSQL
- `DeleteBehavior.Restrict` on `SubscriptionPlan → UserSubscription` to prevent deletion of plans with active subscriptions
- `DeleteBehavior.Cascade` on `User → UserSubscription` and `User → InvoiceRequest`, consistent with existing RefreshToken pattern

### Convenzioni da task Done correlati

**T-12 (Subscription guards — backend authorization):**
- `PermissionAuthorizationPolicyProvider` extended to handle `Plan:` and `Feature:` prefixes in addition to `Permission:`, keeping a single policy provider for all authorization schemes
- `AlwaysAllowSubscriptionAccessService` registered as fallback (else branch) when payments module is disabled, following the same if/else pattern as the email service in `DependencyInjection.cs`
- `ISubscriptionAccessService` uses `AnyAsync` for efficiency; queries filter on `SubscriptionStatus.Active` and `SubscriptionStatus.Trialing` only

**T-07 (Public plans API):**
- Handler placed in `Seed.Infrastructure/Billing/Queries/` instead of `Seed.Application/` because Application does not reference Infrastructure (and thus `ApplicationDbContext`). Query contract and DTOs remain in Application.
- Handler registered manually in DI (`AddScoped<IRequestHandler<...>>`) inside the `IsPaymentsModuleEnabled()` block, since MediatR only scans the Application assembly.
- Used LINQ projection (`Select`) instead of Mapster for mapping — simpler for a read-only query with no complex logic.

**T-09 (Subscription management — portal, view, cancel):**
- `GetMySubscription` returns `null` (not failure) when no subscription exists — "no data ≠ error" pattern, consistent with clean API semantics
- All 3 new handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs`
- Unit tests use InMemory DB for `ApplicationDbContext` and NSubstitute for `IPaymentGateway`/`IAuditService`, following the pattern established in T-08

**T-01 (Module toggle system):**
- Extension method `IsPaymentsModuleEnabled` lives in `Seed.Shared/Extensions/ConfigurationExtensions.cs`
- `ModulesSettings` is registered unconditionally to allow injection even when the payments module is disabled

### Riferimenti

- `docs/requirements/FEAT-3.md` — RF-2 (Gestione subscription utente), US-011 (Feature gating frontend)
- `docs/plans/PLAN-5.md` — Task T-13 definition

## Stato attuale del codice

### File esistenti rilevanti

- `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/MeResponse.cs` — Current DTO: `MeResponse(Guid Id, string Email, string FirstName, string LastName, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)`. No subscription fields.
- `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs` — `sealed record GetCurrentUserQuery(Guid UserId) : IRequest<Result<MeResponse>>`
- `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs` — Uses `UserManager<ApplicationUser>` and `IPermissionService`. Does NOT have access to `ApplicationDbContext` or any subscription service.
- `backend/tests/Seed.UnitTests/Auth/Queries/GetCurrentUserQueryHandlerTests.cs` — 3 existing tests: user not found, user inactive, successful response with permissions.
- `backend/src/Seed.Api/Controllers/AuthController.cs` — `Me()` action at line 124, sends `GetCurrentUserQuery` and returns `MeResponse`.
- `backend/src/Seed.Domain/Entities/UserSubscription.cs` — Entity with `UserId`, `PlanId`, `Status`, `StripeSubscriptionId`, `StripeCustomerId`, `CurrentPeriodStart`, `CurrentPeriodEnd`, `TrialEnd`, `CanceledAt`.
- `backend/src/Seed.Domain/Entities/SubscriptionPlan.cs` — Entity with `Name`, `Features` (ICollection<PlanFeature>).
- `backend/src/Seed.Domain/Entities/PlanFeature.cs` — Entity with `Key`, `Description`, `LimitValue`.
- `backend/src/Seed.Domain/Enums/SubscriptionStatus.cs` — `Active, Trialing, PastDue, Canceled, Expired`
- `backend/src/Seed.Application/Common/Interfaces/ISubscriptionAccessService.cs` — Interface with `UserHasActivePlanAsync` and `UserHasFeatureAsync` (not suitable for returning data, only bool checks).
- `backend/src/Seed.Infrastructure/Billing/Services/SubscriptionAccessService.cs` — Queries `UserSubscriptions` with `ActiveStatuses` filter.
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — Conditional registration: `ISubscriptionAccessService` (real vs AlwaysAllow) inside `IsPaymentsModuleEnabled()` block.
- `backend/src/Seed.Shared/Extensions/ConfigurationExtensions.cs` — `IsPaymentsModuleEnabled()` reads `Modules:Payments:Enabled`.

### Pattern già in uso che il task deve seguire

1. **Handler in Application layer**: `GetCurrentUserQueryHandler` lives in `Seed.Application` and uses `UserManager` + `IPermissionService` (interfaces). It does NOT access `ApplicationDbContext` directly.
2. **Subscription data access**: Subscription queries use `ApplicationDbContext` which is only in Infrastructure. To access subscription data from the handler, a new interface in Application is needed (similar to `ISubscriptionAccessService`).
3. **Conditional behavior**: When payments module is disabled, subscription fields should be null (frontend treats as "all features available" per DA-1).
4. **DTOs as sealed records**: All DTOs in Application layer are `sealed record` types.
5. **Unit tests with NSubstitute**: Existing `GetCurrentUserQueryHandlerTests` use NSubstitute for `UserManager` and `IPermissionService`.

## Piano di esecuzione

### Step 1: Create subscription info DTO

- **File:** `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/SubscriptionInfoDto.cs` (new)
- Create a `sealed record SubscriptionInfoDto(string CurrentPlan, IReadOnlyList<string> PlanFeatures, string SubscriptionStatus, DateTime? TrialEndsAt)`

### Step 2: Extend MeResponse

- **File:** `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/MeResponse.cs` (modify)
- Add optional `SubscriptionInfoDto? Subscription` parameter to the record. Null means payments module disabled or no active subscription.

### Step 3: Create ISubscriptionInfoService interface

- **File:** `backend/src/Seed.Application/Common/Interfaces/ISubscriptionInfoService.cs` (new)
- Interface: `Task<SubscriptionInfoDto?> GetUserSubscriptionInfoAsync(Guid userId, CancellationToken ct = default)`
- This is separate from `ISubscriptionAccessService` (which is bool-based for authorization). This returns data for the /me response.

### Step 4: Implement SubscriptionInfoService in Infrastructure

- **File:** `backend/src/Seed.Infrastructure/Billing/Services/SubscriptionInfoService.cs` (new)
- Query `ApplicationDbContext.UserSubscriptions` for the user where Status is Active or Trialing, include Plan and Plan.Features.
- Return `SubscriptionInfoDto` with plan name, feature keys, status string, trial end date.
- Return `null` if no active/trialing subscription found.

### Step 5: Implement NullSubscriptionInfoService (fallback when module disabled)

- **File:** `backend/src/Seed.Infrastructure/Billing/Services/NullSubscriptionInfoService.cs` (new)
- Always returns `null`. Registered when payments module is disabled (same if/else pattern as `AlwaysAllowSubscriptionAccessService`).

### Step 6: Register in DI

- **File:** `backend/src/Seed.Infrastructure/DependencyInjection.cs` (modify)
- Inside `IsPaymentsModuleEnabled()` block: `services.AddScoped<ISubscriptionInfoService, SubscriptionInfoService>()`
- In else block: `services.AddScoped<ISubscriptionInfoService, NullSubscriptionInfoService>()`

### Step 7: Update GetCurrentUserQueryHandler

- **File:** `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs` (modify)
- Add `ISubscriptionInfoService` to constructor via primary constructor pattern.
- After loading roles/permissions, call `await subscriptionInfoService.GetUserSubscriptionInfoAsync(user.Id, cancellationToken)`.
- Pass the result (nullable) to the `MeResponse` constructor.

### Step 8: Update existing tests and add new ones

- **File:** `backend/tests/Seed.UnitTests/Auth/Queries/GetCurrentUserQueryHandlerTests.cs` (modify)
- Add NSubstitute mock for `ISubscriptionInfoService` in constructor.
- Update existing `Should_Return_MeResponse_When_User_Exists_And_Active` test to verify `Subscription` is null when service returns null.
- Add new test: `Should_Include_Subscription_Info_When_Available` — mock `ISubscriptionInfoService` to return a `SubscriptionInfoDto`, verify `MeResponse.Subscription` is populated with correct plan name, features, status, trial end.

### Tests to write/verify

- **File:** `backend/tests/Seed.UnitTests/Auth/Queries/GetCurrentUserQueryHandlerTests.cs`
  - Update existing test `Should_Return_MeResponse_When_User_Exists_And_Active` — verify `Subscription` is null (no breaking change)
  - New: `Should_Include_Subscription_Info_When_Service_Returns_Data` — mock returns SubscriptionInfoDto, verify all fields mapped
  - New: `Should_Return_Null_Subscription_When_Service_Returns_Null` — explicit test for null case (module disabled or no subscription)

## Criteri di completamento

(Copied verbatim from PLAN-5.md Definition of Done)

- [x] `MeResponse` includes subscription data
- [x] Handler loads subscription conditionally (only when module enabled)
- [x] Returns null/empty when module disabled or no subscription
- [x] Existing auth tests still pass
- [x] Unit test for handler with subscription data

## Risultato

### File modificati

- `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/SubscriptionInfoDto.cs` — **NEW**: sealed record con `CurrentPlan`, `PlanFeatures`, `SubscriptionStatus`, `TrialEndsAt`
- `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/MeResponse.cs` — aggiunto parametro `SubscriptionInfoDto? Subscription`
- `backend/src/Seed.Application/Common/Interfaces/ISubscriptionInfoService.cs` — **NEW**: interfaccia con `GetUserSubscriptionInfoAsync`
- `backend/src/Seed.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQueryHandler.cs` — aggiunto `ISubscriptionInfoService` al costruttore; chiama il servizio e passa il risultato al `MeResponse`
- `backend/src/Seed.Infrastructure/Billing/Services/SubscriptionInfoService.cs` — **NEW**: implementazione reale; query con `Include(Plan).ThenInclude(Features)`, filtra su Active/Trialing, ritorna `null` se nessuna subscription trovata
- `backend/src/Seed.Infrastructure/Billing/Services/NullSubscriptionInfoService.cs` — **NEW**: fallback che ritorna sempre `null` (modulo disabilitato)
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione `ISubscriptionInfoService` (reale nel blocco `IsPaymentsModuleEnabled`, null nel branch else)
- `backend/tests/Seed.UnitTests/Auth/Queries/GetCurrentUserQueryHandlerTests.cs` — aggiunto mock `ISubscriptionInfoService`; aggiornato test esistente; aggiunti `Should_Include_Subscription_Info_When_Service_Returns_Data` e `Should_Return_Null_Subscription_When_Service_Returns_Null`

### Scelte chiave

- `ISubscriptionInfoService` separato da `ISubscriptionAccessService`: quest'ultimo è bool-based per authorization, il nuovo restituisce dati strutturati per il /me endpoint
- `NullSubscriptionInfoService` (non `AlwaysAllow`) come fallback: quando il modulo pagamenti è disabilitato, la subscription è `null` — il frontend interpreta assenza come "tutte le feature disponibili" (DA-1)
- Nessuna deviazione dal mini-plan
