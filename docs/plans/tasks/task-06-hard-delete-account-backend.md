# Task 06: Hard delete account — Backend

## Contesto

### Stato attuale del codice rilevante

- **`DeleteAccountCommandHandler`** (`backend/src/Seed.Application/Auth/Commands/DeleteAccount/DeleteAccountCommandHandler.cs`): esegue attualmente **soft delete** — setta `IsActive = false`, revoca i token tramite `RevokeAllUserTokensAsync`, scrive audit log `AccountDeleted`.
- **`ApplicationUser`** (`backend/src/Seed.Domain/Entities/ApplicationUser.cs`): ha già i campi `IsDeleted` e `DeletedAt` (non usati nel delete handler). Query filter `HasQueryFilter(u => !u.IsDeleted)` già configurato.
- **`RefreshToken`** (`backend/src/Seed.Domain/Entities/RefreshToken.cs`): ha cascade delete configurato nella EF config (`OnDelete(DeleteBehavior.Cascade)`), quindi eliminare l'utente elimina automaticamente i token.
- **`AuditLogEntry`** (`backend/src/Seed.Domain/Entities/AuditLogEntry.cs`): ha `UserId` (Guid?), `EntityId` (string?), `Details` (string?), `IpAddress` (string?), `UserAgent` (string?) — tutti campi che possono contenere PII da anonimizzare.
- **`AuditService`** (`backend/src/Seed.Infrastructure/Services/AuditService.cs`): scrive direttamente su `dbContext.AuditLogEntries`.
- **`TokenService.RevokeAllUserTokensAsync`** (`backend/src/Seed.Infrastructure/Services/TokenService.cs`): revoca i token (setta `RevokedAt`) ma non li elimina fisicamente.
- **DI Registration** (`backend/src/Seed.Infrastructure/DependencyInjection.cs`): pattern `services.AddScoped<IInterface, Implementation>()`.
- **Unit tests** (`backend/tests/Seed.UnitTests/Auth/Commands/DeleteAccountCommandHandlerTests.cs`): testano il flusso soft delete con NSubstitute + FluentAssertions.

### Dipendenze e vincoli

- Il cascade delete di EF si occupa dei RefreshToken quando l'utente viene eliminato via `UserManager.DeleteAsync`, quindi non serve eliminare i token manualmente prima del delete (ma per sicurezza il purge service li gestirà esplicitamente).
- L'anonimizzazione audit log deve avvenire **prima** della cancellazione dell'utente.
- `IUserPurgeService` deve essere riutilizzabile da T-10 (purge utenti soft-deleted).
- L'handler deve continuare a validare la password prima di procedere.
- `AuditActions.AccountDeleted` esiste già — va usato per il log pre-purge.

## Piano di esecuzione

### Step 1: Creare interfaccia `IUserPurgeService`

**File:** `backend/src/Seed.Application/Common/Interfaces/IUserPurgeService.cs` (NUOVO)

```csharp
public interface IUserPurgeService
{
    Task PurgeUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

Il metodo esegue:
1. Anonimizza audit log dell'utente (`UserId = null`, `Details = "[REDACTED]"`, `EntityId = null` dove EntityId era l'userId, `IpAddress = null`, `UserAgent = null`)
2. Elimina tutti i refresh token dell'utente (hard delete, non solo revoca)
3. Elimina il record utente via `UserManager.DeleteAsync`

### Step 2: Implementare `UserPurgeService`

**File:** `backend/src/Seed.Infrastructure/Services/UserPurgeService.cs` (NUOVO)

- Dipendenze: `ApplicationDbContext`, `UserManager<ApplicationUser>`, `ILogger<UserPurgeService>`
- Deve usare `IgnoreQueryFilters()` per trovare utenti soft-deleted (query filter su `IsDeleted`)
- Anonimizzazione audit log: query `AuditLogEntries.Where(a => a.UserId == userId)`, poi set dei campi a null/redacted
- Delete token: `dbContext.RefreshTokens.Where(r => r.UserId == userId).ExecuteDeleteAsync()` (bulk delete efficiente)
- Delete utente: trovare l'utente bypassing query filter, poi `UserManager.DeleteAsync(user)`
- Se l'utente non viene trovato, loggare warning e ritornare senza errore

### Step 3: Registrare in DI

**File da modificare:** `backend/src/Seed.Infrastructure/DependencyInjection.cs`

Aggiungere: `services.AddScoped<IUserPurgeService, UserPurgeService>();`

### Step 4: Modificare `DeleteAccountCommandHandler`

**File da modificare:** `backend/src/Seed.Application/Auth/Commands/DeleteAccount/DeleteAccountCommandHandler.cs`

Nuovo flusso:
1. Trova utente e verifica password (invariato)
2. Scrive audit log `AccountDeleted` con dati prima della cancellazione (invariato)
3. Chiama `IUserPurgeService.PurgeUserAsync(userId)` — questo anonimizza audit, elimina token, elimina utente
4. Rimuovere la logica di soft delete (`IsActive = false`, `UpdateAsync`) e la chiamata a `tokenService.RevokeAllUserTokensAsync`
5. Rimuovere la dipendenza da `ITokenService` (non più necessaria)
6. Aggiungere dipendenza da `IUserPurgeService`

### Step 5: Aggiungere `AccountPurged` ad AuditActions (opzionale)

**File da modificare:** `backend/src/Seed.Domain/Authorization/AuditActions.cs`

Aggiungere: `public const string AccountPurged = "AccountPurged";` — per distinguere l'evento di purge (scritto prima della cancellazione) dall'evento `AccountDeleted` esistente. **Decisione:** usare `AccountDeleted` esistente per il log pre-purge, non serve un nuovo action. L'audit log con `AccountDeleted` viene scritto nell'handler prima di chiamare purge, e poi il purge anonimizza le entry dell'utente (ma l'entry `AccountDeleted` appena scritta avrà già `UserId = null` dopo l'anonimizzazione — questo è corretto per GDPR).

### Step 6: Unit tests

**File da modificare:** `backend/tests/Seed.UnitTests/Auth/Commands/DeleteAccountCommandHandlerTests.cs`

Aggiornare i test esistenti:
- Il mock di `ITokenService` viene rimosso, sostituito con mock di `IUserPurgeService`
- Test happy path: verifica che `PurgeUserAsync` viene chiamato con l'userId corretto
- Test user not found: verifica che `PurgeUserAsync` NON viene chiamato
- Test invalid password: verifica che `PurgeUserAsync` NON viene chiamato

**File nuovo:** `backend/tests/Seed.UnitTests/Services/UserPurgeServiceTests.cs`

Test per `UserPurgeService`:
- Test: purge anonimizza audit log (crea entries con userId, chiama purge, verifica `UserId = null` e `Details = "[REDACTED]"`)
- Test: purge elimina refresh token dell'utente
- Test: purge elimina l'utente dal DB
- Test: purge con userId inesistente non lancia eccezione

**Nota:** `UserPurgeService` dipende da `ApplicationDbContext` e `UserManager`, quindi i test saranno integration tests o useranno un InMemory database. Verificare il pattern esistente in `Seed.UnitTests/Services/` per capire come mockare il DbContext.

### Step 7: Integration test

**File da modificare:** `backend/tests/Seed.IntegrationTests/Auth/AuthEndpointsTests.cs`

Aggiungere test:
- `DeleteAccount_WithValidPassword_RemovesUserFromDatabase`: registra utente, conferma email, login, chiama DELETE /api/v1/auth/account, verifica 204, poi verifica che l'utente non esiste più nel DB (tentare login restituisce 401/400)
- Verificare che i token dell'utente sono stati eliminati
- Verificare che l'audit log dell'utente è stato anonimizzato

### Step 8: Verificare tutti i test

```bash
cd backend && dotnet test Seed.slnx
```

## Criteri di completamento

- [ ] `IUserPurgeService` interface creata in `Application/Common/Interfaces/` con metodo `PurgeUserAsync(Guid userId, CancellationToken)`
- [ ] `UserPurgeService` implementato in `Infrastructure/Services/` — anonimizza audit log, elimina token, elimina utente
- [ ] `DeleteAccountCommandHandler` usa `IUserPurgeService` per hard delete (non più soft delete)
- [ ] `UserPurgeService` registrato in DI in `DependencyInjection.cs`
- [ ] Audit log `AccountDeleted` scritto prima del purge (con dati non ancora anonimizzati)
- [ ] Unit tests aggiornati per `DeleteAccountCommandHandler` (mock di `IUserPurgeService`)
- [ ] Unit/Integration test per `UserPurgeService` (anonimizza audit, elimina token, elimina utente)
- [ ] Integration test: DELETE /api/v1/auth/account rimuove l'utente dal DB
- [ ] `dotnet build Seed.slnx` — 0 errori
- [ ] `dotnet test Seed.slnx` — tutti i test passano
