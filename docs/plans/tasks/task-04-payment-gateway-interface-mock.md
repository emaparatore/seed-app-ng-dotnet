# Task 04: IPaymentGateway interface and MockPaymentGateway

## Contesto

- **Stato attuale del codice rilevante:**
  - Le domain entities sono gia' pronte (T-02): `SubscriptionPlan`, `UserSubscription`, `PlanFeature`, `InvoiceRequest` in `Seed.Domain/Entities/`
  - Gli enums sono definiti: `BillingInterval`, `SubscriptionStatus`, `PlanStatus`, `CustomerType`, `InvoiceRequestStatus` in `Seed.Domain/Enums/`
  - La configurazione Stripe e' registrata condizionalmente in `DependencyInjection.cs` (linea 56-59) quando `IsPaymentsModuleEnabled()` e' true
  - `StripeSettings` POCO esiste in `Seed.Shared/Configuration/` con `SecretKey`, `PublishableKey`, `WebhookSecret`
  - `PaymentsModuleSettings` ha `Enabled` (bool) e `Provider` (string)
  - Pattern esistente per servizi con fallback: `IEmailService` → `SmtpEmailService` / `ConsoleEmailService`
  - Le interfacce applicative vanno in `Seed.Application/Common/Interfaces/`
  - I model/DTO applicativi vanno in `Seed.Application/Common/Models/`
  - I servizi infrastrutturali vanno in `Seed.Infrastructure/Services/`

- **Dipendenze e vincoli:**
  - Dipende da T-01 (done) e T-02 (done)
  - L'interfaccia deve essere nel layer Application (no dipendenze da Stripe SDK)
  - Il MockPaymentGateway va nel layer Infrastructure (come ConsoleEmailService)
  - La registrazione DI deve essere condizionale: registrare MockPaymentGateway quando il modulo pagamenti e' abilitato ma il provider non e' "Stripe" o la SecretKey e' vuota
  - I DTOs del gateway devono essere indipendenti dalle domain entities (layer separation)

## Piano di esecuzione

### File da creare

1. **`backend/src/Seed.Application/Common/Models/CreateCheckoutRequest.cs`**
   - Sealed record con: `PriceId` (string), `CustomerEmail` (string), `CustomerId` (string?), `SuccessUrl` (string), `CancelUrl` (string), `TrialDays` (int?), `Metadata` (Dictionary<string, string>?)

2. **`backend/src/Seed.Application/Common/Models/SubscriptionDetails.cs`**
   - Sealed record con: `SubscriptionId` (string), `CustomerId` (string), `Status` (string), `PriceId` (string), `CurrentPeriodStart` (DateTime), `CurrentPeriodEnd` (DateTime), `TrialEnd` (DateTime?), `CancelAtPeriodEnd` (bool)

3. **`backend/src/Seed.Application/Common/Models/SyncPlanRequest.cs`**
   - Sealed record con: `ProductId` (string?), `Name` (string), `Description` (string?), `MonthlyPriceInCents` (long), `YearlyPriceInCents` (long), `ExistingMonthlyPriceId` (string?), `ExistingYearlyPriceId` (string?)

4. **`backend/src/Seed.Application/Common/Models/ProductSyncResult.cs`**
   - Sealed record con: `ProductId` (string), `MonthlyPriceId` (string), `YearlyPriceId` (string)

5. **`backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs`**
   - Interface con 6 metodi come da piano PLAN-5:
     - `CreateCustomerAsync(string email, string name, CancellationToken ct)` → string
     - `CreateCheckoutSessionAsync(CreateCheckoutRequest request, CancellationToken ct)` → string
     - `CreateCustomerPortalSessionAsync(string stripeCustomerId, string returnUrl, CancellationToken ct)` → string
     - `CancelSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)` → void
     - `GetSubscriptionAsync(string stripeSubscriptionId, CancellationToken ct)` → SubscriptionDetails?
     - `SyncPlanToProviderAsync(SyncPlanRequest request, CancellationToken ct)` → ProductSyncResult

6. **`backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs`**
   - Sealed class che implementa `IPaymentGateway`
   - Usa `ILogger<MockPaymentGateway>` per loggare le operazioni (stesso pattern di ConsoleEmailService)
   - Restituisce dati fake deterministici:
     - `CreateCustomerAsync` → `"mock_cus_{Guid}"` 
     - `CreateCheckoutSessionAsync` → `"https://mock-checkout.example.com/session/{Guid}"`
     - `CreateCustomerPortalSessionAsync` → `"https://mock-portal.example.com/session/{Guid}"`
     - `CancelSubscriptionAsync` → log e return
     - `GetSubscriptionAsync` → SubscriptionDetails con dati fake
     - `SyncPlanToProviderAsync` → ProductSyncResult con IDs fake

7. **`backend/tests/Seed.UnitTests/Services/MockPaymentGatewayTests.cs`**
   - 6 test, uno per metodo:
     - `CreateCustomerAsync_ReturnsCustomerIdStartingWithMockPrefix`
     - `CreateCheckoutSessionAsync_ReturnsUrl`
     - `CreateCustomerPortalSessionAsync_ReturnsUrl`
     - `CancelSubscriptionAsync_CompletesSuccessfully`
     - `GetSubscriptionAsync_ReturnsDetails`
     - `SyncPlanToProviderAsync_ReturnsProductSyncResult`

### File da modificare

8. **`backend/src/Seed.Infrastructure/DependencyInjection.cs`**
   - Nel blocco `if (configuration.IsPaymentsModuleEnabled())` (linea 56-59), aggiungere la registrazione di `IPaymentGateway`:
     - Se `Provider == "Stripe"` e `Stripe:SecretKey` non e' vuoto → non registrare nulla (sara' T-05 a registrare StripePaymentGateway)
     - Altrimenti → registrare `MockPaymentGateway` come `IPaymentGateway` (scoped)
   - Questo permette di usare il mock in dev/test anche con il modulo abilitato

### Approccio tecnico step-by-step

1. Creare i 4 DTO records in `Common/Models/`
2. Creare l'interfaccia `IPaymentGateway` in `Common/Interfaces/`
3. Creare la directory `Services/Payments/` in Infrastructure
4. Implementare `MockPaymentGateway` con logging e dati deterministici
5. Aggiornare `DependencyInjection.cs` con registrazione condizionale
6. Scrivere i test unitari per MockPaymentGateway
7. Verificare build: `dotnet build Seed.slnx`
8. Verificare test: `dotnet test Seed.slnx`

### Test da scrivere/verificare

- 6 unit test per `MockPaymentGateway` (uno per metodo dell'interfaccia)
- Verificare che tutti i test esistenti passino ancora
- Non servono integration test in questa fase (la DI wiring verra' testata in T-05)

## Criteri di completamento

- [ ] `IPaymentGateway` interface definita in `Seed.Application/Common/Interfaces/` con tutti e 6 i metodi
- [ ] 4 DTO (CreateCheckoutRequest, SubscriptionDetails, SyncPlanRequest, ProductSyncResult) creati come sealed records in `Seed.Application/Common/Models/`
- [ ] `MockPaymentGateway` implementato in `Seed.Infrastructure/Services/Payments/` con dati fake deterministici e logging
- [ ] Registrazione DI condizionale in `DependencyInjection.cs`: MockPaymentGateway quando modulo abilitato e provider non e' Stripe configurato
- [ ] 6 unit test per MockPaymentGateway tutti verdi
- [ ] `dotnet build Seed.slnx` compila senza errori
- [ ] `dotnet test Seed.slnx` passa tutti i test (nuovi ed esistenti)

## Risultato

- **File creati:**
  - `backend/src/Seed.Application/Common/Models/CreateCheckoutRequest.cs` — sealed record con 7 proprietà
  - `backend/src/Seed.Application/Common/Models/SubscriptionDetails.cs` — sealed record con 8 proprietà
  - `backend/src/Seed.Application/Common/Models/SyncPlanRequest.cs` — sealed record con 7 proprietà
  - `backend/src/Seed.Application/Common/Models/ProductSyncResult.cs` — sealed record con 3 proprietà
  - `backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs` — interfaccia con 6 metodi
  - `backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs` — implementazione mock con logging
  - `backend/tests/Seed.UnitTests/Services/MockPaymentGatewayTests.cs` — 6 test unitari

- **File modificati:**
  - `backend/src/Seed.Infrastructure/DependencyInjection.cs` — aggiunta registrazione condizionale di `IPaymentGateway` → `MockPaymentGateway` quando il modulo pagamenti è abilitato ma il provider non è "Stripe" o la SecretKey è vuota

- **Scelte implementative e motivazioni:**
  - `MockPaymentGateway` segue lo stesso pattern di `ConsoleEmailService`: primary constructor con `ILogger`, `LogWarning` per segnalare che si sta usando il mock
  - I metodi `SyncPlanToProviderAsync` preservano gli ID esistenti se forniti (`ExistingMonthlyPriceId`, `ExistingYearlyPriceId`, `ProductId`), generando mock ID solo quando null — questo rende il mock più realistico per i test
  - La registrazione DI usa `StringComparison.OrdinalIgnoreCase` per il confronto del provider, coerente con le convenzioni .NET per confronti di configurazione
  - I DTO usano `sealed record` come richiesto dal piano, garantendo immutabilità e value semantics

- **Eventuali deviazioni dal piano:** Nessuna deviazione. Tutti i file, metodi, e test corrispondono esattamente al piano
