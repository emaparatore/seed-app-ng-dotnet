# Task 06: Webhook handler endpoint and event processing

## Contesto

- **Domain entities** già esistenti: `UserSubscription` (con `StripeSubscriptionId`, `StripeCustomerId`, `Status`, `CurrentPeriodStart/End`, `TrialEnd`, `CanceledAt`), `SubscriptionPlan` (con `StripeProductId`, `StripePriceIdMonthly/Yearly`)
- **Enums** già definiti: `SubscriptionStatus` (Active, Trialing, PastDue, Canceled, Expired), `PlanStatus`, `BillingInterval`
- **EF Core DbSets** già configurati in `ApplicationDbContext`: `UserSubscriptions`, `SubscriptionPlans`
- **StripeSettings** in `Seed.Shared/Configuration/StripeSettings.cs` include `WebhookSecret`
- **StripePaymentGateway** in `Seed.Infrastructure/Services/Payments/` usa `StripeClient` via constructor (non statico)
- **IPaymentGateway** in `Seed.Application/Common/Interfaces/` — non include metodi webhook (separazione corretta)
- **AuditActions.cs** in `Seed.Domain/Authorization/` — non ha ancora azioni subscription/webhook
- **IAuditService** accetta `(action, entityType, entityId?, details?, userId?, ipAddress?, userAgent?, ct)`
- **DI condizionale** in `Infrastructure/DependencyInjection.cs` — pattern: `if (configuration.IsPaymentsModuleEnabled()) { ... }`
- **Controller pattern**: `[ApiController]`, primary constructor injection, `ISender` per MediatR, `Result<T>` pattern
- **Integration tests**: `CustomWebApplicationFactory` con Testcontainers PostgreSQL, `IClassFixture<>`
- **Stripe.net 47.4.0** già referenziato in `Seed.Infrastructure.csproj`
- Dipendenze completate: T-02 (entities), T-03 (EF config), T-05 (StripePaymentGateway)

## Piano di esecuzione

### Step 1: Aggiungere audit actions per subscription/webhook

**File:** `backend/src/Seed.Domain/Authorization/AuditActions.cs`

Aggiungere costanti:
```csharp
// Subscription & Payments
public const string SubscriptionCreated = "SubscriptionCreated";
public const string SubscriptionUpdated = "SubscriptionUpdated";
public const string SubscriptionCanceled = "SubscriptionCanceled";
public const string SubscriptionPaymentSucceeded = "SubscriptionPaymentSucceeded";
public const string SubscriptionPaymentFailed = "SubscriptionPaymentFailed";
public const string WebhookReceived = "WebhookReceived";
public const string WebhookVerificationFailed = "WebhookVerificationFailed";
```

### Step 2: Creare IWebhookEventHandler interface

**File da creare:** `backend/src/Seed.Application/Common/Interfaces/IWebhookEventHandler.cs`

```csharp
public interface IWebhookEventHandler
{
    Task<bool> ProcessEventAsync(string eventId, string eventType, string jsonPayload, CancellationToken ct = default);
}
```

Restituisce `true` se l'evento è stato processato (o era duplicato), `false` se non riconosciuto. L'interfaccia resta Stripe-agnostica a livello Application.

### Step 3: Creare StripeWebhookEventHandler

**File da creare:** `backend/src/Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs`

Classe sealed con dipendenze:
- `ApplicationDbContext` (query e update diretti su `UserSubscriptions`, `SubscriptionPlans`)
- `IAuditService` (logging eventi)
- `ILogger<StripeWebhookEventHandler>`

**Idempotency:** Usare `IMemoryCache` con chiave `webhook:{eventId}`, TTL 24h. Check all'inizio di `ProcessEventAsync`: se già presente, return `true` (skip). Dopo processing con successo, aggiungere al cache.

**Event handlers interni (metodi privati):**

1. **`checkout.session.completed`**:
   - Estrarre `Session` da `Event.Data.Object`
   - Trovare user tramite metadata `userId` (impostato nel checkout) o email
   - Trovare plan tramite metadata `planId` o Stripe price ID → match su `StripePriceIdMonthly`/`StripePriceIdYearly`
   - Creare `UserSubscription` con Status=Active (o Trialing se trial), `StripeSubscriptionId`, `StripeCustomerId`
   - Audit log: `SubscriptionCreated`

2. **`invoice.payment_succeeded`**:
   - Estrarre `Invoice` da event
   - Trovare `UserSubscription` via `StripeSubscriptionId` (campo `invoice.subscription`)
   - Aggiornare `CurrentPeriodStart`, `CurrentPeriodEnd` dal period dell'invoice
   - Se status era PastDue → set Active
   - Audit log: `SubscriptionPaymentSucceeded`

3. **`invoice.payment_failed`**:
   - Trovare `UserSubscription` via `StripeSubscriptionId`
   - Set Status = PastDue
   - Audit log: `SubscriptionPaymentFailed`

4. **`customer.subscription.updated`**:
   - Estrarre `Subscription` da event
   - Trovare `UserSubscription` via `StripeSubscriptionId`
   - Aggiornare Status (map Stripe status → enum), `CurrentPeriodStart/End`, `PlanId` se cambiato
   - Audit log: `SubscriptionUpdated`

5. **`customer.subscription.deleted`**:
   - Trovare `UserSubscription` via `StripeSubscriptionId`
   - Set Status = Canceled, `CanceledAt` = UtcNow
   - Audit log: `SubscriptionCanceled`

6. **`customer.subscription.trial_will_end`**:
   - Solo logging (audit + ILogger). Nessun cambio entity.

**Mapping Stripe status → SubscriptionStatus:**
- `active` → Active
- `trialing` → Trialing
- `past_due` → PastDue
- `canceled` → Canceled
- `unpaid`/`incomplete_expired` → Expired

### Step 4: Creare StripeWebhookController

**File da creare:** `backend/src/Seed.Api/Controllers/StripeWebhookController.cs`

```csharp
[ApiController]
[Route("webhooks/stripe")]
public class StripeWebhookController(
    IWebhookEventHandler webhookHandler,
    IOptions<StripeSettings> stripeSettings,
    IAuditService auditService,
    ILogger<StripeWebhookController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> HandleWebhook(CancellationToken ct)
    {
        // 1. Leggere raw body
        using var reader = new StreamReader(HttpContext.Request.Body);
        var json = await reader.ReadToEndAsync(ct);
        
        // 2. Validare firma Stripe
        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                stripeSettings.Value.WebhookSecret);
        }
        catch (StripeException)
        {
            await auditService.LogAsync(AuditActions.WebhookVerificationFailed, ...);
            return BadRequest();
        }
        
        // 3. Audit log evento ricevuto
        await auditService.LogAsync(AuditActions.WebhookReceived, ...);
        
        // 4. Dispatch a handler
        await webhookHandler.ProcessEventAsync(stripeEvent.Id, stripeEvent.Type, json, ct);
        
        // 5. Sempre 200 (Stripe riprova su non-2xx)
        return Ok();
    }
}
```

**Note:**
- Niente `[Authorize]` — endpoint pubblico
- Niente `[ApiVersion]` — webhook non è una API versioned per i client
- Route `/webhooks/stripe` senza prefix `api/v1.0` — è un endpoint infrastrutturale
- Ritorna sempre 200 per eventi non riconosciuti (evita retry inutili di Stripe)

### Step 5: Registrare servizi in DI

**File:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`

Nel blocco `if (configuration.IsPaymentsModuleEnabled())`:
- `services.AddMemoryCache()` (se non già registrato)
- `services.AddScoped<IWebhookEventHandler, StripeWebhookEventHandler>()`

### Step 6: Unit tests per StripeWebhookEventHandler

**File da creare:** `backend/tests/Seed.UnitTests/Infrastructure/Services/Payments/StripeWebhookEventHandlerTests.cs`

Usare NSubstitute per mock di `ApplicationDbContext` (o creare InMemory DbContext), `IAuditService`, `IMemoryCache`.

Test cases:
1. `ProcessEventAsync_CheckoutSessionCompleted_CreatesSubscription`
2. `ProcessEventAsync_InvoicePaymentSucceeded_UpdatesPeriodDates`
3. `ProcessEventAsync_InvoicePaymentFailed_SetsStatusPastDue`
4. `ProcessEventAsync_SubscriptionUpdated_UpdatesStatusAndPeriod`
5. `ProcessEventAsync_SubscriptionDeleted_SetsStatusCanceled`
6. `ProcessEventAsync_TrialWillEnd_LogsOnly`
7. `ProcessEventAsync_DuplicateEvent_SkipsProcessing` (idempotency)
8. `ProcessEventAsync_UnknownEventType_ReturnsFalse`

**Nota:** Poiché `StripeWebhookEventHandler` lavora direttamente con `ApplicationDbContext` e tipi Stripe, i test useranno InMemory database provider per il DbContext e costruiranno `Event` objects manualmente usando JSON deserialization.

### Step 7: Integration test per webhook endpoint

**File da creare:** `backend/tests/Seed.IntegrationTests/Webhooks/StripeWebhookControllerTests.cs`

Test cases:
1. `PostWebhook_InvalidSignature_Returns400`
2. `PostWebhook_ValidSignature_Returns200` (richiede generazione firma test con `EventUtility.GenerateTestSignature` o webhook secret noto)

**Setup:** Configurare `CustomWebApplicationFactory` con payments module enabled e webhook secret noto per i test. Potrebbe richiedere override in `ConfigureTestServices`.

## Criteri di completamento

- [ ] Audit actions per subscription/webhook aggiunti in `AuditActions.cs`
- [ ] `IWebhookEventHandler` interface creata in `Seed.Application/Common/Interfaces/`
- [ ] `StripeWebhookEventHandler` implementato in `Seed.Infrastructure/Services/Payments/` con gestione dei 6 event types
- [ ] Idempotency implementata (eventi duplicati ignorati via `IMemoryCache`)
- [ ] `StripeWebhookController` creato con endpoint `POST /webhooks/stripe`
- [ ] Validazione firma Stripe funzionante
- [ ] HTTP 200 per eventi non riconosciuti, HTTP 400 per firma invalida
- [ ] Servizi registrati condizionalmente in DI
- [ ] Unit tests per event handler logic (almeno 8 test)
- [ ] Integration test per webhook endpoint (firma valida/invalida)
- [ ] Solution compila: `dotnet build Seed.slnx`
- [ ] Tutti i test passano: `dotnet test Seed.slnx`

## Risultato

### File modificati/creati

**Modificati:**
- `backend/src/Seed.Domain/Authorization/AuditActions.cs` — aggiunte 7 costanti per subscription/webhook (già presenti prima dell'esecuzione)
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione `IWebhookEventHandler` e `AddMemoryCache()` nel blocco payments (già presente prima dell'esecuzione)
- `backend/src/Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs` — fix: `Events.` → `EventTypes.` per compatibilità con Stripe.net 47.4.0

**Creati:**
- `backend/src/Seed.Application/Common/Interfaces/IWebhookEventHandler.cs` — interfaccia Stripe-agnostica (già presente prima dell'esecuzione)
- `backend/src/Seed.Api/Controllers/StripeWebhookController.cs` — endpoint `POST /webhooks/stripe` (già presente prima dell'esecuzione)
- `backend/tests/Seed.UnitTests/Services/Payments/StripeWebhookEventHandlerTests.cs` — 15 unit tests (8 handler + 1 idempotency + 6 MapStripeStatus)
- `backend/tests/Seed.IntegrationTests/Webhooks/StripeWebhookControllerTests.cs` — 2 integration tests (firma valida/invalida)
- `backend/tests/Seed.IntegrationTests/Webhooks/WebhookWebApplicationFactory.cs` — factory custom con payments module e webhook secret noto

### Scelte implementative e motivazioni

1. **`EventTypes` invece di `Events`**: Stripe.net v47.4.0 usa `Stripe.EventTypes` per le costanti dei tipi di evento, non `Stripe.Events` che non esiste. Corretto nel handler esistente.
2. **JSON test con campi obbligatori**: `EventUtility.ParseEvent()` in Stripe.net v47 richiede `livemode`, `pending_webhooks`, e `request` nel JSON per deserializzare correttamente. I JSON builder nei test includono questi campi.
3. **`WebhookWebApplicationFactory`**: Factory separata che estende `CustomWebApplicationFactory` per abilitare il modulo payments con webhook secret noto, evitando di modificare la factory condivisa.
4. **InMemory database per unit tests**: Usato `UseInMemoryDatabase` per i test del handler, evitando dipendenze esterne. I test verificano logica di business, non query SQL.
5. **15 unit tests** (oltre gli 8 richiesti): aggiunti test per `MapStripeStatus` (6 Theory) e `PastDueBecomesActive` come caso edge aggiuntivo.

### Deviazioni dal piano

- **Nessuna deviazione significativa**. Steps 1-5 erano già implementati (presenti come file untracked/modified). L'unico fix necessario è stato `Events.` → `EventTypes.` nel handler per compilare con Stripe.net 47.4.0.
