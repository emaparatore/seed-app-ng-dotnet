# Task 03: Campi consenso su entità utente — Backend

## Contesto

- **ApplicationUser** (`backend/src/Seed.Domain/Entities/ApplicationUser.cs`): entità Identity con campi custom (`FirstName`, `LastName`, `CreatedAt`, `UpdatedAt`, `IsActive`, `MustChangePassword`, `IsDeleted`, `DeletedAt`, `RefreshTokens`). Nessun campo di consenso presente.
- **ApplicationUserConfiguration** (`backend/src/Seed.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`): configura max lengths, defaults, e query filter per soft delete. Pattern standard `IEntityTypeConfiguration<ApplicationUser>`.
- **Migrations** in `backend/src/Seed.Infrastructure/Migrations/` — naming convention: `yyyyMMddHHmmss_DescriptiveName.cs`. Ultima migration: `AddSystemSettings` (2026-03-23). Tabella: `AspNetUsers`.
- **DbContext** applica configurazioni automaticamente via `ApplyConfigurationsFromAssembly`.
- Nessuna dipendenza esterna per questo task.

## Piano di esecuzione

### Step 1: Aggiungere campi a ApplicationUser

**File:** `backend/src/Seed.Domain/Entities/ApplicationUser.cs`

Aggiungere prima della collection `RefreshTokens`:

```csharp
public DateTime? PrivacyPolicyAcceptedAt { get; set; }
public DateTime? TermsAcceptedAt { get; set; }
public string? ConsentVersion { get; set; }
```

- `DateTime?` nullable perché utenti esistenti non hanno ancora dato il consenso
- `string?` per `ConsentVersion` — sarà valorizzato con la versione corrente al momento del consenso (es. "1.0")

### Step 2: Aggiornare configurazione EF

**File:** `backend/src/Seed.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`

Aggiungere nel metodo `Configure`, dopo la riga `builder.HasQueryFilter(...)`:

```csharp
builder.Property(u => u.PrivacyPolicyAcceptedAt).IsRequired(false);
builder.Property(u => u.TermsAcceptedAt).IsRequired(false);
builder.Property(u => u.ConsentVersion).HasMaxLength(20).IsRequired(false);
```

- `MaxLength(20)` per ConsentVersion — sufficiente per versioning semantico (es. "1.0", "2.1.0")
- Tutti nullable (`.IsRequired(false)`) per compatibilità con utenti esistenti

### Step 3: Creare migration EF Core

**Comando da eseguire da `backend/`:**

```bash
dotnet ef migrations add AddConsentFieldsToUsers --project src/Seed.Infrastructure --startup-project src/Seed.Api
```

La migration aggiungerà 3 colonne nullable alla tabella `AspNetUsers`:
- `PrivacyPolicyAcceptedAt` — `timestamp with time zone`, nullable
- `TermsAcceptedAt` — `timestamp with time zone`, nullable
- `ConsentVersion` — `character varying(20)`, nullable

### Step 4: Verificare build e test

```bash
dotnet build Seed.slnx
dotnet test Seed.slnx
```

## Criteri di completamento

- [ ] Campi `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` presenti in `ApplicationUser`
- [ ] Configurazione EF aggiornata con tipi e vincoli (MaxLength 20 per ConsentVersion, tutti nullable)
- [ ] Migration `AddConsentFieldsToUsers` creata e compilabile
- [ ] `dotnet build Seed.slnx` passa senza errori
- [ ] `dotnet test Seed.slnx` passa senza errori (nessun test rotto dai nuovi campi nullable)

## Risultato

- File modificati/creati:
  - `backend/src/Seed.Domain/Entities/ApplicationUser.cs` — aggiunti 3 campi: `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion`
  - `backend/src/Seed.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs` — aggiunta configurazione EF per i 3 nuovi campi (nullable, MaxLength 20 per ConsentVersion)
  - `backend/src/Seed.Infrastructure/Migrations/20260409000413_AddConsentFieldsToUsers.cs` — migration EF Core generata
  - `backend/src/Seed.Infrastructure/Migrations/20260409000413_AddConsentFieldsToUsers.Designer.cs` — migration designer generato
  - `backend/src/Seed.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` — snapshot aggiornato
- Scelte implementative e motivazioni:
  - Campi posizionati prima di `RefreshTokens` come da piano, raggruppando i campi di consenso insieme
  - Tutti i campi nullable per compatibilità con utenti esistenti (nessun dato default necessario)
  - `ConsentVersion` con `MaxLength(20)` sufficiente per versioning semantico
- Eventuali deviazioni dal piano e perché:
  - Nessuna deviazione. Build OK (0 errori, 0 warning), tutti i 277 test passati (180 unit + 97 integration)
