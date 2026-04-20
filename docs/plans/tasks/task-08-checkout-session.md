# Task 08: Checkout flow — create checkout session

## Contesto

- **Stato attuale:** Phase 1 (infra) è completa: domain entities, EF Core config, IPaymentGateway + Mock + Stripe, webhook handler, e public plans API sono tutti Done. Non esiste ancora nessun command in `Seed.Application/Billing/Commands/` né un `BillingController`.
- **Dipendenze completate:** T-04 (IPaymentGateway), T-05 (StripePaymentGateway), T-07 (GetPlans public API)
- **Pattern di riferimento:** I command esistenti (es. `LoginCommand`, `ChangePasswordCommand`) usano `sealed record : IRequest<Result<T>>` con proprietà `[JsonIgnore]` per UserId/IpAddress/UserAgent, handler separato, e `AbstractValidator<T>`.
- **Webhook compatibility:** `StripeWebhookEventHandler.HandleCheckoutSessionCompletedAsync` si aspetta metadata con chiavi `"userId"` e `"planId"` (Guid come stringa).
- **StripeCustomerId:** È su `UserSubscription.StripeCustomerId`, non su `ApplicationUser`. Per la prima checkout, l'utente potrebbe non avere ancora un customer Stripe — va creato con `IPaymentGateway.CreateCustomerAsync()`.
- **DI registration:** L'handler va registrato manualmente in `DependencyInjection.cs` dentro il blocco `IsPaymentsModuleEnabled()`, come fatto per `GetPlansQueryHandler`.

## Piano di esecuzione

### File da creare

1. **`backend/src/Seed.Application/Billing/Commands/CreateCheckoutSession/CreateCheckoutSessionCommand.cs`**
   - `sealed record CreateCheckoutSessionCommand(Guid PlanId, BillingInterval BillingInterval, string SuccessUrl, string CancelUrl) : IRequest<Result<CheckoutSessionResponse>>`
   - Proprietà `[JsonIgnore]`: `UserId` (Guid), `IpAddress` (string?), `UserAgent` (string?)

2. **`backend/src/Seed.Application/Billing/Commands/CreateCheckoutSession/CreateCheckoutSessionCommandValidator.cs`**
   - FluentValidation `AbstractValidator<CreateCheckoutSessionCommand>`
   - Regole: PlanId not empty, BillingInterval è valore valido dell'enum, SuccessUrl/CancelUrl not empty e formato URL valido

3. **`backend/src/Seed.Application/Billing/Models/CheckoutSessionResponse.cs`**
   - `sealed record CheckoutSessionResponse(string CheckoutUrl)`

4. **`backend/src/Seed.Infrastructure/Billing/Commands/CreateCheckoutSessionCommandHandler.cs`**
   - Implementa `IRequestHandler<CreateCheckoutSessionCommand, Result<CheckoutSessionResponse>>`
   - Inject: `ApplicationDbContext`, `UserManager<ApplicationUser>`, `IPaymentGateway`, `IAuditService`
   - Logica:
     1. Recupera il plan da DB, verifica `Status == Active` e non `IsFreeTier`
     2. Seleziona il PriceId corretto (Monthly/Yearly) dal plan; fallisci se null
     3. Recupera l'utente via `UserManager.FindByIdAsync()`
     4. Cerca un `StripeCustomerId` esistente nell'ultima `UserSubscription` dell'utente
     5. Se non esiste, chiama `IPaymentGateway.CreateCustomerAsync(email, name)`
     6. Costruisci `CreateCheckoutRequest` con: priceId, customerEmail, customerId, successUrl, cancelUrl, trialDays (se plan.TrialDays > 0), metadata `{"userId": ..., "planId": ...}`
     7. Chiama `IPaymentGateway.CreateCheckoutSessionAsync(request)`
     8. Log audit con `IAuditService.LogAsync()` — nuovo action `AuditActions.CheckoutSessionCreated`
     9. Ritorna `Result<CheckoutSessionResponse>.Success(new(checkoutUrl))`

5. **`backend/src/Seed.Api/Controllers/BillingController.cs`**
   - Route: `api/v{version:apiVersion}/billing`
   - `[Authorize]`, `[ApiVersion("1.0")]`
   - Pattern: primary constructor con `ISender sender`
   - Helper properties: `CurrentUserId`, `IpAddress`, `UserAgent` (come `AdminSettingsController`)
   - Endpoint: `[HttpPost("checkout")]` — riceve `CreateCheckoutSessionCommand`, enrichisce con UserId/IpAddress/UserAgent, invia via MediatR, ritorna Ok(url) o BadRequest(errors)

### File da modificare

6. **`backend/src/Seed.Domain/Authorization/AuditActions.cs`**
   - Aggiungere: `public const string CheckoutSessionCreated = "CheckoutSessionCreated";`

7. **`backend/src/Seed.Infrastructure/DependencyInjection.cs`**
   - Nel blocco `IsPaymentsModuleEnabled()`, aggiungere registrazione handler:
     ```csharp
     services.AddScoped<IRequestHandler<CreateCheckoutSessionCommand, Result<CheckoutSessionResponse>>, CreateCheckoutSessionCommandHandler>();
     ```

### Test da scrivere

8. **`backend/tests/Seed.UnitTests/Billing/Commands/CreateCheckoutSessionCommandHandlerTests.cs`**
   - Test con MockPaymentGateway (o NSubstitute mock di IPaymentGateway):
     - ✅ Piano attivo con prezzo monthly → ritorna checkout URL
     - ✅ Piano attivo con prezzo yearly → usa StripePriceIdYearly
     - ✅ Piano inesistente → failure
     - ✅ Piano non attivo (Archived) → failure
     - ✅ Piano IsFreeTier → failure (non si fa checkout per il free tier)
     - ✅ PriceId null per l'intervallo scelto → failure
     - ✅ Utente senza StripeCustomerId → crea customer e usa l'ID
     - ✅ Utente con StripeCustomerId esistente → riutilizza l'ID
     - ✅ Piano con TrialDays > 0 → checkout request include trial days

9. **`backend/tests/Seed.UnitTests/Billing/Commands/CreateCheckoutSessionCommandValidatorTests.cs`**
   - ✅ PlanId vuoto → validation failure
   - ✅ SuccessUrl vuoto → validation failure
   - ✅ CancelUrl vuoto → validation failure
   - ✅ Dati validi → nessun errore

## Criteri di completamento

- [ ] `CreateCheckoutSessionCommand`, handler, e validator creati e funzionanti
- [ ] `CheckoutSessionResponse` DTO creato
- [ ] `BillingController` creato con endpoint `POST /api/v1.0/billing/checkout` (auth required)
- [ ] Customer Stripe creato per utenti senza StripeCustomerId; riutilizzato se esistente
- [ ] PriceId corretto selezionato in base a BillingInterval (Monthly/Yearly)
- [ ] Trial period incluso quando plan.TrialDays > 0
- [ ] Metadata `userId` e `planId` passati nella checkout request (compatibilità webhook)
- [ ] `AuditActions.CheckoutSessionCreated` aggiunto e usato nel handler
- [ ] Handler registrato in DI dentro blocco `IsPaymentsModuleEnabled()`
- [ ] Unit test handler (9 casi) e validator (4 casi) passano
- [ ] `dotnet build Seed.slnx` e `dotnet test Seed.slnx` passano senza errori

## Risultato

### File creati
- `backend/src/Seed.Application/Billing/Commands/CreateCheckoutSession/CreateCheckoutSessionCommand.cs` — sealed record con PlanId, BillingInterval, SuccessUrl, CancelUrl + proprietà JsonIgnore per UserId/IpAddress/UserAgent
- `backend/src/Seed.Application/Billing/Commands/CreateCheckoutSession/CreateCheckoutSessionCommandValidator.cs` — FluentValidation: PlanId NotEmpty, BillingInterval IsInEnum, SuccessUrl/CancelUrl NotEmpty + formato URL valido
- `backend/src/Seed.Application/Billing/Models/CheckoutSessionResponse.cs` — sealed record con CheckoutUrl
- `backend/src/Seed.Infrastructure/Billing/Commands/CreateCheckoutSessionCommandHandler.cs` — handler completo con logica: verifica plan attivo e non free tier, selezione PriceId per intervallo, lookup/creazione StripeCustomerId, costruzione checkout request con metadata e trial days, audit log
- `backend/src/Seed.Api/Controllers/BillingController.cs` — controller con [Authorize], POST checkout endpoint, enrichment UserId/IpAddress/UserAgent via primary constructor pattern
- `backend/tests/Seed.UnitTests/Billing/Commands/CreateCheckoutSessionCommandHandlerTests.cs` — 9 test cases handler
- `backend/tests/Seed.UnitTests/Billing/Commands/CreateCheckoutSessionCommandValidatorTests.cs` — 4 test cases validator

### File modificati
- `backend/src/Seed.Domain/Authorization/AuditActions.cs` — aggiunto `CheckoutSessionCreated`
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrato handler nel blocco `IsPaymentsModuleEnabled()`

### Scelte implementative e motivazioni
- **InMemoryDatabase per test handler**: usato per simulare DbContext senza dipendenze esterne, coerente con l'approccio unit test del progetto
- **NSubstitute per UserManager e IPaymentGateway**: permette di verificare le chiamate (Received/DidNotReceive) e controllare i valori di ritorno
- **Lookup StripeCustomerId da UserSubscription**: segue il piano — cerca l'ultimo StripeCustomerId dall'ultima subscription dell'utente, crea un nuovo customer solo se non ne esiste uno
- **Pattern primary constructor per BillingController**: coerente con AdminSettingsController e altri controller del progetto
- **Helper properties CurrentUserId/IpAddress/UserAgent**: stesso pattern di AdminSettingsController

### Deviazioni dal piano
- Nessuna deviazione. Tutti i file, la logica e i test sono stati implementati esattamente come descritto nel mini-plan.
