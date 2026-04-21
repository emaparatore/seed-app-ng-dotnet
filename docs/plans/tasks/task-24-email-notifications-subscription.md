# Task 24: Email notifications for subscription events

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| US-006 | Trial period | T-08, T-15 | ✅ Done |
| (Trasversale) | DA-1: Modulo attivabile via configurazione | T-01, T-23 | ✅ Done |

### Dipendenze (da 'Depends on:')
T-06: Webhook handler endpoint and event processing -
**Implementation Notes:**
- Used `Stripe.EventTypes` constants instead of `Stripe.Events` (which doesn't exist in Stripe.net v47.4.0)
- `WebhookWebApplicationFactory` extends `CustomWebApplicationFactory` with payments module enabled and a known webhook secret, avoiding modifications to the shared factory
- Unit tests use InMemory database provider for `ApplicationDbContext` to test business logic without external dependencies
- JSON test payloads include `livemode`, `pending_webhooks`, and `request` fields required by `EventUtility.ParseEvent()` in Stripe.net v47
- `IWebhookEventHandler` interface kept Stripe-agnostic in `Seed.Application`, implementation in `Seed.Infrastructure`

### Convenzioni da task Done correlati
- `ConsoleEmailService` follows pattern: primary constructor with `ILogger`, `LogWarning` to signal console fallback usage (from T-01/T-04 pattern, visible in existing `ConsoleEmailService`)
- `SmtpEmailService` uses `IOptions<SmtpSettings>`, builds `MimeMessage` with HTML body, connects/authenticates/sends/disconnects via MailKit (existing pattern)
- `IEmailService` interface lives in `Seed.Application/Common/Interfaces/IEmailService.cs`
- Both email implementations live in `Seed.Infrastructure/Services/`
- `StripeWebhookEventHandler` uses primary constructor with `ApplicationDbContext`, `IAuditService`, `IMemoryCache`, `ILogger` — all injected via DI
- Unit tests for webhook handler use InMemory database, `NSubstitute` for `IAuditService`, real `MemoryCache`
- Webhook handler resolves user by `userId` from session metadata, and subscription by `StripeSubscriptionId` from DB
- `HandleTrialWillEnd` currently just logs — needs to be upgraded to send email

### Riferimenti
- `docs/requirements/FEAT-3.md` — FEAT-3 requirements
- `docs/smtp-configuration.md` — SMTP auto-switch (console fallback), Gmail dev setup, Brevo production

## Stato attuale del codice
- `backend/src/Seed.Application/Common/Interfaces/IEmailService.cs` — interface with 2 methods: `SendPasswordResetEmailAsync`, `SendEmailVerificationAsync`
- `backend/src/Seed.Infrastructure/Services/SmtpEmailService.cs` — SMTP implementation using MailKit, primary constructor with `IOptions<SmtpSettings>` + `ILogger<SmtpEmailService>`
- `backend/src/Seed.Infrastructure/Services/ConsoleEmailService.cs` — console fallback, primary constructor with `ILogger<ConsoleEmailService>`, logs with `LogWarning` then `LogInformation`
- `backend/src/Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs` — handles 6 event types, `HandleTrialWillEnd` is a no-op (just logs)
- `backend/tests/Seed.UnitTests/Services/Payments/StripeWebhookEventHandlerTests.cs` — 15 existing tests using InMemory DB + NSubstitute
- Webhook handler does NOT currently have `IEmailService` injected
- To get user email from a subscription event, the handler needs to query `ApplicationUser` (IdentityUser<Guid>) by `UserId` from the `UserSubscription` entity. `ApplicationUser.Email` is inherited from `IdentityUser`.
- To get plan name, handler needs to Include/query `SubscriptionPlan` by `PlanId` from `UserSubscription`. `SubscriptionPlan.Name` is the display name.

## Piano di esecuzione

### Step 1: Add 3 new methods to `IEmailService`
**File:** `backend/src/Seed.Application/Common/Interfaces/IEmailService.cs`
- `SendSubscriptionConfirmationAsync(string toEmail, string planName, CancellationToken ct = default)`
- `SendTrialEndingNotificationAsync(string toEmail, string planName, int daysRemaining, CancellationToken ct = default)`
- `SendSubscriptionCanceledAsync(string toEmail, string planName, DateTime endDate, CancellationToken ct = default)`

### Step 2: Implement in `SmtpEmailService`
**File:** `backend/src/Seed.Infrastructure/Services/SmtpEmailService.cs`
- Follow existing pattern: build `MimeMessage` with HTML body, connect/authenticate/send/disconnect
- Reuse the same SMTP connection pattern as `SendPasswordResetEmailAsync`
- HTML templates should be simple inline HTML (matching existing style — no external template engine)

### Step 3: Implement in `ConsoleEmailService`
**File:** `backend/src/Seed.Infrastructure/Services/ConsoleEmailService.cs`
- Follow existing pattern: `LogWarning("SMTP not configured — logging email to console")` then `LogInformation` with relevant data
- Return `Task.CompletedTask`

### Step 4: Inject `IEmailService` into `StripeWebhookEventHandler` and call from relevant handlers
**File:** `backend/src/Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs`
- Add `IEmailService emailService` to primary constructor
- In `HandleCheckoutSessionCompletedAsync`: after saving subscription, query user email and plan name, call `SendSubscriptionConfirmationAsync`
- In `HandleTrialWillEnd`: change from sync no-op to async — parse event, find subscription + user + plan, call `SendTrialEndingNotificationAsync` with `daysRemaining` (from `TrialEnd - UtcNow`). Trial will end sends 3 days before by default from Stripe.
- In `HandleSubscriptionDeletedAsync`: after saving, query user email and plan name, call `SendSubscriptionCanceledAsync` with `CurrentPeriodEnd` as endDate
- Email sending should be fire-and-forget style (catch exceptions and log, don't fail the webhook). Wrap each call in try/catch so email failure doesn't break webhook processing.

### Step 5: Update unit tests
**File:** `backend/tests/Seed.UnitTests/Services/Payments/StripeWebhookEventHandlerTests.cs`
- Add `IEmailService` mock (`NSubstitute`) to test setup — update constructor call
- Add test: `CheckoutSessionCompleted_SendsConfirmationEmail` — verify `SendSubscriptionConfirmationAsync` called with correct email/planName
- Add test: `TrialWillEnd_SendsTrialEndingEmail` — need to seed a user + subscription + plan, verify `SendTrialEndingNotificationAsync` called
- Add test: `SubscriptionDeleted_SendsCanceledEmail` — verify `SendSubscriptionCanceledAsync` called with correct endDate
- Add test: `EmailFailure_DoesNotBreakWebhookProcessing` — configure email mock to throw, verify handler still returns true

**File:** `backend/tests/Seed.UnitTests/Services/` (new file if needed, or add to existing)
- Unit tests for `ConsoleEmailService` subscription methods (verify logging)
- No tests needed for `SmtpEmailService` (requires real SMTP, covered by integration tests or manual testing)

### Step 6: Verify build and all tests pass
```bash
dotnet build backend/Seed.slnx
dotnet test backend/Seed.slnx
```

## Criteri di completamento
- [x] Three email methods added to interface and both implementations
- [x] Webhook handlers send emails on relevant events
- [x] Console fallback logs email content
- [x] Unit tests for email service methods

## Risultato

### File modificati
- `backend/src/Seed.Application/Common/Interfaces/IEmailService.cs` — aggiunti 3 nuovi metodi: `SendSubscriptionConfirmationAsync`, `SendTrialEndingNotificationAsync`, `SendSubscriptionCanceledAsync`
- `backend/src/Seed.Infrastructure/Services/SmtpEmailService.cs` — implementati i 3 nuovi metodi con MimeMessage HTML inline, riutilizzando lo stesso pattern SMTP
- `backend/src/Seed.Infrastructure/Services/ConsoleEmailService.cs` — implementati i 3 nuovi metodi con `LogWarning` + `LogInformation` seguendo il pattern esistente
- `backend/src/Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs` — iniettato `IEmailService`; aggiunto invio email in `HandleCheckoutSessionCompletedAsync`, `HandleSubscriptionDeletedAsync`; `HandleTrialWillEnd` (sync) rimpiazzato con `HandleTrialWillEndAsync` (async) che ora parsa il payload, trova user+plan e invia notifica trial; tutte le chiamate email in try/catch fire-and-forget
- `backend/tests/Seed.UnitTests/Services/Payments/StripeWebhookEventHandlerTests.cs` — aggiunto mock `IEmailService`, `SeedTestUser()` helper, 4 nuovi test (`CheckoutSessionCompleted_SendsConfirmationEmail`, `TrialWillEnd_SendsTrialEndingEmail`, `SubscriptionDeleted_SendsCanceledEmail`, `EmailFailure_DoesNotBreakWebhookProcessing`), builder `BuildSubscriptionEventJsonWithTrialEnd`

### Scelte chiave
- `HandleTrialWillEnd` convertito da sync bool a async Task<bool> per poter fare query DB e inviare email; il switch in `ProcessEventAsync` aggiornato con `await`
- Email sending è fire-and-forget (try/catch per ogni chiamata) — un errore SMTP non blocca il processing del webhook
- `daysRemaining` calcolato da `stripeSub.TrialEnd - UtcNow`, con fallback a 3 se `TrialEnd` è null
- Test usa `SeedTestUser()` in constructor (shared tra tutti i test) per avere un utente disponibile nelle query

### Deviazioni dal piano
- Nessuna deviazione significativa. Il piano era completo e seguito fedelmente.
