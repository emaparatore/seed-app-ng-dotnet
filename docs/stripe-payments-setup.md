 # Guida Operativa: Configurazione Pagamenti e Stripe

Questo documento contiene tutte le istruzioni operative per configurare i pagamenti e Stripe nel progetto, per gli ambienti **Development**, **Staging** e **Produzione**.

---

## Panoramica dell'Architettura

Il modulo pagamenti è **opt-in**: è disabilitato di default e si attiva esclusivamente tramite configurazione. Quando disabilitato, nessuna route, UI o logica Stripe viene caricata.

### Flusso architetturale

```
Utente → Checkout → Stripe → Webhook → StripeWebhookController
                                              ↓
                                    StripeWebhookEventHandler
                                              ↓
                                    Update DB (UserSubscription)
                                              ↓
                                    Email notifica (fire-and-forget)
```

### Componenti Backend

| Componente | Percorso | Ruolo |
|---|---|---|
| `IPaymentGateway` | `Seed.Application/Common/Interfaces/IPaymentGateway.cs` | Astrazione per checkout, portal, sync piani |
| `StripePaymentGateway` | `Seed.Infrastructure/Services/Payments/StripePaymentGateway.cs` | Implementazione Stripe |
| `MockPaymentGateway` | `Seed.Infrastructure/Services/Payments/MockPaymentGateway.cs` | Stub dev/test — nessun account Stripe richiesto |
| `StripeWebhookController` | `Seed.Api/Controllers/StripeWebhookController.cs` | Riceve POST webhook Stripe, valida signature |
| `StripeWebhookEventHandler` | `Seed.Infrastructure/Services/Payments/StripeWebhookEventHandler.cs` | Processa eventi: crea/aggiorna/cancella subscription, invia email |
| `PaymentsModuleConvention` | `Seed.Api/Conventions/PaymentsModuleConvention.cs` | Rimuove i controller billing dall'app model quando disabilitato |
| `ConfigController` | `Seed.Api/Controllers/ConfigController.cs` | `GET /api/v1.0/config` — espone `{ paymentsEnabled: bool }` anonimamente |

### Componenti Frontend

| Componente | Percorso | Ruolo |
|---|---|---|
| `ConfigService` | `projects/app/src/app/services/config.service.ts` | Carica `GET /api/v1.0/config` via `APP_INITIALIZER`, imposta signal `paymentsEnabled` |
| `paymentsEnabledGuard` | `projects/app/src/app/guards/payments-enabled.guard.ts` | Protegge route billing/pricing — redirect a `/` se modulo disabilitato |
| `BillingService` | `projects/app/src/app/pages/pricing/billing.service.ts` | Servizio per checkout, subscription, portal, invoice |
| `requiresPlanGuard` | `projects/shared-auth/src/lib/guards/requires-plan.guard.ts` | Route guard basata sul piano |
| `RequiresPlanDirective` | `projects/shared-auth/src/lib/directives/requires-plan.directive.ts` | Direttiva strutturale per mostrare/nascondere elementi in base al piano |

### Eventi Webhook gestiti

| Evento Stripe | Azione |
|---|---|
| `checkout.session.completed` | Crea `UserSubscription`, invia email conferma |
| `invoice.payment_succeeded` | Aggiorna periodo fatturazione, ripristina `Active` se era `PastDue` |
| `invoice.payment_failed` | Imposta status a `PastDue` |
| `customer.subscription.updated` | Sync status, periodo, trial end, cambio piano |
| `customer.subscription.deleted` | Imposta status a `Canceled`, invia email cancellazione |
| `customer.subscription.trial_will_end` | Invia notifica scadenza trial |

**Idempotenza:** ogni `eventId` è cachato per 24 ore per evitare duplicati.
**Email:** l'invio è fire-and-forget — errori SMTP sono loggati ma non bloccano il webhook.

---

## Configurazione — Tutti gli Ambienti

### Chiavi di configurazione

Tutta la configurazione vive in `appsettings.json` (o override tramite variabili d'ambiente).

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

| Chiave | Tipo | Default | Descrizione |
|---|---|---|---|
| `Modules:Payments:Enabled` | bool | `false` | Switch principale. Impostare `true` per attivare il modulo. |
| `Modules:Payments:Provider` | string | `""` | Gateway da usare. Valori accettati: `"Stripe"`, `"Mock"`. |
| `Stripe:SecretKey` | string | `""` | Chiave segreta Stripe (`sk_test_...` o `sk_live_...`). Richiesta quando Provider è `"Stripe"`. |
| `Stripe:PublishableKey` | string | `""` | Chiave pubblica Stripe (`pk_test_...` o `pk_live_...`). Envio al frontend via API. |
| `Stripe:WebhookSecret` | string | `""` | Signing secret webhook (`whsec_...`). Usato per verificare le richieste webhook in ingresso. |

### Cosa succede quando `Modules:Payments:Enabled` è `false`:

- I controller Billing, Webhook, Admin Plans e Pricing **non vengono registrati** (rimossi da `PaymentsModuleConvention` a startup).
- Swagger non espone endpoint payment.
- `GET /api/v1.0/config` restituisce `{ "paymentsEnabled": false }`.
- Il frontend nasconde tutta l'UI pricing/billing; le guard redirectano a `/`.

---

## Ambiente di Sviluppo Locale

### Opzione A: Stripe Test Keys (consigliata)

Questa configurazione permette di testare l'intero flusso con Stripe in modalità test, senza transazioni reali.

#### 1. Creare un account Stripe

Andare su [https://dashboard.stripe.com/register](https://dashboard.stripe.com/register) e creare un account.

#### 2. Recuperare le API keys test

Nella Dashboard Stripe → **Developers → API keys**:
- **Publishable key** (`pk_test_...`) — sicuro da esporre al frontend.
- **Secret key** (`sk_test_...`) — mantenere solo lato server, **mai committare**.

#### 3. Configurare il progetto

Aggiungere al file `backend/src/Seed.Api/appsettings.Development.json`:

```json
{
  "Modules": {
    "Payments": {
      "Enabled": true,
      "Provider": "Stripe"
    }
  },
  "Stripe": {
    "SecretKey": "sk_test_LA_TUA_CHIAVE",
    "PublishableKey": "pk_test_LA_TUA_CHIAVE",
    "WebhookSecret": "whsec_UN_SECRET_LOCALE"
  }
}
```

#### 4. Stripe CLI — forward webhook in locale

Installare Stripe CLI: [https://docs.stripe.com/stripe-cli](https://docs.stripe.com/stripe-cli)

```bash
# Login (una sola volta)
stripe login

# Forward eventi all'API locale
stripe listen --forward-to localhost:5000/webhooks/stripe
```

La CLI stampa un webhook signing secret (`whsec_...`) — usarlo come `Stripe:WebhookSecret` nella configurazione locale. **Questo secret cambia ogni volta che si riavvia `stripe listen`.**

> **Nota:** La porta `5000` va adattata alla porta effettiva su cui gira il backend locale. Se usi Docker Compose dev, l'endpoint è `localhost:5035` (come configurato in `docker/docker-compose.yml`):
> ```bash
> stripe listen --forward-to localhost:5035/webhooks/stripe
> ```

#### 5. Configurare il webhook endpoint su Stripe Dashboard

In Stripe Dashboard → **Developers → Webhooks → Add endpoint**:
- **URL:** `https://your-ngrok-url.com/webhooks/stripe` (se usi ngrok) oppure lasciar gestire alla CLI
- **Eventi da sottoscrivere:**
  - `checkout.session.completed`
  - `invoice.payment_succeeded`
  - `invoice.payment_failed`
  - `customer.subscription.updated`
  - `customer.subscription.deleted`
  - `customer.subscription.trial_will_end`

Dopo il salvataggio, copiare il **Signing secret** (`whsec_...`) e impostarlo come `Stripe:WebhookSecret`.

Documentazione ufficiale Stripe: [https://docs.stripe.com/webhooks](https://docs.stripe.com/webhooks)

#### 6. Carte di test

| Numero carta | Scenario |
|---|---|
| `4242 4242 4242 4242` | Pagamento completato con successo |
| `4000 0000 0000 0341` | Rifiutato (charge fallisce) |
| `4000 0025 0000 3155` | Richiede autenticazione (3D Secure) |

Usare qualsiasi data di scadenza futura e qualsiasi CVC a 3 cifre.

#### 7. Trigger eventi manualmente

```bash
# Simulare un checkout completato
stripe trigger checkout.session.completed

# Simulare un aggiornamento subscription
stripe trigger customer.subscription.updated

# Simulare un trial in scadenza
stripe trigger customer.subscription.trial_will_end
```

---

### Opzione B: MockPaymentGateway (senza account Stripe)

Se non si dispone di un account Stripe o si vuole uno sviluppo completamente offline, impostare `Provider` a `"Mock"`:

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

`MockPaymentGateway` restituisce risposte stub per checkout, portal e sync piani. **Nessuna chiave Stripe è richiesta.** I webhook non vengono triggerati — lo stato delle subscription va manipolato direttamente nel database per i test.

> **Nota:** `MockPaymentGateway` viene automaticamente registrato dal DI container quando `Modules:Payments:Provider` è diverso da `"Stripe"` o quando `Stripe:SecretKey` non è valido. Vedi `Seed.Infrastructure/DependencyInjection.cs`.

---

### Sviluppo locale con Docker Compose

Lo stack dev include già tutti i servizi necessari:

```bash
cd docker/
docker compose up
```

Servizi inclusi:
- **API** (.NET watch) su `localhost:5035`
- **PostgreSQL** su `localhost:5432`
- **Mailpit** (SMTP locale) su `localhost:8025` — per testare le email delle subscription
- **Seq** (log viewer) su `localhost:8081`

Per abilitare i pagamenti in Docker dev:

1. Configurare `appsettings.Development.json` come descritto sopra (Opzione A o B)
2. Avviare Stripe CLI in un terminale separato:
   ```bash
   stripe listen --forward-to localhost:5035/webhooks/stripe
   ```
3. Le email di conferma/cancellazione subscription appariranno su `http://localhost:8025`

---

## Ambiente Staging

### Prerequisiti

- Account Stripe con accesso alla dashboard
- Server con Docker e Docker Compose installati
- Chiavi Stripe **test** (consigliato per staging) o **live** (se si vuole testare in produzione simulata)

### 1. Configurare le variabili d'ambiente

Copiare il file template e configurare:

```bash
cd docker/
cp .env.prod.example .env
```

Aggiungere le variabili Stripe nel file `.env`:

```env
# --- Stripe (Staging) ---
Stripe__SecretKey=sk_test_LA_TUA_CHIAVE_TEST
Stripe__PublishableKey=pk_test_LA_TUA_CHIAVE_TEST
Stripe__WebhookSecret=whsec_IL_TUO_SIGNING_SECRET

# --- Abilitazione modulo pagamenti ---
Modules__Payments__Enabled=true
Modules__Payments__Provider=Stripe
```

> **Importante:** ASP.NET Core usa `__` (doppio underscore) come separatore per le sezioni nested nelle variabili d'ambiente.

### 2. Configurare il webhook endpoint

In Stripe Dashboard → **Developers → Webhooks → Add endpoint**:
- **URL:** `https://staging.yourdomain.com/webhooks/stripe`
- **Eventi:** tutti e 6 elencati sopra
- Copiare il **Signing secret** e impostarlo come `Stripe__WebhookSecret` nel file `.env`

### 3. Deploy

```bash
cd docker/
docker compose -f docker-compose.deploy.yml up -d
```

### 4. Verifica

1. Controllare che l'API sia healthy:
   ```bash
   curl https://staging.yourdomain.com/health/ready
   ```
2. Verificare che `GET https://staging.yourdomain.com/api/v1.0/config` restituisca `"paymentsEnabled": true`
3. Testare un checkout con la carta `4242 4242 4242 4242`
4. Verificare in Stripe Dashboard → **Developers → Logs** che il webhook sia stato ricevuto correttamente

---

## Ambiente di Produzione

### Prerequisiti

- Account Stripe verificato e attivo
- Dominio configurato con HTTPS certificato
- SMTP configurato (vedi [smtp-configuration.md](smtp-configuration.md))
- Chiavi Stripe **live**

### 1. Switch a chiavi live

Sostituire le chiavi test con quelle live nel file `.env` di produzione:

```env
# --- Stripe (Production) ---
Stripe__SecretKey=sk_live_LA_TUA_CHIAVE_LIVE
Stripe__PublishableKey=pk_live_LA_TUA_CHIAVE_LIVE
Stripe__WebhookSecret=whsec_IL_TUO_SIGNING_SECRET_LIVE

# --- Abilitazione modulo pagamenti ---
Modules__Payments__Enabled=true
Modules__Payments__Provider=Stripe
```

> **⚠️ SICUREZZA:** Le chiavi live **non devono mai** essere committate nel repository. Usare esclusivamente variabili d'ambiente o un secrets manager.

### 2. Configurare il webhook endpoint live

In Stripe Dashboard → **Developers → Webhooks → Add endpoint**:
- **URL:** `https://yourdomain.com/webhooks/stripe`
- **Eventi:** tutti e 6 elencati sopra
- Copiare il **Signing secret** per questo endpoint live e impostarlo come `Stripe__WebhookSecret`

### 3. Configurare il Customer Portal

In Stripe Dashboard → **Settings → Billing → Customer portal**:
- Abilitare il portal
- Configurare le azioni consentite (cancellare subscription, aggiornare metodo di pagamento, ecc.)
- Il portal URL viene generato dinamicamente per utente via `IPaymentGateway.CreatePortalSessionAsync`

### 4. Checklist pre-go-live

- [ ] Chiavi live API configurate nei secrets di produzione (non nel codice o `appsettings.json`)
- [ ] Webhook endpoint registrato in Stripe Dashboard con signing secret live
- [ ] Tutti e 6 gli eventi webhook selezionati (vedi sezione Setup Stripe)
- [ ] Customer Portal configurato e testato con una carta reale
- [ ] SMTP configurato per l'invio delle email di subscription (vedi [smtp-configuration.md](smtp-configuration.md))
- [ ] Almeno un piano creato e sincronizzato su Stripe (`SyncPlanToProviderAsync` chiamato dall'Admin UI)
- [ ] Stripe Dashboard → **Developers → Logs** verificato — nessun errore su transazioni test
- [ ] Stripe Radar rules revisionati (la protezione antifrode default è attiva)
- [ ] HTTPS attivo e certificato valido sul dominio
- [ ] Backup database configurato prima del go-live

### 5. Deploy

```bash
cd docker/
docker compose -f docker-compose.deploy.yml up -d
```

### 6. Monitoring

Usare Stripe Dashboard → **Developers → Webhooks** per visualizzare:
- Cronologia delivery webhook
- Payload e stato di ogni evento
- Retry automatici (Stripe retry per fino a 72 ore)

A livello applicativo, i log strutturati (Serilog / Seq) includono entry per ogni evento processato:

```
[INF] Subscription created for user {UserId}, plan {PlanId}
[INF] Payment succeeded for subscription {SubscriptionId}
[WRN] Payment failed for subscription {SubscriptionId}
[INF] Subscription {SubscriptionId} updated, status: {Status}
[INF] Subscription {SubscriptionId} canceled
[INF] Trial will end soon for subscription {SubscriptionId}
```

Accesso Seq: `http://localhost:8081` (dev) oppure `https://yourdomain.com:8081` (production/staging).

---

## Gestione dei Piani (Plans)

### Come aggiungere un nuovo piano

1. Andare su **Admin → Plans** nell'UI di amministrazione
2. Creare un nuovo piano con: nome, prezzo (mensile/annuale), e feature incluse
3. Cliccare **Sync to provider** — questo chiama `SyncPlanToProviderAsync` su `IPaymentGateway`, che crea (o aggiorna) un Stripe Product e due Prices (monthly + yearly) e salva `StripePriceIdMonthly` e `StripePriceIdYearly` sul record del piano
4. Il piano è ora disponibile nella pagina `/pricing` e selezionabile durante il checkout

### Come archiviare un piano

Impostare il piano come **inactive** dall'Admin UI. Il piano viene archiviato in Stripe (nessuna nuova subscription può essere creata) ma gli subscriber esistenti su quel piano non sono interessati.

---

## Autorizzazione basata su Piano/Feature

### Backend — Attributi

```csharp
// Richiedere una subscription "Pro" attiva
[RequiresPlan("Pro")]
[HttpGet("export")]
public IActionResult ExportData() { ... }

// Richiedere un feature flag specifico sul piano dell'utente
[RequiresFeature("api-access")]
[HttpGet("api/data")]
public IActionResult ApiData() { ... }

// Consentire più piani
[RequiresPlan("Pro", "Enterprise")]
[HttpPost("advanced")]
public IActionResult AdvancedAction() { ... }
```

`RequiresPlanAttribute`: `Seed.Api/Authorization/RequiresPlanAttribute.cs`
`RequiresFeatureAttribute`: `Seed.Api/Authorization/RequiresFeatureAttribute.cs`

Quando il modulo pagamenti è disabilitato, queste policy funzionano come **pass-through** (nessuna restrizione).

### Frontend — Direttiva strutturale

```html
<!-- Mostra elemento solo se utente ha piano "Pro" -->
<button *requiresPlan="'Pro'" (click)="export()">Export</button>

<!-- Consentire più piani -->
<div *requiresPlan="['Pro', 'Enterprise']">Advanced settings</div>
```

`RequiresPlanDirective`: `shared-auth/src/lib/directives/requires-plan.directive.ts`

### Frontend — Route guard

```typescript
// Nella definizione delle route
{
  path: 'advanced',
  component: AdvancedComponent,
  canActivate: [requiresPlanGuard('Pro')],
}

// Più piani
{
  path: 'enterprise-feature',
  component: EnterpriseComponent,
  canActivate: [requiresPlanGuard('Pro', 'Enterprise')],
}
```

`requiresPlanGuard`: `shared-auth/src/lib/guards/requires-plan.guard.ts` — redirecta a `/pricing` se l'utente non ha il piano richiesto.

---

## Troubleshooting

### Webhook non ricevuto

**Sintomo:** Checkout completato su Stripe ma nessuna subscription creata nel DB.

**Verificare:**
1. `stripe listen` è in esecuzione locale? La CLI deve essere attiva per forwardare eventi.
2. `Stripe:WebhookSecret` è impostato al **signing secret della CLI** (non quello della Dashboard) durante lo sviluppo locale? I due sono diversi.
3. Controllare Stripe Dashboard → Developers → Webhooks → il tuo endpoint → delivery attempts. Se status è `Failed`, ispezionare il corpo della risposta.
4. Controllare i log applicativi per entry `[ERR]` in `StripeWebhookController` o `StripeWebhookEventHandler`.

---

### Subscription non aggiornata dopo pagamento

**Sintomo:** Pagamento completato su Stripe ma `UserSubscription.Status` rimane invariato.

**Verificare:**
1. Verificare che `invoice.payment_succeeded` sia nella lista degli eventi sottoscritti sul webhook endpoint.
2. Controllare la cronologia delivery webhook in Stripe Dashboard — cercare una delivery fallita.
3. Cercare nei log dell'app per `invoice.payment_succeeded` — se la riga manca, l'evento non è stato ricevuto.
4. Verificare che `StripeSubscriptionId` nella tabella `UserSubscriptions` corrisponda all'ID subscription Stripe nella invoice.

---

### Customer Portal non si apre

**Sintomo:** Cliccando "Manage subscription" restituisce errore o pagina bianca.

**Verificare:**
1. Il Customer Portal deve essere abilitato in Stripe Dashboard → Settings → Billing → Customer portal.
2. L'utente deve avere un `StripeCustomerId` in `UserSubscriptions`. Se è null, la portal session non può essere creata.
3. Controllare i log per `[ERR]` da `BillingController` / `IPaymentGateway.CreatePortalSessionAsync`.
4. Confermare che `Stripe:SecretKey` sia valida e non sia stata revocata o scaduta.

---

### Checkout failure / pagamento rifiutato

**Sintomo:** L'utente raggiunge la pagina Stripe Checkout ma il pagamento viene rifiutato.

**Verificare:**
1. In modalità test, usare la carta `4242 4242 4242 4242` (successo) o `4000 0000 0000 0341` (rifiuto forzato) per riprodurre scenari specifici.
2. Controllare Stripe Dashboard → Payments per il motivo del rifiuto (es. `card_declined`, `insufficient_funds`).
3. Se l'utente è bloccato dopo 3D Secure, verificare che il return URL sia configurato correttamente nella checkout session.
4. Per i pagamenti live, Stripe Radar potrebbe bloccare la transazione — rivedere le regole in Dashboard → Radar.

---

### Email di conferma/cancellazione non inviate

**Sintomo:** Subscription creata correttamente ma l'utente non riceve email di conferma.

**Verificare:**
1. SMTP è configurato correttamente? Vedi [smtp-configuration.md](smtp-configuration.md).
2. In dev con Docker, controllare Mailpit su `http://localhost:8025`.
3. Controllare i log Seq per errori SMTP — sono loggati ma non bloccano il webhook.
4. Verificare che l'utente abbia un indirizzo email valido nel record `Users`.

---

## Riepilogo configurazione per ambiente

| Impostazione | Development | Staging | Production |
|---|---|---|---|
| **Stripe keys** | `sk_test_...` / `pk_test_...` | `sk_test_...` / `pk_test_...` (o `sk_live_...` per test avanzati) | `sk_live_...` / `pk_live_...` |
| **Webhook secret** | Da `stripe listen` CLI (`whsec_...`) | Da Stripe Dashboard endpoint staging | Da Stripe Dashboard endpoint production |
| **Provider** | `Stripe` o `Mock` | `Stripe` | `Stripe` |
| **Enabled** | `true` (per testare) | `true` | `true` |
| **Configurazione** | `appsettings.Development.json` | Variabili d'ambiente `.env` | Variabili d'ambiente `.env` |
| **Webhook URL** | `localhost:5035/webhooks/stripe` (via CLI) | `https://staging.yourdomain.com/webhooks/stripe` | `https://yourdomain.com/webhooks/stripe` |
| **SMTP** | Mailpit (`localhost:8025`) | Brevo (stesso account prod, sender diverso) | Brevo |
| **Customer Portal** | Opzionale (test locale) | Configurato in Stripe Dashboard | Configurato e testato |

---

## Riferimenti correlati

- [smtp-configuration.md](smtp-configuration.md) — Configurazione SMTP per email transazionali
- [docs/vps-setup-guide.md](vps-setup-guide.md) — Guida al deployment su VPS
- Documentazione ufficiale Stripe: [https://docs.stripe.com](https://docs.stripe.com)
- Stripe Webhooks: [https://docs.stripe.com/webhooks](https://docs.stripe.com/webhooks)
- Stripe CLI: [https://docs.stripe.com/stripe-cli](https://docs.stripe.com/stripe-cli)
