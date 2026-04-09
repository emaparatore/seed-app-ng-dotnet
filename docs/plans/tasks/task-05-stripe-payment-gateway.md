# Task 05: StripePaymentGateway implementation

## Contesto

- `IPaymentGateway` interface already defined in `backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs` with 6 methods
- 4 gateway DTOs exist in `backend/src/Seed.Application/Common/Models/`: `CreateCheckoutRequest`, `SubscriptionDetails`, `SyncPlanRequest`, `ProductSyncResult`
- `MockPaymentGateway` exists in `backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs` — serves as reference implementation
- `StripeSettings` POCO in `backend/src/Seed.Shared/Configuration/StripeSettings.cs` with `SecretKey`, `PublishableKey`, `WebhookSecret`
- DI registration in `backend/src/Seed.Infrastructure/DependencyInjection.cs` (lines 57-70): currently registers `MockPaymentGateway` when provider is NOT Stripe or SecretKey is empty. The `else` branch for the real Stripe gateway is **missing** — needs to be added.
- `Stripe.net` NuGet package is **not yet** in `Seed.Infrastructure.csproj`

## Piano di esecuzione

### Step 1: Add Stripe.net NuGet package
- File: `backend/src/Seed.Infrastructure/Seed.Infrastructure.csproj`
- Add `<PackageReference Include="Stripe.net" Version="47.*" />` (latest stable for .NET 10)
- Run `dotnet restore` from `backend/`

### Step 2: Create StripePaymentGateway class
- File to create: `backend/src/Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs`
- Sealed class with primary constructor injecting `IOptions<StripeSettings>` and `ILogger<StripePaymentGateway>`
- Set `StripeConfiguration.ApiKey` in constructor from settings
- Implement all 6 methods:

  1. **CreateCustomerAsync**: Use `CustomerService.CreateAsync()` with `CustomerCreateOptions { Email, Name }`. Return `customer.Id`.

  2. **CreateCheckoutSessionAsync**: Use `Stripe.Checkout.SessionService.CreateAsync()` with options:
     - `Mode = "subscription"`
     - `CustomerEmail` (if no CustomerId) or `Customer` (if CustomerId provided)
     - `LineItems` with `Price = request.PriceId`, `Quantity = 1`
     - `SuccessUrl = request.SuccessUrl`, `CancelUrl = request.CancelUrl`
     - `SubscriptionData.TrialPeriodDays = request.TrialDays` (if > 0)
     - `Metadata = request.Metadata`
     - Return `session.Url`

  3. **CreateCustomerPortalSessionAsync**: Use `Stripe.BillingPortal.SessionService.CreateAsync()` with `Customer = stripeCustomerId`, `ReturnUrl = returnUrl`. Return `session.Url`.

  4. **CancelSubscriptionAsync**: Use `SubscriptionService.UpdateAsync()` with `SubscriptionUpdateOptions { CancelAtPeriodEnd = true }` (graceful cancellation, not immediate delete).

  5. **GetSubscriptionAsync**: Use `SubscriptionService.GetAsync()`. Map to `SubscriptionDetails` record. Return null if `StripeException` with 404.

  6. **SyncPlanToProviderAsync**: 
     - If `request.ProductId` is null → `ProductService.CreateAsync()` with Name/Description; else `ProductService.UpdateAsync()`
     - Create new `PriceService.CreateAsync()` for monthly (with `Recurring.Interval = "month"`) and yearly (`Recurring.Interval = "year"`) using `UnitAmount = request.MonthlyPriceInCents` / `YearlyPriceInCents`, `Currency = "eur"`
     - Only create new prices if amount differs from existing (compare by creating new ones; Stripe prices are immutable)
     - Return `ProductSyncResult` with product ID and price IDs

- Wrap all Stripe API calls in try/catch for `StripeException`, log and rethrow as appropriate

### Step 3: Update DI registration
- File: `backend/src/Seed.Infrastructure/DependencyInjection.cs`
- Add `else` branch at line ~69 (after MockPaymentGateway registration):
  ```csharp
  else
  {
      services.AddScoped<IPaymentGateway, StripePaymentGateway>();
  }
  ```
- This ensures: when module enabled + provider is "Stripe" + SecretKey is non-empty → use real gateway

### Step 4: Build and verify
- Run `dotnet build backend/Seed.slnx` — must compile cleanly
- Run `dotnet test backend/Seed.slnx` — all existing tests must pass (Mock tests still use MockPaymentGateway since test config doesn't have Stripe keys)

### Step 5: Integration test for DI wiring
- File: `backend/tests/Seed.IntegrationTests/` — add a test that verifies:
  - With payments enabled + provider=Stripe + SecretKey set → resolves `IPaymentGateway` as `StripePaymentGateway`
  - With payments enabled + no SecretKey → resolves as `MockPaymentGateway`
  - With payments disabled → `IPaymentGateway` is not registered
- Follow existing integration test patterns (check `WebApplicationFactory` setup)

### Test da scrivere/verificare
1. **DI wiring integration test** — verify correct gateway resolved based on configuration
2. **Existing MockPaymentGateway unit tests** — must continue passing (6 tests)
3. **Build verification** — solution compiles with no warnings related to new code

## Criteri di completamento
- `Stripe.net` package added to `Seed.Infrastructure.csproj`
- `StripePaymentGateway` class fully implements all 6 `IPaymentGateway` methods using Stripe SDK
- DI registration updated: Stripe gateway registered when provider is "Stripe" and SecretKey is present
- Solution builds successfully (`dotnet build backend/Seed.slnx`)
- All existing tests pass (`dotnet test backend/Seed.slnx`)
- Integration test verifying DI wiring for Stripe vs Mock vs disabled scenarios

## Risultato

### File modificati/creati
- **Creato:** `backend/src/Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs` — implementazione completa di `IPaymentGateway` con Stripe SDK
- **Creato:** `backend/tests/Seed.IntegrationTests/Services/PaymentGatewayDiWiringTests.cs` — 4 test per verifica DI wiring
- **Modificato:** `backend/src/Seed.Infrastructure/Seed.Infrastructure.csproj` — aggiunto `Stripe.net 47.4.0`
- **Modificato:** `backend/src/Seed.Infrastructure/DependencyInjection.cs` — aggiunto `else` branch per registrare `StripePaymentGateway`

### Scelte implementative e motivazioni
- **StripeClient iniettato via constructor:** usato `new StripeClient(secretKey)` come campo readonly invece di settare il globale `StripeConfiguration.ApiKey`, per evitare problemi di thread-safety e consentire testabilità futura
- **Cancellazione graceful:** `CancelSubscriptionAsync` usa `CancelAtPeriodEnd = true` (non cancellazione immediata) come da piano
- **SyncPlanToProviderAsync con price comparison:** confronta il prezzo esistente prima di crearne uno nuovo, dato che i Stripe Prices sono immutabili
- **GetSubscriptionAsync con null return:** cattura `StripeException` 404 e ritorna null, coerente con il contratto dell'interfaccia
- **Test DI leggeri:** i test usano `ServiceCollection` direttamente con `ConfigurationBuilder.AddInMemoryCollection` invece di `CustomWebApplicationFactory`, per non richiedere PostgreSQL/Testcontainers per un test puramente di wiring

### Eventuali deviazioni dal piano e perché
- **StripeClient vs StripeConfiguration.ApiKey:** il piano diceva "Set `StripeConfiguration.ApiKey` in constructor" ma ho usato `StripeClient` passato ai service per-instance. Questo è il pattern raccomandato dalla Stripe SDK per evitare stato globale e garantire thread-safety in ambienti DI/scoped
- **Stripe.net versione 47.4.0:** specificata versione esatta invece di `47.*` per determinismo nella build
