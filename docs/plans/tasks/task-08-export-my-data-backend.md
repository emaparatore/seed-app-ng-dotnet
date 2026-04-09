# Task 08: Export dati personali — Backend

## Contesto

### Stato attuale del codice rilevante

- **`AuthController`** (`backend/src/Seed.Api/Controllers/AuthController.cs`): controller REST con endpoint auth, pattern consolidato per endpoint `[Authorize]` con estrazione `UserId` da `ClaimTypes.NameIdentifier`. Endpoint GET esistente: `GET /api/v1/auth/me` che restituisce profilo utente.
- **`GetCurrentUserQuery`** (`backend/src/Seed.Application/Auth/Queries/GetCurrentUser/`): pattern di riferimento per query MediatR autenticata — usa `UserManager` + `IPermissionService`, restituisce `Result<MeResponse>`.
- **`ApplicationUser`** (`backend/src/Seed.Domain/Entities/ApplicationUser.cs`): campi disponibili per export: `Id`, `Email`, `FirstName`, `LastName`, `CreatedAt`, `UpdatedAt`, `IsActive`, `PrivacyPolicyAcceptedAt`, `TermsAcceptedAt`, `ConsentVersion`.
- **`AuditLogEntry`** (`backend/src/Seed.Domain/Entities/AuditLogEntry.cs`): campi: `Id`, `Timestamp`, `UserId`, `Action`, `EntityType`, `EntityId`, `Details`, `IpAddress`, `UserAgent`.
- **`IAuditLogReader`** (`backend/src/Seed.Application/Common/Interfaces/IAuditLogReader.cs`): espone `GetQueryable()` per query su audit log — da usare per filtrare entries per `UserId`.
- **`AuditActions`** (`backend/src/Seed.Domain/Authorization/AuditActions.cs`): contiene le costanti per le azioni di audit. Non esiste ancora `DataExported` — da aggiungere.
- **`Result<T>`** (`backend/src/Seed.Application/Common/Result.cs`): wrapper standard per risultati handler.
- **DI** (`backend/src/Seed.Infrastructure/DependencyInjection.cs`): nessuna registrazione aggiuntiva necessaria (tutti i servizi usati sono già registrati).

### Dipendenze e vincoli

- Nessuna dipendenza da altri task — T-08 è indipendente.
- L'endpoint deve restituire solo dati dell'utente autenticato (mai dati di altri utenti).
- Il formato di risposta è JSON (non file download — il download come file sarà gestito dal frontend in T-09).
- L'audit log dell'export va scritto per tracciabilità GDPR (Art. 15 — diritto di accesso).

## Piano di esecuzione

### Step 1: Aggiungere `DataExported` ad AuditActions

**File da modificare:** `backend/src/Seed.Domain/Authorization/AuditActions.cs`

Aggiungere: `public const string DataExported = "DataExported";`

### Step 2: Creare DTO `UserDataExportDto`

**File nuovo:** `backend/src/Seed.Application/Auth/Queries/ExportMyData/UserDataExportDto.cs`

```csharp
public sealed record UserDataExportDto(
    UserProfileExportDto Profile,
    UserConsentExportDto Consent,
    IReadOnlyList<string> Roles,
    IReadOnlyList<AuditLogExportDto> AuditLog);

public sealed record UserProfileExportDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsActive);

public sealed record UserConsentExportDto(
    DateTime? PrivacyPolicyAcceptedAt,
    DateTime? TermsAcceptedAt,
    string? ConsentVersion);

public sealed record AuditLogExportDto(
    DateTime Timestamp,
    string Action,
    string EntityType,
    string? EntityId,
    string? Details,
    string? IpAddress);
```

### Step 3: Creare `ExportMyDataQuery`

**File nuovo:** `backend/src/Seed.Application/Auth/Queries/ExportMyData/ExportMyDataQuery.cs`

```csharp
public sealed record ExportMyDataQuery(Guid UserId) : IRequest<Result<UserDataExportDto>>;
```

### Step 4: Creare `ExportMyDataQueryHandler`

**File nuovo:** `backend/src/Seed.Application/Auth/Queries/ExportMyData/ExportMyDataQueryHandler.cs`

- Dipendenze: `UserManager<ApplicationUser>`, `IPermissionService` (per i ruoli non serve, basta `UserManager.GetRolesAsync`), `IAuditLogReader`, `IAuditService`
- Flusso:
  1. Trovare utente via `UserManager.FindByIdAsync`
  2. Se non trovato, return failure
  3. Ottenere ruoli via `UserManager.GetRolesAsync`
  4. Query audit log via `IAuditLogReader.GetQueryable().Where(a => a.UserId == userId)` ordinato per `Timestamp` desc
  5. Mappare in `UserDataExportDto`
  6. Scrivere audit log `DataExported` (azione tracciata per GDPR)
  7. Return success con il DTO

### Step 5: Aggiungere endpoint in AuthController

**File da modificare:** `backend/src/Seed.Api/Controllers/AuthController.cs`

Aggiungere dopo l'endpoint `me`:
```csharp
[Authorize]
[HttpGet("export-my-data")]
public async Task<IActionResult> ExportMyData()
{
    var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    var result = await sender.Send(new ExportMyDataQuery(userId));
    return result.Succeeded ? Ok(result.Data) : NotFound(new { errors = result.Errors });
}
```

Aggiungere `using Seed.Application.Auth.Queries.ExportMyData;` agli import.

### Step 6: Unit test per ExportMyDataQueryHandler

**File nuovo:** `backend/tests/Seed.UnitTests/Auth/Queries/ExportMyDataQueryHandlerTests.cs`

Test cases:
- `Should_Return_UserData_When_User_Exists`: mock UserManager con utente valido, mock IAuditLogReader con entries, verifica che il DTO contiene profilo, consenso, ruoli, audit log corretti
- `Should_Fail_When_User_Not_Found`: mock UserManager returns null, verifica Result failure
- `Should_Write_Audit_Log_For_Export`: verifica che `IAuditService.LogAsync` viene chiamato con action `DataExported`
- `Should_Return_Empty_AuditLog_When_No_Entries`: utente senza audit entries, verifica lista vuota

Pattern di test: seguire `DeleteAccountCommandHandlerTests` per il setup di `UserManager` con NSubstitute. Per `IAuditLogReader.GetQueryable()`, mockare restituendo una lista convertita in `IQueryable` (`.AsQueryable()`).

### Step 7: Integration test per endpoint

**File da modificare:** `backend/tests/Seed.IntegrationTests/Auth/AuthEndpointsTests.cs`

Test cases:
- `ExportMyData_WithoutAuth_Returns401`: GET senza token, verifica 401
- `ExportMyData_WithAuth_ReturnsUserData`: registra utente, conferma, login, GET /api/v1/auth/export-my-data, verifica 200 con dati profilo e almeno un audit log entry

### Step 8: Verificare tutti i test

```bash
cd backend && dotnet test Seed.slnx
```

## Criteri di completamento

- [ ] `ExportMyDataQuery` e `ExportMyDataQueryHandler` creati in `Application/Auth/Queries/ExportMyData/`
- [ ] DTO `UserDataExportDto` con sezioni: profilo, consenso, ruoli, audit log
- [ ] Endpoint `GET /api/v1/auth/export-my-data` in `AuthController` protetto da `[Authorize]`
- [ ] Endpoint restituisce solo dati dell'utente autenticato
- [ ] `AuditActions.DataExported` aggiunto e usato per tracciare l'export
- [ ] Unit test: query restituisce dati corretti per utente esistente
- [ ] Unit test: query fallisce per utente non trovato
- [ ] Unit test: audit log scritto per l'export
- [ ] Integration test: endpoint restituisce 401 senza auth, 200 con dati corretti con auth
- [ ] `dotnet build Seed.slnx` — 0 errori
- [ ] `dotnet test Seed.slnx` — tutti i test passano

## Risultato

### File modificati/creati
- **Creato** `backend/src/Seed.Application/Auth/Queries/ExportMyData/UserDataExportDto.cs` — DTO con 4 record: `UserDataExportDto`, `UserProfileExportDto`, `UserConsentExportDto`, `AuditLogExportDto`
- **Creato** `backend/src/Seed.Application/Auth/Queries/ExportMyData/ExportMyDataQuery.cs` — query MediatR
- **Creato** `backend/src/Seed.Application/Auth/Queries/ExportMyData/ExportMyDataQueryHandler.cs` — handler che raccoglie profilo, ruoli, consensi, audit log
- **Modificato** `backend/src/Seed.Domain/Authorization/AuditActions.cs` — aggiunto `DataExported`
- **Modificato** `backend/src/Seed.Api/Controllers/AuthController.cs` — aggiunto endpoint `GET /api/v1/auth/export-my-data`
- **Creato** `backend/tests/Seed.UnitTests/Auth/Queries/ExportMyDataQueryHandlerTests.cs` — 4 test cases
- **Modificato** `backend/tests/Seed.IntegrationTests/Auth/AuthEndpointsTests.cs` — 2 test cases + DTO di risposta
- **Modificato** `backend/tests/Seed.IntegrationTests/Services/UserPurgeServiceTests.cs` — fix errore di compilazione pre-esistente (`HaveCountGreaterOrEqualTo` → `HaveCountGreaterThanOrEqualTo`)

### Scelte implementative e motivazioni
- Usato `.ToList()` sincronamente invece di `.ToListAsync()` per la query audit log, perché il layer Application non referenzia `Microsoft.EntityFrameworkCore` (Clean Architecture). Questo è coerente con il pattern usato dagli altri handler (`GetAuditLogEntriesQueryHandler`, `ExportAuditLogQueryHandler`) che usano lo stesso approccio
- L'audit log dell'export viene scritto **dopo** la raccolta dei dati, così l'evento `DataExported` non appare nella stessa risposta di export (comportamento atteso: l'utente vede i dati fino al momento della richiesta)

### Deviazioni dal piano
- Fix del file `UserPurgeServiceTests.cs` (T-06): il metodo FluentAssertions `HaveCountGreaterOrEqualTo` non esiste, corretto in `HaveCountGreaterThanOrEqualTo`. Necessario per far passare `dotnet build` e `dotnet test`
