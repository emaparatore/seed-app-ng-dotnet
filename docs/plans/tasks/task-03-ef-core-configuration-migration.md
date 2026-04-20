# Task 03: EF Core configuration and migration

## Contesto

- **Entità domain già create (T-02 completato):**
  - `SubscriptionPlan` — `backend/src/Seed.Domain/Entities/SubscriptionPlan.cs`
  - `PlanFeature` — `backend/src/Seed.Domain/Entities/PlanFeature.cs`
  - `UserSubscription` — `backend/src/Seed.Domain/Entities/UserSubscription.cs`
  - `InvoiceRequest` — `backend/src/Seed.Domain/Entities/InvoiceRequest.cs`
- **Enum già creati:** `PlanStatus`, `SubscriptionStatus`, `CustomerType`, `InvoiceRequestStatus`, `BillingInterval` in `Seed.Domain/Enums/`
- **Navigation properties su ApplicationUser** già aggiunte: `Subscriptions` e `InvoiceRequests`
- **DbContext** in `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs` — usa `ApplyConfigurationsFromAssembly` quindi le configuration vengono auto-registrate
- **Pattern esistente** per le configuration: vedi `RefreshTokenConfiguration.cs` come riferimento (HasKey, HasIndex, HasOne/WithMany, OnDelete)
- **Convenzione DbSet:** pattern `public DbSet<T> Name => Set<T>();`

## Piano di esecuzione

### Step 1: Creare le Entity Configuration

**File da creare:**

1. **`backend/src/Seed.Infrastructure/Persistence/Configurations/SubscriptionPlanConfiguration.cs`**
   - `HasKey(p => p.Id)`
   - `Property(p => p.Name).HasMaxLength(200).IsRequired()`
   - `Property(p => p.Description).HasMaxLength(1000)`
   - `Property(p => p.MonthlyPrice).HasPrecision(18, 2)` 
   - `Property(p => p.YearlyPrice).HasPrecision(18, 2)`
   - `Property(p => p.StripePriceIdMonthly).HasMaxLength(100)`
   - `Property(p => p.StripePriceIdYearly).HasMaxLength(100)`
   - `Property(p => p.StripeProductId).HasMaxLength(100)`
   - `Property(p => p.Status).HasConversion<string>().HasMaxLength(20)` (string conversion per leggibilità DB)
   - `HasIndex(p => p.Status)`
   - `HasIndex(p => p.IsDefault)`
   - `HasMany(p => p.Features).WithOne(f => f.Plan).HasForeignKey(f => f.PlanId).OnDelete(DeleteBehavior.Cascade)`
   - `HasMany(p => p.Subscriptions).WithOne(s => s.Plan).HasForeignKey(s => s.PlanId).OnDelete(DeleteBehavior.Restrict)`

2. **`backend/src/Seed.Infrastructure/Persistence/Configurations/PlanFeatureConfiguration.cs`**
   - `HasKey(f => f.Id)`
   - `Property(f => f.Key).HasMaxLength(100).IsRequired()`
   - `Property(f => f.Description).HasMaxLength(500).IsRequired()`
   - `Property(f => f.LimitValue).HasMaxLength(50)`
   - `HasIndex(f => new { f.PlanId, f.Key }).IsUnique()` (composite unique index)

3. **`backend/src/Seed.Infrastructure/Persistence/Configurations/UserSubscriptionConfiguration.cs`**
   - `HasKey(s => s.Id)`
   - `Property(s => s.Status).HasConversion<string>().HasMaxLength(20)`
   - `Property(s => s.StripeSubscriptionId).HasMaxLength(100)`
   - `Property(s => s.StripeCustomerId).HasMaxLength(100)`
   - `HasIndex(s => new { s.UserId, s.Status })` (query per user+status)
   - `HasIndex(s => s.StripeSubscriptionId).IsUnique().HasFilter("\"StripeSubscriptionId\" IS NOT NULL")` (unique ma nullable)
   - `HasOne(s => s.User).WithMany(u => u.Subscriptions).HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade)`

4. **`backend/src/Seed.Infrastructure/Persistence/Configurations/InvoiceRequestConfiguration.cs`**
   - `HasKey(i => i.Id)`
   - `Property(i => i.CustomerType).HasConversion<string>().HasMaxLength(20)`
   - `Property(i => i.Status).HasConversion<string>().HasMaxLength(20)`
   - `Property(i => i.FullName).HasMaxLength(200).IsRequired()`
   - `Property(i => i.CompanyName).HasMaxLength(200)`
   - `Property(i => i.Address).HasMaxLength(500).IsRequired()`
   - `Property(i => i.City).HasMaxLength(100).IsRequired()`
   - `Property(i => i.PostalCode).HasMaxLength(20).IsRequired()`
   - `Property(i => i.Country).HasMaxLength(100).IsRequired()`
   - `Property(i => i.FiscalCode).HasMaxLength(20)`
   - `Property(i => i.VatNumber).HasMaxLength(20)`
   - `Property(i => i.SdiCode).HasMaxLength(10)`
   - `Property(i => i.PecEmail).HasMaxLength(200)`
   - `Property(i => i.StripePaymentIntentId).HasMaxLength(100)`
   - `HasIndex(i => i.UserId)`
   - `HasIndex(i => i.Status)`
   - `HasOne(i => i.User).WithMany(u => u.InvoiceRequests).HasForeignKey(i => i.UserId).OnDelete(DeleteBehavior.Cascade)`

### Step 2: Aggiungere DbSet all'ApplicationDbContext

**File da modificare:** `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs`

Aggiungere dopo la riga 15 (SystemSettings):
```csharp
public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
public DbSet<InvoiceRequest> InvoiceRequests => Set<InvoiceRequest>();
```

### Step 3: Generare la migration

```bash
cd backend
dotnet ef migrations add AddSubscriptionPayments --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

### Step 4: Verificare

```bash
cd backend
dotnet build Seed.slnx
dotnet test Seed.slnx
```

### Test

Non servono nuovi unit test per questo task — le entity configurations sono infrastrutturali e vengono verificate dalla compilazione della migration e dal build. I test di integrazione esistenti confermano già che il DbContext si inizializza correttamente.

## Criteri di completamento

- [ ] 4 file di configuration creati in `Persistence/Configurations/` con vincoli, indici e FK corretti
- [ ] 4 DbSet aggiunti ad `ApplicationDbContext`
- [ ] Migration `AddSubscriptionPayments` generata correttamente
- [ ] `dotnet build Seed.slnx` passa senza errori
- [ ] `dotnet test Seed.slnx` passa senza errori

## Risultato

- **File creati:**
  - `backend/src/Seed.Infrastructure/Persistence/Configurations/SubscriptionPlanConfiguration.cs`
  - `backend/src/Seed.Infrastructure/Persistence/Configurations/PlanFeatureConfiguration.cs`
  - `backend/src/Seed.Infrastructure/Persistence/Configurations/UserSubscriptionConfiguration.cs`
  - `backend/src/Seed.Infrastructure/Persistence/Configurations/InvoiceRequestConfiguration.cs`
  - `backend/src/Seed.Infrastructure/Persistence/Migrations/*_AddSubscriptionPayments.cs` (migration + designer + snapshot update)
- **File modificati:**
  - `backend/src/Seed.Infrastructure/Persistence/ApplicationDbContext.cs` — aggiunti 4 DbSet
- **Scelte implementative:**
  - Seguito esattamente il pattern esistente (`RefreshTokenConfiguration`) per struttura e stile (sealed class, namespace file-scoped)
  - Enum convertiti a stringa nel DB (`HasConversion<string>()`) per leggibilità come da piano
  - Indice unique su `StripeSubscriptionId` con filtro `IS NOT NULL` per supportare il nullable correttamente in PostgreSQL
  - `DeleteBehavior.Restrict` su `SubscriptionPlan → UserSubscription` per impedire la cancellazione di piani con sottoscrizioni attive
  - `DeleteBehavior.Cascade` su `User → UserSubscription` e `User → InvoiceRequest` coerente con il pattern RefreshToken
- **Deviazioni dal piano:** Nessuna
