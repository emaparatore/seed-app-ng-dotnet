# Task 04: Consenso obbligatorio alla registrazione — Backend

## Contesto

- **Stato attuale:** `RegisterCommand` accetta solo `Email`, `Password`, `FirstName`, `LastName`. Nessun campo di consenso.
- **T-03 completato:** `ApplicationUser` ha già i campi `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` (tutti nullable). Migration `20260409000413_AddConsentFieldsToUsers` applicata.
- **Configurazione:** Le settings sono in `Seed.Shared/Configuration/` e registrate in `Infrastructure/DependencyInjection.cs` con `services.Configure<>`.
- **Audit:** `IAuditService.LogAsync()` e `AuditActions` in `Seed.Domain/Authorization/AuditActions.cs` già presenti.
- **Test esistenti:** `RegisterCommandHandlerTests.cs` ha 4 test che creano `RegisterCommand` con 4 parametri — tutti da aggiornare.

## Piano di esecuzione

### Step 1: Creare `PrivacySettings` in Shared

- **File da creare:** `backend/src/Seed.Shared/Configuration/PrivacySettings.cs`
- Classe `PrivacySettings` con `SectionName = "Privacy"` e proprietà `ConsentVersion` (string, default `"1.0"`)
- Pattern identico a `ClientSettings.cs`

### Step 2: Aggiungere sezione `Privacy` in appsettings.json

- **File da modificare:** `backend/src/Seed.Api/appsettings.json`
- Aggiungere `"Privacy": { "ConsentVersion": "1.0" }`

### Step 3: Registrare `PrivacySettings` in DI

- **File da modificare:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`
- Aggiungere `services.Configure<PrivacySettings>(configuration.GetSection(PrivacySettings.SectionName));` accanto alle altre Configure

### Step 4: Aggiungere campi consenso a `RegisterCommand`

- **File da modificare:** `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommand.cs`
- Aggiungere parametri `bool AcceptPrivacyPolicy` e `bool AcceptTermsOfService` al record

### Step 5: Aggiungere validazione in `RegisterCommandValidator`

- **File da modificare:** `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommandValidator.cs`
- Aggiungere `RuleFor(x => x.AcceptPrivacyPolicy).Equal(true).WithMessage("You must accept the Privacy Policy.")` 
- Aggiungere `RuleFor(x => x.AcceptTermsOfService).Equal(true).WithMessage("You must accept the Terms of Service.")`

### Step 6: Aggiornare `RegisterCommandHandler`

- **File da modificare:** `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommandHandler.cs`
- Iniettare `IOptions<PrivacySettings>` nel costruttore
- Dopo la creazione di `ApplicationUser`, impostare `PrivacyPolicyAcceptedAt = DateTime.UtcNow`, `TermsAcceptedAt = DateTime.UtcNow`, `ConsentVersion = _privacySettings.ConsentVersion`
- Aggiungere audit log per evento consenso (nuovo `AuditActions.ConsentGiven`)

### Step 7: Aggiungere `ConsentGiven` ad `AuditActions`

- **File da modificare:** `backend/src/Seed.Domain/Authorization/AuditActions.cs`
- Aggiungere `public const string ConsentGiven = "ConsentGiven";`

### Step 8: Aggiornare i test esistenti

- **File da modificare:** `backend/tests/Seed.UnitTests/Auth/Commands/RegisterCommandHandlerTests.cs`
- Aggiornare tutte le istanze di `RegisterCommand` per includere i due nuovi parametri `true, true`
- Aggiornare il costruttore del handler per iniettare `IOptions<PrivacySettings>`

### Step 9: Aggiungere nuovi test

- **File da modificare:** `backend/tests/Seed.UnitTests/Auth/Commands/RegisterCommandHandlerTests.cs`
- **Test 1 — registrazione rifiutata senza consenso:** Usare `RegisterCommandValidator` direttamente, verificare che `AcceptPrivacyPolicy = false` o `AcceptTermsOfService = false` produce errori di validazione
- **Test 2 — registrazione con consenso salva timestamp e versione:** Verificare che dopo `Handle()`, l'`ApplicationUser` passato a `CreateAsync` ha `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt` e `ConsentVersion` impostati correttamente

### Step 10: Verificare build e test

```bash
cd backend && dotnet build Seed.slnx
cd backend && dotnet test Seed.slnx
```

## Criteri di completamento

- `RegisterCommand` include `AcceptPrivacyPolicy` e `AcceptTermsOfService`
- `RegisterCommandValidator` rifiuta registrazione senza consenso (entrambi devono essere `true`)
- `RegisterCommandHandler` salva `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` sull'utente
- Sezione `Privacy` in `appsettings.json` con `ConsentVersion: "1.0"`
- `PrivacySettings` in Shared registrata in DI
- `AuditActions.ConsentGiven` aggiunto e utilizzato nell'handler
- Test unitari: validazione rifiuta senza consenso + handler salva timestamp e versione
- Tutti i test esistenti aggiornati e passano
- `dotnet build Seed.slnx` e `dotnet test Seed.slnx` completano senza errori

## Risultato

- **File creati:**
  - `backend/src/Seed.Shared/Configuration/PrivacySettings.cs` — classe di configurazione con `SectionName = "Privacy"` e `ConsentVersion` (default "1.0")

- **File modificati:**
  - `backend/src/Seed.Api/appsettings.json` — aggiunta sezione `Privacy` con `ConsentVersion: "1.0"`
  - `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione `PrivacySettings` in DI
  - `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommand.cs` — aggiunti parametri `AcceptPrivacyPolicy` e `AcceptTermsOfService`
  - `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommandValidator.cs` — aggiunte regole di validazione per i due campi consenso
  - `backend/src/Seed.Application/Auth/Commands/Register/RegisterCommandHandler.cs` — iniettato `IOptions<PrivacySettings>`, impostati `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion` sull'utente, aggiunto audit log `ConsentGiven`
  - `backend/src/Seed.Domain/Authorization/AuditActions.cs` — aggiunta costante `ConsentGiven`
  - `backend/tests/Seed.UnitTests/Auth/Commands/RegisterCommandHandlerTests.cs` — aggiornati 4 test esistenti con nuovi parametri, aggiunti 2 nuovi test (consent timestamp/version + validator rejection)
  - `backend/tests/Seed.UnitTests/Auth/Validators/RegisterCommandValidatorTests.cs` — aggiornato `ValidCommand` con i nuovi parametri
  - `backend/tests/Seed.IntegrationTests/Auth/AuthEndpointsTests.cs` — aggiunti campi consenso a tutti i payload di registrazione
  - `backend/tests/Seed.IntegrationTests/Auth/ChangePasswordTests.cs` — aggiunti campi consenso al payload di registrazione
  - `backend/tests/Seed.IntegrationTests/Authorization/PermissionAuthorizationTests.cs` — aggiunti campi consenso a tutti i payload di registrazione
  - `backend/tests/Seed.IntegrationTests/Admin/AdminRolesEndpointsTests.cs` — aggiunti campi consenso
  - `backend/tests/Seed.IntegrationTests/Admin/AdminSettingsEndpointsTests.cs` — aggiunti campi consenso
  - `backend/tests/Seed.IntegrationTests/Admin/AdminDashboardEndpointsTests.cs` — aggiunti campi consenso
  - `backend/tests/Seed.IntegrationTests/Admin/AdminSystemHealthEndpointsTests.cs` — aggiunti campi consenso
  - `backend/tests/Seed.IntegrationTests/Admin/AdminUsersEndpointsTests.cs` — aggiunti campi consenso
  - `backend/tests/Seed.IntegrationTests/Admin/AdminAuditLogEndpointsTests.cs` — aggiunti campi consenso

- **Scelte implementative:**
  - I timestamp di consenso vengono impostati direttamente sull'oggetto `ApplicationUser` prima di `CreateAsync`, così vengono salvati atomicamente con la creazione dell'utente
  - L'audit log `ConsentGiven` viene scritto come evento separato da `UserCreated` per distinguere chiaramente le due azioni ai fini GDPR
  - Il test di validazione usa `[Theory]` con `[InlineData]` per coprire tutte e 3 le combinazioni di rifiuto (solo privacy, solo terms, entrambi)
  - Il test di salvataggio timestamp usa `Arg.Do<ApplicationUser>()` per catturare l'utente passato a `CreateAsync` e verificare i campi

- **Deviazioni dal piano:** Nessuna deviazione. Tutti gli step eseguiti come da piano. In aggiunta, sono stati aggiornati anche tutti i test di integrazione (9 file) che usavano il payload di registrazione senza i campi di consenso, non menzionati esplicitamente nel mini-plan ma necessari per il passaggio dei test.

- **Risultati test:** Build OK, 184 unit test + 97 integration test = 281 test totali, tutti passano.
