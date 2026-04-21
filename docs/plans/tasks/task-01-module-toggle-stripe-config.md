# Task 01: Module toggle system and Stripe configuration

## Contesto

- Le configurazioni POCO esistenti seguono un pattern consolidato in `backend/src/Seed.Shared/Configuration/` (es. `JwtSettings.cs`, `SmtpSettings.cs`): classe `sealed` con `SectionName` const e proprietà `init`.
- La registrazione avviene in `backend/src/Seed.Infrastructure/DependencyInjection.cs` con `services.Configure<T>(configuration.GetSection(...))`.
- `appsettings.json` contiene le sezioni di configurazione con valori vuoti/default; `appsettings.Development.json` aggiunge solo override per lo sviluppo locale.
- Non esiste ancora alcun concetto di "module toggle" nel codebase.

## Piano di esecuzione

### File da creare

1. **`backend/src/Seed.Shared/Configuration/ModulesSettings.cs`**
   - Classe `ModulesSettings` con `SectionName = "Modules"`.
   - Proprietà `PaymentsModuleSettings Payments { get; init; }` (inizializzata a `new()`).

2. **`backend/src/Seed.Shared/Configuration/PaymentsModuleSettings.cs`**
   - Classe `PaymentsModuleSettings` con proprietà:
     - `bool Enabled { get; init; }` — default `false`
     - `string Provider { get; init; }` — default `string.Empty` (valori attesi: `"Stripe"`, `"Mock"`, `""`)

3. **`backend/src/Seed.Shared/Configuration/StripeSettings.cs`**
   - Classe `StripeSettings` con `SectionName = "Stripe"`.
   - Proprietà: `SecretKey`, `PublishableKey`, `WebhookSecret` — tutte `string`, default `string.Empty`.

4. **`backend/src/Seed.Shared/Extensions/ConfigurationExtensions.cs`**
   - Metodo statico `IsPaymentsModuleEnabled(this IConfiguration configuration)` che legge `Modules:Payments:Enabled` e restituisce `bool`.

### File da modificare

5. **`backend/src/Seed.Api/appsettings.json`**
   - Aggiungere sezione `"Modules"` con `"Payments": { "Enabled": false, "Provider": "" }`.
   - Aggiungere sezione `"Stripe"` con `"SecretKey": "", "PublishableKey": "", "WebhookSecret": ""`.

6. **`backend/src/Seed.Api/appsettings.Development.json`**
   - Aggiungere sezione `"Stripe"` con chiavi placeholder: `"sk_test_placeholder"`, `"pk_test_placeholder"`, `"whsec_placeholder"`.

7. **`backend/src/Seed.Infrastructure/DependencyInjection.cs`**
   - Aggiungere `services.Configure<ModulesSettings>(configuration.GetSection(ModulesSettings.SectionName))`.
   - Aggiungere `services.Configure<StripeSettings>(configuration.GetSection(StripeSettings.SectionName))` condizionalmente (solo se payments module enabled).
   - Usare il pattern condizionale simile a quello SMTP esistente.

### Approccio tecnico step-by-step

1. Creare i 3 file POCO seguendo l'identico pattern di `JwtSettings.cs`.
2. Creare l'extension method in una nuova classe `ConfigurationExtensions`.
3. Aggiornare `appsettings.json` e `appsettings.Development.json`.
4. Registrare le nuove configurazioni in `DependencyInjection.cs`.
5. Build della solution per verificare compilazione.
6. Scrivere unit test per `IsPaymentsModuleEnabled` extension method.

### Test da scrivere

- **`backend/tests/Seed.UnitTests/Shared/ConfigurationExtensionsTests.cs`**
  - Test `IsPaymentsModuleEnabled_WhenEnabled_ReturnsTrue`
  - Test `IsPaymentsModuleEnabled_WhenDisabled_ReturnsFalse`
  - Test `IsPaymentsModuleEnabled_WhenSectionMissing_ReturnsFalse`

## Criteri di completamento

- [ ] `PaymentsModuleSettings` e `StripeSettings` POCOs esistono con nomi di sezione corretti
- [ ] `ModulesSettings` esiste come wrapper per i moduli
- [ ] `appsettings.json` contiene sezione `Modules:Payments` (disabled) e sezione `Stripe` (chiavi vuote)
- [ ] `appsettings.Development.json` contiene placeholder Stripe
- [ ] Settings registrati in DI
- [ ] Extension method `IsPaymentsModuleEnabled` funzionante con unit test
- [ ] `dotnet build Seed.slnx` dalla cartella `backend/` compila senza errori
- [ ] `dotnet test Seed.slnx` dalla cartella `backend/` passa tutti i test

## Risultato

### File creati
- `backend/src/Seed.Shared/Configuration/ModulesSettings.cs` — wrapper POCO per i moduli con sezione `Modules`
- `backend/src/Seed.Shared/Configuration/PaymentsModuleSettings.cs` — POCO con `Enabled` (default false) e `Provider` (default empty)
- `backend/src/Seed.Shared/Configuration/StripeSettings.cs` — POCO con `SecretKey`, `PublishableKey`, `WebhookSecret`
- `backend/src/Seed.Shared/Extensions/ConfigurationExtensions.cs` — extension method `IsPaymentsModuleEnabled`
- `backend/tests/Seed.UnitTests/Shared/ConfigurationExtensionsTests.cs` — 3 test per l'extension method

### File modificati
- `backend/src/Seed.Shared/Seed.Shared.csproj` — aggiunto `Microsoft.Extensions.Configuration.Binder` 10.0.3
- `backend/src/Seed.Api/appsettings.json` — aggiunte sezioni `Modules` e `Stripe`
- `backend/src/Seed.Api/appsettings.Development.json` — aggiunta sezione `Stripe` con placeholder test
- `backend/src/Seed.Infrastructure/DependencyInjection.cs` — registrazione `ModulesSettings` e `StripeSettings` (condizionale)

### Scelte implementative e motivazioni
- Aggiunto `Microsoft.Extensions.Configuration.Binder` a `Seed.Shared.csproj` (v10.0.3, allineata alle altre dipendenze Microsoft nel progetto) per poter usare `GetValue<T>` nell'extension method
- Pattern condizionale per `StripeSettings` segue lo stesso approccio del blocco SMTP esistente in `DependencyInjection.cs`
- `ModulesSettings` registrato sempre (non condizionalmente) per consentire l'iniezione anche quando il modulo è disabilitato

### Deviazioni dal piano
- Nessuna deviazione sostanziale. Unica aggiunta: il package `Microsoft.Extensions.Configuration.Binder` nel `.csproj` di Shared, necessario per compilare l'extension method (non menzionato esplicitamente nel piano ma implicito nell'uso di `IConfiguration.GetValue<T>`)
