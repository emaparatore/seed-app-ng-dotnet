# Task 02: Domain entities — Plan, Subscription, InvoiceRequest

## Contesto

- **Stato attuale del codice:** Le entità di dominio vivono in `backend/src/Seed.Domain/Entities/`. Le entità esistenti (ApplicationUser, Permission, AuditLogEntry, etc.) seguono un pattern semplice: classi POCO con proprietà auto-implemented, nessuna base class, Guid come tipo Id, `DateTime.UtcNow` come default per timestamp. Non esiste una directory `Enums/` nel progetto Domain — va creata.
- **ApplicationUser** (`Seed.Domain/Entities/ApplicationUser.cs`) estende `IdentityUser<Guid>` e ha già una collection `RefreshTokens`. Va aggiunta la navigation property `Subscriptions`.
- **Dipendenze:** T-01 (module toggle + Stripe config) è completato. T-03 (EF config + migration) dipende da questo task.

## Piano di esecuzione

### File da creare

1. **`backend/src/Seed.Domain/Enums/PlanStatus.cs`**
   - Enum: `Active`, `Inactive`, `Archived`

2. **`backend/src/Seed.Domain/Enums/SubscriptionStatus.cs`**
   - Enum: `Active`, `Trialing`, `PastDue`, `Canceled`, `Expired`

3. **`backend/src/Seed.Domain/Enums/CustomerType.cs`**
   - Enum: `Individual`, `Company`

4. **`backend/src/Seed.Domain/Enums/InvoiceRequestStatus.cs`**
   - Enum: `Requested`, `InProgress`, `Issued`

5. **`backend/src/Seed.Domain/Enums/BillingInterval.cs`**
   - Enum: `Monthly`, `Yearly` (usato dai task successivi ma è naturale definirlo ora col dominio)

6. **`backend/src/Seed.Domain/Entities/SubscriptionPlan.cs`**
   - Properties: Id (Guid), Name (string), Description (string?), MonthlyPrice (decimal), YearlyPrice (decimal), StripePriceIdMonthly (string?), StripePriceIdYearly (string?), StripeProductId (string?), TrialDays (int), IsFreeTier (bool), IsDefault (bool), IsPopular (bool), Status (PlanStatus), SortOrder (int), CreatedAt (DateTime), UpdatedAt (DateTime)
   - Navigation: `ICollection<PlanFeature> Features`, `ICollection<UserSubscription> Subscriptions`

7. **`backend/src/Seed.Domain/Entities/PlanFeature.cs`**
   - Properties: Id (Guid), PlanId (Guid), Key (string), Description (string), LimitValue (string?), SortOrder (int)
   - Navigation: `SubscriptionPlan Plan`

8. **`backend/src/Seed.Domain/Entities/UserSubscription.cs`**
   - Properties: Id (Guid), UserId (Guid), PlanId (Guid), Status (SubscriptionStatus), StripeSubscriptionId (string?), StripeCustomerId (string?), CurrentPeriodStart (DateTime), CurrentPeriodEnd (DateTime), TrialEnd (DateTime?), CanceledAt (DateTime?), CreatedAt (DateTime), UpdatedAt (DateTime)
   - Navigation: `ApplicationUser User`, `SubscriptionPlan Plan`

9. **`backend/src/Seed.Domain/Entities/InvoiceRequest.cs`**
   - Properties: Id (Guid), UserId (Guid), StripePaymentIntentId (string?), CustomerType (CustomerType), FullName (string), CompanyName (string?), Address (string), City (string), PostalCode (string), Country (string), FiscalCode (string?), VatNumber (string?), SdiCode (string?), PecEmail (string?), Status (InvoiceRequestStatus), CreatedAt (DateTime), UpdatedAt (DateTime), ProcessedAt (DateTime?)
   - Navigation: `ApplicationUser User`

### File da modificare

10. **`backend/src/Seed.Domain/Entities/ApplicationUser.cs`**
    - Aggiungere: `public ICollection<UserSubscription> Subscriptions { get; set; } = [];`
    - Aggiungere: `public ICollection<InvoiceRequest> InvoiceRequests { get; set; } = [];`

### Approccio tecnico

- Seguire le convenzioni esistenti: classi non-sealed, proprietà auto-implemented, defaults con `= string.Empty` per required strings, `= DateTime.UtcNow` per timestamp, `= []` per collections.
- Enums in un nuovo namespace `Seed.Domain.Enums`.
- Non aggiungere logica di validazione nelle entity (la validazione è in Application layer via FluentValidation).
- I campi Stripe (StripeSubscriptionId, StripeCustomerId, StripePriceId*, StripeProductId) sono nullable perché dipendono dal provider esterno.

### Test da scrivere/verificare

- Nessun unit test necessario per questo task: le entità sono pure POCO senza logica di dominio. La DoD nel piano dice "Unit tests for any domain validation logic" — non c'è validation logic in queste entità.
- Verificare che la solution compili: `dotnet build Seed.slnx` da `backend/`.

## Criteri di completamento

- [ ] Tutte le entità create con tipi corretti e nullable annotations
- [ ] Enums creati per i campi Status (PlanStatus, SubscriptionStatus, CustomerType, InvoiceRequestStatus, BillingInterval)
- [ ] Navigation properties impostate correttamente (bidirezionali tra Plan↔Feature, Plan↔Subscription, User↔Subscription, User↔InvoiceRequest)
- [ ] `ApplicationUser` ha la navigation property `Subscriptions` e `InvoiceRequests`
- [ ] Solution compila: `dotnet build Seed.slnx` passa senza errori

## Risultato

### File creati
- `backend/src/Seed.Domain/Enums/PlanStatus.cs` — enum Active, Inactive, Archived
- `backend/src/Seed.Domain/Enums/SubscriptionStatus.cs` — enum Active, Trialing, PastDue, Canceled, Expired
- `backend/src/Seed.Domain/Enums/CustomerType.cs` — enum Individual, Company
- `backend/src/Seed.Domain/Enums/InvoiceRequestStatus.cs` — enum Requested, InProgress, Issued
- `backend/src/Seed.Domain/Enums/BillingInterval.cs` — enum Monthly, Yearly
- `backend/src/Seed.Domain/Entities/SubscriptionPlan.cs` — entità piano con proprietà Stripe, pricing, feature flags
- `backend/src/Seed.Domain/Entities/PlanFeature.cs` — feature associata a un piano (key/value con limit opzionale)
- `backend/src/Seed.Domain/Entities/UserSubscription.cs` — sottoscrizione utente con stato, periodo, dati Stripe
- `backend/src/Seed.Domain/Entities/InvoiceRequest.cs` — richiesta fattura con dati fiscali italiani (SDI, PEC, P.IVA, CF)

### File modificati
- `backend/src/Seed.Domain/Entities/ApplicationUser.cs` — aggiunte navigation properties `Subscriptions` e `InvoiceRequests`

### Scelte implementative e motivazioni
- Navigation properties verso entità padre (es. `Plan`, `User`) inizializzate con `= null!` per seguire il pattern EF Core consigliato (evita null warnings senza creare istanze fittizie)
- Enum `Status` nelle entità inizializzati con valori di default sensati (`PlanStatus.Active`, `SubscriptionStatus.Active`, `InvoiceRequestStatus.Requested`)
- `Description` in `PlanFeature` è required (`string`) perché è un campo sempre visibile nell'UI, a differenza di `Description` in `SubscriptionPlan` che è opzionale
- Creata la directory `Enums/` con namespace `Seed.Domain.Enums` come indicato nel piano

### Deviazioni dal piano
- Nessuna deviazione. Implementazione conforme al mini-plan.
