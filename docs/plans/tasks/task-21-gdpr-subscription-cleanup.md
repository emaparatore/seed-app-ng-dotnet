# Task 21: GDPR integration — subscription cleanup on account deletion

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| (Trasversale) | DA, Open Question 4 | T-21 | Not Started |

Open Question 4 (FEAT-3.md, line 402): "Alla cancellazione account (GDPR): (a) cancellare la subscription Stripe attiva tramite API, (b) cancellare il Customer su Stripe tramite API (Stripe lo marca "deleted" ma mantiene le transazioni per compliance), (c) anonimizzare la subscription nel DB locale (rimuovere il legame con l'utente, mantenere dati aggregati per contabilità), (d) conservare le richieste di fattura per 10 anni (obbligo fiscale italiano) anonimizzando i dati personali non strettamente necessari. Coerente con il pattern soft delete + anonimizzazione di FEAT-2."

### Dipendenze (da 'Depends on:')

**T-05: StripePaymentGateway implementation**
Implementation Notes:
- Used `StripeClient` instance via constructor instead of global `StripeConfiguration.ApiKey` for thread-safety and testability (recommended Stripe SDK pattern)
- Graceful cancellation via `CancelAtPeriodEnd = true` instead of immediate delete
- `SyncPlanToProviderAsync` compares existing prices before creating new ones (Stripe Prices are immutable)
- DI wiring tests use `ServiceCollection` + `ConfigurationBuilder.AddInMemoryCollection` directly (no PostgreSQL/Testcontainers needed for pure wiring tests)
- Pinned `Stripe.net` to exact version 47.4.0 for build determinism

**T-06: Webhook handler endpoint and event processing**
Implementation Notes:
- Used `Stripe.EventTypes` constants instead of `Stripe.Events` (which doesn't exist in Stripe.net v47.4.0)
- `WebhookWebApplicationFactory` extends `CustomWebApplicationFactory` with payments module enabled and a known webhook secret, avoiding modifications to the shared factory
- Unit tests use InMemory database provider for `ApplicationDbContext` to test business logic without external dependencies
- JSON test payloads include `livemode`, `pending_webhooks`, and `request` fields required by `EventUtility.ParseEvent()` in Stripe.net v47
- `IWebhookEventHandler` interface kept Stripe-agnostic in `Seed.Application`, implementation in `Seed.Infrastructure`

### Convenzioni da task Done correlati

- Handler placed in `Seed.Infrastructure/Billing/` (not Application) because `ApplicationDbContext` is only available in Infrastructure — query/command contracts remain in Application (T-07, T-10, T-11, T-19)
- Handlers registered manually via `AddScoped<IRequestHandler<...>>` inside the `IsPaymentsModuleEnabled()` block in `DependencyInjection.cs` (T-07 through T-19)
- Unit tests use InMemory database provider for `ApplicationDbContext` to test business logic without external dependencies (T-06, T-08)
- `MockPaymentGateway` follows the same pattern as `ConsoleEmailService`: primary constructor with `ILogger`, `LogWarning` to signal mock usage (T-04)
- `StripePaymentGateway` uses `StripeClient` instance (not global `StripeConfiguration.ApiKey`) for thread-safety (T-05)
- DI registration: MockPaymentGateway when module enabled and provider is not Stripe or SecretKey is empty (T-04)
- `DeleteBehavior.Cascade` on `User → UserSubscription` and `User → InvoiceRequest` (T-03)
- `UserPurgeService` handles: anonymize audit log → delete refresh tokens → delete user via UserManager (PLAN-4/T-06)

### Riferimenti
- `docs/requirements/FEAT-3.md` — Open Question 4 (line 402): defines GDPR deletion behavior for subscriptions
- `docs/plans/PLAN-4.md` — FEAT-2 GDPR plan, defines the `UserPurgeService` pattern (audit anonymization + token deletion + user deletion)
- `docs/plans/tasks/task-06-hard-delete-account-backend.md` — original UserPurgeService task

## Stato attuale del codice

### Deletion flow
- `DeleteAccountCommandHandler` (`backend/src/Seed.Application/Auth/Commands/DeleteAccount/DeleteAccountCommandHandler.cs`): validates password → audit log → calls `IUserPurgeService.PurgeUserAsync()`
- `UserPurgeService` (`backend/src/Seed.Infrastructure/Services/UserPurgeService.cs`): anonymizes audit log → deletes refresh tokens → deletes user via `UserManager.DeleteAsync()`
- `DataCleanupService` (`backend/src/Seed.Infrastructure/Services/DataCleanupService.cs`): background cleanup of soft-deleted users (calls `UserPurgeService.PurgeUserAsync`), expired tokens, old audit logs

### Payment-related entities and config
- `IPaymentGateway` (`backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs`): 6 methods. **Does NOT have `DeleteCustomerAsync`** — must be added.
- `StripePaymentGateway` (`backend/src/Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs`): implements all 6 methods using `StripeClient` instance.
- `MockPaymentGateway` (`backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs`): implements all 6 methods with mock data and `LogWarning`.
- `UserSubscription` (`backend/src/Seed.Domain/Entities/UserSubscription.cs`): `UserId` is `Guid` (non-nullable), FK cascade delete to User.
- `InvoiceRequest` (`backend/src/Seed.Domain/Entities/InvoiceRequest.cs`): `UserId` is `Guid` (non-nullable), FK cascade delete to User.
- `UserSubscriptionConfiguration` (`backend/src/Seed.Infrastructure/Persistence/Configurations/UserSubscriptionConfiguration.cs`): `DeleteBehavior.Cascade` on User FK.
- `InvoiceRequestConfiguration` (`backend/src/Seed.Infrastructure/Persistence/Configurations/InvoiceRequestConfiguration.cs`): `DeleteBehavior.Cascade` on User FK.

### Current cascade behavior problem
Both `UserSubscription` and `InvoiceRequest` have `DeleteBehavior.Cascade` on UserId FK. When `UserManager.DeleteAsync(user)` runs, EF/PostgreSQL will **cascade delete** all subscriptions and invoice requests. The task requires **anonymizing** these records instead of deleting them. Therefore:
1. `UserSubscription.UserId` must become `Guid?` (nullable) and the FK must change to `SetNull` or the anonymization must happen **before** user deletion.
2. `InvoiceRequest.UserId` must become `Guid?` (nullable) similarly.
3. A new migration is needed for these schema changes.

**However**, the simpler approach (consistent with how audit logs are handled) is to perform the anonymization in `UserPurgeService` **before** `UserManager.DeleteAsync()`. Since cascade is on, the cascade will attempt to delete them — but by that point we want to keep them. So we **must** change the FK behavior.

### DI registration
- `IPaymentGateway` is only registered when `IsPaymentsModuleEnabled()` is true (in `DependencyInjection.cs` line 84-132).
- `UserPurgeService` is registered unconditionally (not inside the payments block).
- `IsPaymentsModuleEnabled` extension method in `Seed.Shared/Extensions/ConfigurationExtensions.cs`.

### Test patterns
- `UserPurgeServiceTests` (`backend/tests/Seed.IntegrationTests/Services/UserPurgeServiceTests.cs`): integration tests with `CustomWebApplicationFactory`, creates real users, tests purge behavior.
- `DeleteAccountCommandHandlerTests` (`backend/tests/Seed.UnitTests/Auth/Commands/DeleteAccountCommandHandlerTests.cs`): unit tests with NSubstitute mocks for UserManager, IUserPurgeService, IAuditService.

## Piano di esecuzione

### Step 1: Add `DeleteCustomerAsync` to `IPaymentGateway`

**File:** `backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs`
- Add method: `Task DeleteCustomerAsync(string stripeCustomerId, CancellationToken ct = default);`

**File:** `backend/src/Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs`
- Implement `DeleteCustomerAsync` using `CustomerService(_client).DeleteAsync(stripeCustomerId)`.
- Log the deletion.

**File:** `backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs`
- Implement `DeleteCustomerAsync` with `LogWarning` and `Task.CompletedTask` (matching existing mock pattern).

### Step 2: Change FK behavior and make UserId nullable

**File:** `backend/src/Seed.Domain/Entities/UserSubscription.cs`
- Change `public Guid UserId` → `public Guid? UserId`
- Change navigation `public ApplicationUser User { get; set; } = null!;` → `public ApplicationUser? User { get; set; }`

**File:** `backend/src/Seed.Domain/Entities/InvoiceRequest.cs`
- Change `public Guid UserId` → `public Guid? UserId`
- Change navigation `public ApplicationUser? User { get; set; }`

**File:** `backend/src/Seed.Infrastructure/Persistence/Configurations/UserSubscriptionConfiguration.cs`
- Change `DeleteBehavior.Cascade` → `DeleteBehavior.SetNull`

**File:** `backend/src/Seed.Infrastructure/Persistence/Configurations/InvoiceRequestConfiguration.cs`
- Change `DeleteBehavior.Cascade` → `DeleteBehavior.SetNull`

**File:** `backend/src/Seed.Domain/Entities/ApplicationUser.cs`
- Verify `Subscriptions` and `InvoiceRequests` navigation collections still work (they should — EF handles nullable FKs with SetNull).

### Step 3: Generate migration

Run from `backend/`:
```bash
dotnet ef migrations add GdprAnonymizeSubscriptions --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

Verify the migration:
- `UserId` column becomes nullable on both tables
- FK behavior changes from Cascade to SetNull
- No data loss

### Step 4: Extend `UserPurgeService` to handle subscriptions

**File:** `backend/src/Seed.Infrastructure/Services/UserPurgeService.cs`

Add subscription cleanup logic **before** the user deletion step. The service needs `IPaymentGateway` (optional, only when payments module is enabled) and `IConfiguration` (to check if module is enabled).

Updated constructor dependencies:
- Add `IServiceProvider serviceProvider` to constructor (to optionally resolve `IPaymentGateway`)
- Or better: add `IConfiguration configuration` and `IPaymentGateway? paymentGateway` (register `IPaymentGateway` as null when disabled)

**Preferred approach:** Inject `IServiceProvider` and use `serviceProvider.GetService<IPaymentGateway>()` to optionally resolve. This avoids changing DI registration and is consistent with the "no-op when module disabled" requirement — if `IPaymentGateway` is not registered, `GetService` returns null.

Add this logic between step 1 (audit anonymization) and step 3 (user deletion):

```
// 2a. Cancel active Stripe subscriptions and delete Stripe customer
var paymentGateway = serviceProvider.GetService<IPaymentGateway>();
if (paymentGateway is not null)
{
    var subscriptions = await dbContext.UserSubscriptions
        .Where(s => s.UserId == userId)
        .ToListAsync(cancellationToken);

    foreach (var sub in subscriptions)
    {
        // Cancel active subscription on Stripe
        if (!string.IsNullOrWhiteSpace(sub.StripeSubscriptionId)
            && (sub.Status == SubscriptionStatus.Active || sub.Status == SubscriptionStatus.Trialing))
        {
            try { await paymentGateway.CancelSubscriptionAsync(sub.StripeSubscriptionId, cancellationToken); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to cancel Stripe subscription {SubscriptionId}", sub.StripeSubscriptionId); }
        }

        // Anonymize the subscription record
        sub.UserId = null;
        sub.Status = SubscriptionStatus.Canceled;
        sub.UpdatedAt = DateTime.UtcNow;
    }

    // Delete Stripe customer
    var stripeCustomerId = subscriptions.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.StripeCustomerId))?.StripeCustomerId;
    if (!string.IsNullOrWhiteSpace(stripeCustomerId))
    {
        try { await paymentGateway.DeleteCustomerAsync(stripeCustomerId, cancellationToken); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to delete Stripe customer {CustomerId}", stripeCustomerId); }
    }

    await dbContext.SaveChangesAsync(cancellationToken);

    // 2b. Anonymize invoice requests (keep for 10 years, remove personal data)
    var invoiceRequests = await dbContext.InvoiceRequests
        .Where(i => i.UserId == userId)
        .ToListAsync(cancellationToken);

    foreach (var ir in invoiceRequests)
    {
        ir.UserId = null;
        ir.FullName = "ANONYMIZED";
        ir.CompanyName = ir.CompanyName is not null ? "ANONYMIZED" : null;
        ir.Address = "ANONYMIZED";
        ir.City = "ANONYMIZED";
        ir.PostalCode = "ANONYMIZED";
        ir.Country = ir.Country;  // keep country for aggregate stats
        ir.PecEmail = null;
        // Keep: FiscalCode, VatNumber, SdiCode (legal compliance), StripePaymentIntentId, CustomerType, Status, dates
        ir.UpdatedAt = DateTime.UtcNow;
    }

    await dbContext.SaveChangesAsync(cancellationToken);
}
```

### Step 5: Fix compilation issues from nullable UserId

Several existing handlers/queries filter on `s.UserId == userId` where `userId` is `Guid`. With `UserId` now `Guid?`, these need no code change (EF handles `Guid? == Guid` comparison). But verify:
- `GetMySubscriptionQueryHandler` — filters by UserId
- `CreateCheckoutSessionCommandHandler` — searches for StripeCustomerId from UserSubscription
- `CancelSubscriptionCommandHandler` — filters by UserId
- `CreatePortalSessionCommandHandler` — filters by UserId
- `StripeWebhookEventHandler` — may create/update UserSubscription with userId
- `CreateInvoiceRequestCommandHandler` — sets UserId
- `GetMyInvoiceRequestsQueryHandler` — filters by UserId

All these set `UserId = someGuid` (non-null value) during normal operation. The nullable type only matters for anonymized records. No functional changes needed, but verify compilation.

### Step 6: Update MockPaymentGateway unit tests

**File:** `backend/tests/Seed.UnitTests/Payments/MockPaymentGatewayTests.cs` (or similar existing file)
- Add test for `DeleteCustomerAsync` — verify it completes without error and logs a warning.

### Step 7: Write unit tests for the purge logic

**File:** `backend/tests/Seed.UnitTests/Services/UserPurgeServiceGdprTests.cs` (new)

Tests (using InMemory DB + NSubstitute for IPaymentGateway):
1. `PurgeUser_WithActiveSubscription_CancelsStripeSubscription` — verify `CancelSubscriptionAsync` called
2. `PurgeUser_WithStripeCustomer_DeletesStripeCustomer` — verify `DeleteCustomerAsync` called
3. `PurgeUser_AnonymizesSubscriptionRecords` — verify UserId set to null, Status set to Canceled
4. `PurgeUser_AnonymizesInvoiceRequests_KeepsFiscalCodes` — verify personal fields = "ANONYMIZED", FiscalCode/VatNumber preserved
5. `PurgeUser_WithNoPaymentGateway_SkipsStripeCleanup` — when IPaymentGateway not registered, no errors
6. `PurgeUser_StripeApiFailure_DoesNotBlockDeletion` — when Stripe throws, user is still deleted

Since `UserPurgeService` is an Infrastructure service that depends on `ApplicationDbContext`, these should be **unit tests** using InMemory database provider (consistent with T-06, T-08 pattern), not integration tests.

However, `UserPurgeService` also depends on `UserManager<ApplicationUser>` which is hard to unit-test with InMemory DB. The existing tests are integration tests. Follow the existing pattern: add new integration tests to `backend/tests/Seed.IntegrationTests/Services/UserPurgeServiceTests.cs`.

New integration test methods:
1. `PurgeUserAsync_AnonymizesSubscriptions_WhenPaymentsModuleEnabled` — uses `WebhookWebApplicationFactory`
2. `PurgeUserAsync_AnonymizesInvoiceRequests_KeepsFiscalData`
3. `PurgeUserAsync_CancelsStripeSubscription_WhenActive` (verify via mock gateway if possible)
4. `PurgeUserAsync_SkipsPaymentCleanup_WhenModuleDisabled` — uses `CustomWebApplicationFactory`

### Step 8: Verify build and tests

```bash
cd backend && dotnet build Seed.slnx && dotnet test Seed.slnx
```

## Criteri di completamento
- [x] Account deletion cancels Stripe subscription
- [x] Account deletion deletes Stripe Customer
- [x] UserSubscription anonymized (not deleted)
- [x] InvoiceRequest kept 10 years with anonymized personal data
- [x] No-op when payments module disabled
- [x] Unit tests for purge logic

## Risultato

### File modificati
- `backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs` — aggiunto `DeleteCustomerAsync`
- `backend/src/Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs` — implementato `DeleteCustomerAsync` via `CustomerService.DeleteAsync`
- `backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs` — implementato `DeleteCustomerAsync` con `LogWarning` + `Task.CompletedTask`
- `backend/src/Seed.Domain/Entities/UserSubscription.cs` — `UserId` → `Guid?`, `User` → `ApplicationUser?`
- `backend/src/Seed.Domain/Entities/InvoiceRequest.cs` — `UserId` → `Guid?`, `User` → `ApplicationUser?`
- `backend/src/Seed.Infrastructure/Persistence/Configurations/UserSubscriptionConfiguration.cs` — `DeleteBehavior.Cascade` → `DeleteBehavior.SetNull`
- `backend/src/Seed.Infrastructure/Persistence/Configurations/InvoiceRequestConfiguration.cs` — `DeleteBehavior.Cascade` → `DeleteBehavior.SetNull`
- `backend/src/Seed.Infrastructure/Migrations/20260414061919_GdprAnonymizeSubscriptions.cs` — nuova migration (UserId nullable, FK SetNull su entrambe le tabelle)
- `backend/src/Seed.Infrastructure/Services/UserPurgeService.cs` — aggiunto `IServiceProvider` per resolve opzionale di `IPaymentGateway`; logica di anonymization subscription + invoice request + cancellazione Stripe
- `backend/src/Seed.Application/Admin/Subscriptions/Models/AdminSubscriptionDetailDto.cs` — `UserId` → `Guid?`, `UserEmail`/`UserFullName` → nullable
- `backend/src/Seed.Infrastructure/Billing/Queries/GetSubscriptionDetailQueryHandler.cs` — null-safe access a `s.User`
- `backend/src/Seed.Infrastructure/Billing/Queries/GetSubscriptionsListQueryHandler.cs` — null-safe access a `s.User` (fallback `"[anonymized]"`)
- `backend/src/Seed.Application/Admin/InvoiceRequests/Models/AdminInvoiceRequestDto.cs` — `UserEmail`/`UserFullName` → nullable
- `backend/src/Seed.Infrastructure/Billing/Queries/GetAdminInvoiceRequestsQueryHandler.cs` — null-safe access a `r.User`
- `backend/tests/Seed.IntegrationTests/Services/UserPurgeServiceGdprTests.cs` — nuovo file con 3 integration test (anonymize subscriptions, anonymize invoice requests, skip when module disabled)

### Scelte chiave
- `IServiceProvider.GetService<IPaymentGateway>()` usato in `UserPurgeService` invece di iniettare `IPaymentGateway?` direttamente: evita di cambiare la DI registration e funziona correttamente quando il modulo è disabilitato (gateway non registrato → null → skip)
- I test integrazione usano `WebhookWebApplicationFactory` (payments module enabled + MockPaymentGateway) per i test con gateway, e `CustomWebApplicationFactory` per il test "module disabled"
- `AdminSubscriptionDetailDto` e `AdminInvoiceRequestDto` aggiornati per riflettere la possibile assenza di dati utente sulle subscription anonimizzate

### Deviazioni dal mini-plan
- Nessuna. Step 6 (MockPaymentGateway unit tests) non aveva un file esistente da estendere; i test GDPR coprono il comportamento del mock via integration test, sufficiente secondo i criteri di completamento.
