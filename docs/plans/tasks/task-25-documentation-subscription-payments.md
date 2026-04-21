# Task 25: Documentation — setup guide, Stripe configuration, testing

## Contesto ereditato dal piano
### Storie coperte
| Story | Description | Tasks | Status |
|-------|-------------|-------|--------|
| (Trasversale) | DA-1: Modulo attivabile via configurazione | T-01, T-23 | ✅ Done |

T-25 is a cross-cutting documentation task covering ALL stories (US-001 through US-012 + trasversali).

### Dipendenze (da 'Depends on:')
**T-23: Conditional module registration — routes and middleware**
Implementation Notes (verbatim):
- `IApplicationModelConvention` (`PaymentsModuleConvention`) removes billing controllers from the application model at build time — routes are never registered and Swagger never exposes them when the module is disabled
- `GET /api/v1.0/config` anonymous endpoint (`ConfigController`) exposes `{ paymentsEnabled: bool }` for unauthenticated and authenticated callers alike
- Frontend `ConfigService` loads config via `APP_INITIALIZER`, guaranteeing `paymentsEnabled` signal is set before route guards execute
- `paymentsEnabledGuard` applied to all billing and pricing routes (including public `/pricing`) — redirects to `/` when module disabled; admin routes also guarded
- Fallback to `false` in `ConfigService` on HTTP error: safe default that hides all payment UI if backend is unreachable

**T-24: Email notifications for subscription events**
Implementation Notes (verbatim):
- `HandleTrialWillEnd` converted from sync `bool` to async `Task<bool>` to enable DB queries and email sending; `ProcessEventAsync` switch updated with `await`
- Email sending is fire-and-forget: each call wrapped in try/catch so SMTP failures never block webhook processing
- `daysRemaining` calculated from `stripeSub.TrialEnd - UtcNow`, with fallback to 3 if `TrialEnd` is null
- Tests use a shared `SeedTestUser()` helper (called in constructor) to ensure a user is available for handler queries
- All 19 unit tests pass (15 existing + 4 new)

### Convenzioni da task Done correlati
From **T-01 (Module toggle system and Stripe configuration)**:
- `StripeSettings` registration follows the same conditional pattern as the existing SMTP block in `DependencyInjection.cs`
- `ModulesSettings` is registered unconditionally to allow injection even when the payments module is disabled
- Extension method `IsPaymentsModuleEnabled` lives in `Seed.Shared/Extensions/ConfigurationExtensions.cs`

From **T-04/T-05 (Payment gateway interface + Stripe impl)**:
- `IPaymentGateway` in `Seed.Application/Common/Interfaces/IPaymentGateway.cs` defines the abstraction
- `MockPaymentGateway` in `Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs` for dev without Stripe
- `StripePaymentGateway` in `Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs`

From **T-12 (Subscription guards)**:
- `[RequiresPlan("Pro")]` attribute in `Seed.Api/Authorization/RequiresPlanAttribute.cs`
- `[RequiresFeature("api-access")]` attribute in `Seed.Api/Authorization/RequiresFeatureAttribute.cs`

From **T-13b (Frontend feature gating)**:
- `*requiresPlan="'Pro'"` directive in `shared-auth/src/lib/directives/requires-plan.directive.ts`
- `requiresPlanGuard` in `shared-auth/src/lib/guards/requires-plan.guard.ts`

### Riferimenti
- `docs/requirements/FEAT-3.md` — full requirements for Subscription Plans & Payments
- `docs/plans/PLAN-5.md` — implementation plan with all task details
- `docs/smtp-configuration.md` — existing email/SMTP doc (referenced for email integration)
- `docs/admin-dashboard.md` — existing admin doc (admin plan management touches this area)

## Stato attuale del codice
- **Configuration POCOs:**
  - `backend/src/Seed.Shared/Configuration/PaymentsModuleSettings.cs` — `Enabled` (bool), `Provider` (string)
  - `backend/src/Seed.Shared/Configuration/StripeSettings.cs` — `SecretKey`, `PublishableKey`, `WebhookSecret` (const `SectionName = "Stripe"`)
- **appsettings.json:** `Modules:Payments` section (Enabled=false, Provider="") and `Stripe` section (empty keys)
- **Module toggle:** `IsPaymentsModuleEnabled` in `Seed.Shared/Extensions/ConfigurationExtensions.cs`
- **DI registration:** `backend/src/Seed.Infrastructure/DependencyInjection.cs` — conditional registration of Stripe/Mock gateway
- **Convention:** `backend/src/Seed.Api/Conventions/PaymentsModuleConvention.cs` — removes billing controllers when disabled
- **Config endpoint:** `backend/src/Seed.Api/Controllers/ConfigController.cs` — `GET /api/v1.0/config` exposes `paymentsEnabled`
- **Webhook controller:** `backend/src/Seed.Api/Controllers/StripeWebhookController.cs`
- **Webhook handler:** `backend/src/Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs`
- **Gateway interface:** `backend/src/Seed.Application/Common/Interfaces/IPaymentGateway.cs`
- **Mock gateway:** `backend/src/Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs`
- **Stripe gateway:** `backend/src/Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs`
- **Backend guards:** `backend/src/Seed.Api/Authorization/RequiresPlanAttribute.cs`, `RequiresFeatureAttribute.cs`, corresponding handlers
- **Frontend guards:** `frontend/web/projects/shared-auth/src/lib/guards/requires-plan.guard.ts`
- **Frontend directive:** `frontend/web/projects/shared-auth/src/lib/directives/requires-plan.directive.ts`
- **Frontend config service:** `frontend/web/projects/app/src/app/services/config.service.ts`
- **Frontend payments guard:** `frontend/web/projects/app/src/app/guards/payments-enabled.guard.ts`
- **No existing `docs/subscription-payments.md`** — file needs to be created from scratch
- **README.md** has a docs index table at lines 213-227 — new doc must be added there
- **CLAUDE.md** has an "Existing docs" list — new doc must be added there

## Piano di esecuzione

### Step 1: Create `docs/subscription-payments.md`
Write the full documentation file with these sections (as specified in T-25's "What to do"):

1. **Overview del modulo** — architecture description: IPaymentGateway abstraction, webhook flow (Stripe → StripeWebhookController → StripeWebhookEventHandler → DB update + email), module toggle (`Modules:Payments:Enabled`), conditional registration via `PaymentsModuleConvention`. Include a text-based flow diagram: Checkout → Stripe → Webhook → Subscription update.

2. **Come attivare il modulo** — document all `appsettings.json` parameters:
   - `Modules:Payments:Enabled` (bool, default: false)
   - `Modules:Payments:Provider` (string, "Stripe" or "Mock")
   - `Stripe:SecretKey` (string)
   - `Stripe:PublishableKey` (string)
   - `Stripe:WebhookSecret` (string)

3. **Setup Stripe** — account creation, API keys (test/live), Customer Portal configuration, webhook endpoint setup (events: `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`, `customer.subscription.trial_will_end`), link to Stripe docs.

4. **Sviluppo locale** — `appsettings.Development.json` with test keys, Stripe CLI install + login, `stripe listen --forward-to localhost:5000/webhooks/stripe`, test cards (4242..., 4000 0000 0000 0341, 4000 0025 0000 3155), manual trigger commands, MockPaymentGateway usage (set Provider to "Mock").

5. **Ambiente staging/produzione** — switch to live keys, webhook endpoint with public URL, signing secret, pre-go-live checklist, monitoring via Stripe Dashboard.

6. **Troubleshooting** — at least 4 scenarios: webhook not received, subscription not updated, Customer Portal not opening, checkout failure.

7. **Come aggiungere un nuovo piano** — via admin UI, what happens on Stripe (Product + Price created via SyncPlanToProviderAsync), archiving a plan.

8. **Come proteggere un endpoint con plan/feature guard** — backend `[RequiresPlan("Pro")]` and `[RequiresFeature("api-access")]` attributes, frontend `*requiresPlan="'Pro'"` directive and `requiresPlanGuard`, behavior when module disabled (guards pass always).

### Step 2: Update `CLAUDE.md`
Add to the "Existing docs" list:
```
- `docs/subscription-payments.md` — Subscription payments module: Stripe setup, module toggle, webhook flow, plan/feature guards, troubleshooting. Read when configuring or developing subscription/payment features.
```

### Step 3: Update `README.md`
Add a row to the docs index table (after GDPR Compliance Checklist line):
```
| [Subscription Payments](docs/subscription-payments.md) | Stripe integration, module toggle, webhook flow, plan guards, troubleshooting |
```

### Step 4: Verify
- Review that all 8 sections are present in the doc
- Verify all file paths and attribute names referenced are accurate
- Ensure CLAUDE.md and README.md updates are consistent
- No tests needed (documentation-only task)

## Criteri di completamento
(Definition of Done, verbatim from PLAN-5.md)
- [x] `docs/subscription-payments.md` creato con tutte le sezioni
- [x] Istruzioni Stripe verificabili (link a docs ufficiali dove appropriato)
- [x] Sezione sviluppo locale testabile passo-passo
- [x] Checklist staging/produzione completa
- [x] Troubleshooting con almeno 4 scenari comuni
- [x] `CLAUDE.md` aggiornato con riferimento al nuovo doc
- [x] Un developer senza contesto può attivare il modulo seguendo solo il doc

## Risultato

### File modificati
- `docs/subscription-payments.md` — **creato** con 8 sezioni complete
- `CLAUDE.md` — aggiunto riferimento a `docs/subscription-payments.md` nella lista "Existing docs"
- `README.md` — aggiunta riga nella tabella docs index (dopo GDPR Compliance Checklist)

### Scelte chiave
- Inclusa tabella dei webhook events gestiti (tutti e 6, inclusi `invoice.payment_succeeded` e `invoice.payment_failed` che il mini-plan non elencava esplicitamente ma il codice gestisce)
- Documentata la semantica fire-and-forget delle email e l'idempotenza via cache (dettagli dal codice reale di `StripeWebhookEventHandler`)
- Sezione `MockPaymentGateway` inclusa nella sezione sviluppo locale con istruzioni self-contained
- Comportamento guards quando il modulo è disabilitato documentato esplicitamente (pass-through backend, redirect frontend)

### Deviazioni dal mini-plan
- Nessuna deviazione sostanziale. I 4 scenari troubleshooting richiesti sono stati rispettati: webhook non ricevuto, subscription non aggiornata, Customer Portal non si apre, checkout failure.
