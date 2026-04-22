# Subscription Payments

This document covers how to configure, develop against, and troubleshoot the subscription payments module. It is the primary reference for anyone working with Stripe integration, plan management, or feature gating.

## Overview del modulo

The payments module is **opt-in**: it is disabled by default and activated entirely via configuration. When disabled, no payment-related routes, UI, or Stripe logic is loaded.

### Architecture

```
User → Checkout → Stripe → Webhook → StripeWebhookController
                                             ↓
                                   StripeWebhookEventHandler
                                             ↓
                                    DB update (UserSubscription)
                                             ↓
                                    Email notification (fire-and-forget)

Fallback path (if webhook is delayed/failing):
User → Checkout success page (`session_id`) → `POST /billing/checkout/confirm` → Stripe API verification → DB update
```

**Backend layers:**

| Component | Location | Role |
|---|---|---|
| `IPaymentGateway` | `Seed.Application/Common/Interfaces/IPaymentGateway.cs` | Abstraction for checkout, portal, plan sync |
| `StripePaymentGateway` | `Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs` | Stripe implementation |
| `MockPaymentGateway` | `Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs` | Dev/test stub — no Stripe account required |
| `StripeWebhookController` | `Seed.Api/Controllers/StripeWebhookController.cs` | Receives Stripe webhook POST, validates signature |
| `StripeWebhookEventHandler` | `Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs` | Processes events: creates/updates/cancels subscriptions, sends emails |
| `PaymentsModuleConvention` | `Seed.Api/Conventions/PaymentsModuleConvention.cs` | Removes billing controllers from the app model at startup when disabled |
| `ConfigController` | `Seed.Api/Controllers/ConfigController.cs` | `GET /api/v1.0/config` — exposes `{ paymentsEnabled: bool }` anonymously |

**Webhook events handled:**

| Stripe event | Action |
|---|---|
| `checkout.session.completed` | Create `UserSubscription`, send confirmation email |
| `invoice.payment_succeeded` | Update billing period, restore `Active` if was `PastDue` |
| `invoice.payment_failed` | Set status to `PastDue` |
| `customer.subscription.updated` | Sync status, period, trial end, and plan changes |
| `customer.subscription.deleted` | Set status to `Canceled`, send cancellation email |
| `customer.subscription.trial_will_end` | Send trial-ending notification email |

Webhook events are **idempotent**: each `eventId` is now persisted in `ProcessedWebhookEvents` (DB unique index) and also cached in memory for fast duplicate skips. Email sending is **fire-and-forget**: SMTP failures are logged but never block webhook processing.

Checkout sessions are now persisted in `CheckoutSessionAttempts` with statuses (`Pending`, `Completed`, `Failed`, `Expired`). This enables:
- prevention of duplicate checkout starts while a recent pending checkout exists,
- server-side fallback confirmation from the success page,
- admin monitoring of stale pending checkouts and recent webhook failures.

**Frontend:**

- `ConfigService` (`projects/app/src/app/services/config.service.ts`) loads `GET /api/v1.0/config` via `APP_INITIALIZER`, setting the `paymentsEnabled` signal before any route guard runs. Falls back to `false` on HTTP error.
- `paymentsEnabledGuard` (`projects/app/src/app/guards/payments-enabled.guard.ts`) protects all billing and pricing routes — redirects to `/` when the module is disabled.

### Invoice request workflow (manual invoice)

Invoice requests are now always linked to a concrete subscription reference so the admin can issue documents without manual lookup.

- Request payload includes `userSubscriptionId` (required).
- Backend validates that the subscription belongs to the authenticated user.
- Backend blocks duplicates for the same billing transaction (prefers `StripeInvoiceId`, falls back to `StripePaymentIntentId`).
- On creation, backend stores a snapshot on `InvoiceRequest`:
  - `serviceName` (plan name)
  - `servicePeriodStart`
  - `servicePeriodEnd`
  - `userSubscriptionId`
- Backend also stores payment snapshot details for admin invoicing:
  - `stripeInvoiceId`, `stripePaymentIntentId`
  - `currency`, `amountSubtotal`, `amountTax`, `amountTotal`, `amountPaid`
  - proration metadata: `isProrationApplied`, `prorationAmount`, `billingReason`
  - invoice period: `invoicePeriodStart`, `invoicePeriodEnd`
- User and admin detail screens show this purchase reference in the invoice request detail dialog.

This keeps the request auditable even if the subscription changes later.

---

## Come attivare il modulo

All configuration lives in `appsettings.json` (or environment-specific overrides).

### Parametri

```json
{
  "Modules": {
    "Payments": {
      "Enabled": false,
      "Provider": ""
    }
  },
  "Stripe": {
    "SecretKey": "",
    "PublishableKey": "",
    "WebhookSecret": ""
  }
}
```

| Key | Type | Default | Description |
|---|---|---|---|
| `Modules:Payments:Enabled` | bool | `false` | Master switch. Set to `true` to activate the module. |
| `Modules:Payments:Provider` | string | `""` | Payment gateway to use. Accepted values: `"Stripe"`, `"Mock"`. |
| `Stripe:SecretKey` | string | `""` | Stripe secret key (`sk_test_...` or `sk_live_...`). Required when Provider is `"Stripe"`. |
| `Stripe:PublishableKey` | string | `""` | Stripe publishable key (`pk_test_...` or `pk_live_...`). Sent to frontend via API or embedded in config. |
| `Stripe:WebhookSecret` | string | `""` | Webhook signing secret (`whsec_...`). Used to verify incoming webhook requests. |

When `Modules:Payments:Enabled` is `false`:
- Billing, webhook, admin plans, and pricing controllers are **never registered** (removed by `PaymentsModuleConvention` at startup).
- Swagger does not expose any payment endpoints.
- `GET /api/v1.0/config` returns `{ "paymentsEnabled": false }`.
- Frontend hides all pricing and billing UI; guards redirect to `/`.

---

## Setup Stripe

### 1. Creare un account Stripe

Go to [https://dashboard.stripe.com/register](https://dashboard.stripe.com/register) and create an account.

### 2. Recuperare le API keys

In the Stripe Dashboard → **Developers → API keys**:
- **Publishable key** (`pk_test_...`) — safe to expose to the frontend.
- **Secret key** (`sk_test_...`) — keep server-side only, never commit.

Test keys (prefixed `_test_`) work without real payments. Switch to live keys (`pk_live_...`, `sk_live_...`) only for production.

### 3. Configurare il Customer Portal

The Customer Portal allows users to manage their subscriptions (cancel, update payment method, download invoices) without custom UI.

In the Stripe Dashboard → **Settings → Billing → Customer portal**:
- Enable the portal.
- Configure which actions users can perform (cancel subscription, update payment method, etc.).
- Save the portal URL — it is generated dynamically per-user via `IPaymentGateway.CreatePortalSessionAsync`.

### 4. Configurare il webhook endpoint

In the Stripe Dashboard → **Developers → Webhooks → Add endpoint**:
- **URL:** `https://your-domain.com/webhooks/stripe`
- **Events to listen to:**
  - `checkout.session.completed`
  - `invoice.payment_succeeded`
  - `invoice.payment_failed`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
  - `customer.subscription.trial_will_end`

After saving, copy the **Signing secret** (`whsec_...`) and set it as `Stripe:WebhookSecret`.

Official Stripe docs: [https://docs.stripe.com/webhooks](https://docs.stripe.com/webhooks)

---

## Sviluppo locale

### Setup minimo con Stripe test keys

Add to `backend/src/Seed.Api/appsettings.Development.json`:

```json
{
  "Modules": {
    "Payments": {
      "Enabled": true,
      "Provider": "Stripe"
    }
  },
  "Stripe": {
    "SecretKey": "sk_test_YOUR_KEY",
    "PublishableKey": "pk_test_YOUR_KEY",
    "WebhookSecret": "whsec_YOUR_LOCAL_SECRET"
  }
}
```

### Stripe CLI — forward webhooks in locale

Install Stripe CLI: [https://docs.stripe.com/stripe-cli](https://docs.stripe.com/stripe-cli)

```bash
# Login (once)
stripe login

# Forward events to local API
stripe listen --forward-to localhost:5000/webhooks/stripe
```

The CLI prints a webhook signing secret (`whsec_...`) — use this as `Stripe:WebhookSecret` for local development. It changes each time you run `stripe listen`.

### Test cards

| Card number | Scenario |
|---|---|
| `4242 4242 4242 4242` | Successful payment |
| `4000 0000 0000 0341` | Declined (charge fails) |
| `4000 0025 0000 3155` | Requires authentication (3D Secure) |

Use any future expiry date and any 3-digit CVC.

### Trigger eventi manualmente

```bash
# Simulate a completed checkout
stripe trigger checkout.session.completed

# Simulate a subscription update
stripe trigger customer.subscription.updated

# Simulate a trial ending soon
stripe trigger customer.subscription.trial_will_end
```

### MockPaymentGateway (senza account Stripe)

If you do not have a Stripe account or want fully offline development, set `Provider` to `"Mock"`:

```json
{
  "Modules": {
    "Payments": {
      "Enabled": true,
      "Provider": "Mock"
    }
  }
}
```

`MockPaymentGateway` returns stub responses for checkout, portal, and plan sync operations. No Stripe keys are required. Webhooks are not triggered — subscription state must be manipulated directly in the database for testing.

---

## Ambiente staging/produzione

### Switch to live keys

Replace test keys with live keys in your production secrets (environment variables or secrets manager — **never commit**):

```
Stripe__SecretKey=sk_live_...
Stripe__PublishableKey=pk_live_...
Stripe__WebhookSecret=whsec_...
Modules__Payments__Enabled=true
Modules__Payments__Provider=Stripe
```

### Webhook endpoint con URL pubblica

Create a separate webhook endpoint in the Stripe Dashboard pointing to your production URL:
- `https://your-domain.com/webhooks/stripe`

Copy the signing secret for this endpoint and set it as `Stripe__WebhookSecret` in production config.

### Checklist pre-go-live

- [ ] Live API keys configured in production secrets (not in code or `appsettings.json`)
- [ ] Webhook endpoint registered in Stripe Dashboard with live signing secret
- [ ] All 6 webhook events selected (see Setup Stripe section)
- [ ] Customer Portal configured and tested with a real card
- [ ] SMTP configured so subscription emails are delivered (see `docs/smtp-configuration.md`)
- [ ] At least one plan created and synced to Stripe (`SyncPlanToProviderAsync` called)
- [ ] Stripe Dashboard → **Developers → Logs** verified — no errors on test transactions
- [ ] Stripe Radar rules reviewed (default fraud protection is active)

### Monitoring

Use the Stripe Dashboard → **Developers → Webhooks** to view webhook delivery history, payload, and retry status. Failed webhooks are retried automatically by Stripe for up to 72 hours.

Application-level logs (Serilog / Seq) include structured entries for every processed event:

```
[INF] Subscription created for user {UserId}, plan {PlanId}
[INF] Payment succeeded for subscription {SubscriptionId}
[WRN] Payment failed for subscription {SubscriptionId}
[INF] Subscription {SubscriptionId} updated, status: {Status}
[INF] Subscription {SubscriptionId} canceled
[INF] Trial will end soon for subscription {SubscriptionId}
```

---

## Troubleshooting

### Webhook non ricevuto

**Symptom:** Checkout completes on Stripe but no subscription is created in the DB.

**Check:**
1. Is `stripe listen` running locally? The CLI must be active to forward events.
2. Is `Stripe:WebhookSecret` set to the **CLI signing secret** (not the Dashboard one) during local dev? The two are different.
3. Check Stripe Dashboard → Developers → Webhooks → your endpoint → delivery attempts. If status is `Failed`, inspect the response body.
4. Check application logs for `[ERR]` entries in `StripeWebhookController` or `StripeWebhookEventHandler`.

---

### Subscription non aggiornata dopo pagamento

**Symptom:** Payment succeeds in Stripe but `UserSubscription.Status` stays unchanged.

**Check:**
1. Verify that `invoice.payment_succeeded` is in the list of subscribed events on the webhook endpoint.
2. Check the webhook delivery history in the Stripe Dashboard — look for a failed delivery.
3. Search app logs for `invoice.payment_succeeded` — if the log line is missing, the event was not received.
4. Check that `StripeSubscriptionId` in the `UserSubscriptions` table matches the Stripe subscription ID in the invoice.

If the checkout success page receives `session_id`, the frontend calls `POST /api/v1.0/billing/checkout/confirm` as a fallback reconciliation. This endpoint validates the Stripe session server-side and synchronizes `UserSubscription` if the webhook has not updated the DB yet.

For Stripe Customer Portal changes (plan switch/cancel), the billing UI can call `POST /api/v1.0/billing/subscription/sync` to force a user-scoped reconciliation from Stripe before rendering current subscription details.

---

### Customer Portal non si apre

**Symptom:** Clicking "Manage subscription" returns an error or blank page.

**Check:**
1. The Customer Portal must be enabled in Stripe Dashboard → Settings → Billing → Customer portal.
2. The user must have a `StripeCustomerId` in `UserSubscriptions`. If it is null, the portal session cannot be created.
3. Check application logs for `[ERR]` from `BillingController` / `IPaymentGateway.CreatePortalSessionAsync`.
4. Confirm `Stripe:SecretKey` is valid and has not expired or been revoked.

---

### Checkout failure / pagamento rifiutato

**Symptom:** User reaches the Stripe Checkout page but the payment is declined.

**Check:**
1. In test mode, use test card `4242 4242 4242 4242` (successful) or `4000 0000 0000 0341` (forced decline) to reproduce specific scenarios.
2. Check Stripe Dashboard → Payments for the decline reason (e.g., `card_declined`, `insufficient_funds`).
3. If the user is stuck after 3D Secure, ensure your return URL is correctly configured in the checkout session.
4. For live payments, Stripe Radar may be blocking the transaction — review rules in Dashboard → Radar.

---

## Come aggiungere un nuovo piano

1. Go to **Admin → Plans** in the application admin UI.
2. Create a new plan with name, price (monthly/yearly), and the features it includes.
3. Click **Sync to provider** — this calls `SyncPlanToProviderAsync` on `IPaymentGateway`, which creates (or updates) a Stripe Product and two Prices (monthly + yearly) and stores the resulting `StripePriceIdMonthly` and `StripePriceIdYearly` on the plan record.
4. The plan is now available in the `/pricing` page and can be selected during checkout.

**Archiving a plan:** Mark the plan as inactive in the admin UI. The plan is archived in Stripe (no new subscriptions can be created) but existing subscribers on that plan are unaffected.

---

## Come proteggere un endpoint con plan/feature guard

### Backend — attributi

```csharp
// Require an active "Pro" subscription
[RequiresPlan("Pro")]
[HttpGet("export")]
public IActionResult ExportData() { ... }

// Require a specific feature flag on the user's plan
[RequiresFeature("api-access")]
[HttpGet("api/data")]
public IActionResult ApiData() { ... }

// Allow multiple plans
[RequiresPlan("Pro", "Enterprise")]
[HttpPost("advanced")]
public IActionResult AdvancedAction() { ... }
```

`RequiresPlanAttribute` is in `Seed.Api/Authorization/RequiresPlanAttribute.cs`.
`RequiresFeatureAttribute` is in `Seed.Api/Authorization/RequiresFeatureAttribute.cs`.

When the payments module is disabled, these authorization policies still exist but the underlying handler resolves them as **pass-through** (no subscription data = no restriction), so existing endpoints continue to work without a subscription.

### Frontend — direttiva strutturale

```html
<!-- Show element only if user has "Pro" plan -->
<button *requiresPlan="'Pro'" (click)="export()">Export</button>

<!-- Allow multiple plans -->
<div *requiresPlan="['Pro', 'Enterprise']">Advanced settings</div>
```

`RequiresPlanDirective` is in `shared-auth/src/lib/directives/requires-plan.directive.ts`. It reacts to subscription state changes via Angular signals — no manual change detection needed.

### Frontend — route guard

```typescript
// In your route definition
{
  path: 'advanced',
  component: AdvancedComponent,
  canActivate: [requiresPlanGuard('Pro')],
}

// Multiple plans
{
  path: 'enterprise-feature',
  component: EnterpriseComponent,
  canActivate: [requiresPlanGuard('Pro', 'Enterprise')],
}
```

`requiresPlanGuard` is in `shared-auth/src/lib/guards/requires-plan.guard.ts`. It redirects to `/pricing` when the user does not have the required plan.

All billing and pricing routes also have `paymentsEnabledGuard` applied, which redirects to `/` when the module is disabled — so plan guards are never reached when payments are off.
