# Task 07: API gestione utenti (AdminUsersController)

## Contesto

### Stato attuale del codice rilevante
- **Domain**: `ApplicationUser` ha campi `Id`, `Email`, `FirstName`, `LastName`, `IsActive`, `MustChangePassword`, `CreatedAt`, `UpdatedAt`. **Non esiste** un campo `IsDeleted` — va aggiunto per il soft delete.
- **Permessi**: Costanti definite in `Seed.Domain/Authorization/Permissions.Users.*` (`Read`, `Create`, `Update`, `Delete`, `ToggleStatus`, `AssignRoles`).
- **Audit actions**: Costanti in `Seed.Domain/Authorization/AuditActions` (`UserCreated`, `UserUpdated`, `UserDeleted`, `UserStatusChanged`, `UserRolesChanged`).
- **Autorizzazione**: `HasPermissionAttribute` in `Seed.Api/Authorization/` per decorare endpoint. SuperAdmin bypassa tutti i controlli.
- **Servizi disponibili**: `IPermissionService` (cache + invalidazione), `ITokenBlacklistService` (blacklist token), `IAuditService` (logging audit).
- **Pattern**: MediatR CQRS con `Result<T>`, FluentValidation auto-discovered, controller con `ISender`.
- **Paginazione**: Non esiste `PagedResult<T>` — va creato come modello generico.
- **Test infra**: `CustomWebApplicationFactory` con Testcontainers, helper `RegisterAndConfirmUserAsync`, pattern per assegnare ruoli via `UserManager`.

### Dipendenze e vincoli
- **T-03** (autorizzazione permessi): ✅ Completato
- **T-06** (audit log infrastructure): ✅ Completato
- Protezioni: no auto-eliminazione, no eliminazione/disattivazione SuperAdmin, no auto-modifica ruolo SuperAdmin
- Alla disattivazione: invalidazione token via `ITokenBlacklistService`
- Alla modifica ruoli: invalidazione cache permessi + blacklist token

## Piano di esecuzione

### Step 1: Aggiungere `IsDeleted` a `ApplicationUser` + Migration

**File da modificare:**
- `backend/src/Seed.Domain/Entities/ApplicationUser.cs` — aggiungere `public bool IsDeleted { get; set; }` e `public DateTime? DeletedAt { get; set; }`
- `backend/src/Seed.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs` — aggiungere configurazione per `IsDeleted` (query filter globale: `.HasQueryFilter(u => !u.IsDeleted)`)

**File da creare:**
- Migration: `dotnet ef migrations add AddSoftDeleteToUsers --project src/Seed.Infrastructure --startup-project src/Seed.Api`

### Step 2: Creare `PagedResult<T>`

**File da creare:**
- `backend/src/Seed.Application/Common/Models/PagedResult.cs`

```csharp
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
```

### Step 3: Creare DTOs per Admin Users

**File da creare:**
- `backend/src/Seed.Application/Admin/Users/Models/AdminUserDto.cs`
- `backend/src/Seed.Application/Admin/Users/Models/AdminUserDetailDto.cs`

`AdminUserDto` (per lista): Id, Email, FirstName, LastName, IsActive, Roles (string[]), CreatedAt, LastLoginAt
`AdminUserDetailDto` (per dettaglio): stessi + MustChangePassword, UpdatedAt, EmailConfirmed

### Step 4: Creare Queries

**File da creare:**
- `backend/src/Seed.Application/Admin/Users/Queries/GetUsers/GetUsersQuery.cs` — parametri: PageNumber, PageSize, SearchTerm?, RoleFilter?, StatusFilter?, DateFrom?, DateTo?, SortBy?, SortDescending?
- `backend/src/Seed.Application/Admin/Users/Queries/GetUsers/GetUsersQueryHandler.cs` — query EF Core con filtri, ordinamento, paginazione; ritorna `Result<PagedResult<AdminUserDto>>`
- `backend/src/Seed.Application/Admin/Users/Queries/GetUsers/GetUsersQueryValidator.cs`
- `backend/src/Seed.Application/Admin/Users/Queries/GetUserById/GetUserByIdQuery.cs`
- `backend/src/Seed.Application/Admin/Users/Queries/GetUserById/GetUserByIdQueryHandler.cs` — ritorna `Result<AdminUserDetailDto>`

### Step 5: Creare Commands

**File da creare (per ogni command: Command.cs, CommandHandler.cs, CommandValidator.cs):**

1. **CreateUser**: `backend/src/Seed.Application/Admin/Users/Commands/CreateUser/`
   - Input: Email, FirstName, LastName, Password, RoleIds[]
   - Handler: crea utente via `UserManager`, assegna ruoli, log audit `UserCreated`
   - Validator: email valida, nome/cognome non vuoti, password valida

2. **UpdateUser**: `backend/src/Seed.Application/Admin/Users/Commands/UpdateUser/`
   - Input: UserId, FirstName, LastName, Email
   - Handler: aggiorna campi, log audit `UserUpdated` con diff prima/dopo
   - Protezione: non modifica se stesso (per ruoli)

3. **DeleteUser**: `backend/src/Seed.Application/Admin/Users/Commands/DeleteUser/`
   - Input: UserId, CurrentUserId (dal controller)
   - Handler: soft delete (`IsDeleted = true`, `DeletedAt = now`), blacklist token, log audit `UserDeleted`
   - Protezione: no auto-eliminazione, no eliminazione SuperAdmin

4. **ToggleUserStatus**: `backend/src/Seed.Application/Admin/Users/Commands/ToggleUserStatus/`
   - Input: UserId, IsActive, CurrentUserId
   - Handler: toggle `IsActive`, se disattivato → blacklist token, log audit `UserStatusChanged`
   - Protezione: no disattivazione SuperAdmin, no auto-disattivazione

5. **AssignUserRoles**: `backend/src/Seed.Application/Admin/Users/Commands/AssignUserRoles/`
   - Input: UserId, RoleNames[], CurrentUserId
   - Handler: rimuovi ruoli vecchi, assegna nuovi, invalida cache permessi + blacklist token, log audit `UserRolesChanged`
   - Protezione: no modifica ruolo SuperAdmin (assegnato a utente originale)

6. **ForcePasswordChange**: `backend/src/Seed.Application/Admin/Users/Commands/ForcePasswordChange/`
   - Input: UserId
   - Handler: setta `MustChangePassword = true`, blacklist token, log audit

7. **AdminResetPassword**: `backend/src/Seed.Application/Admin/Users/Commands/AdminResetPassword/`
   - Input: UserId
   - Handler: genera reset token via `UserManager`, invia email via `IEmailService`, log audit

### Step 6: Creare AdminUsersController

**File da creare:**
- `backend/src/Seed.Api/Controllers/AdminUsersController.cs`

```
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/users")]
[Authorize]
```

Endpoint:
- `GET /` → `[HasPermission(Permissions.Users.Read)]` → `GetUsersQuery`
- `GET /{id}` → `[HasPermission(Permissions.Users.Read)]` → `GetUserByIdQuery`
- `POST /` → `[HasPermission(Permissions.Users.Create)]` → `CreateUserCommand`
- `PUT /{id}` → `[HasPermission(Permissions.Users.Update)]` → `UpdateUserCommand`
- `DELETE /{id}` → `[HasPermission(Permissions.Users.Delete)]` → `DeleteUserCommand`
- `PUT /{id}/status` → `[HasPermission(Permissions.Users.ToggleStatus)]` → `ToggleUserStatusCommand`
- `PUT /{id}/roles` → `[HasPermission(Permissions.Users.AssignRoles)]` → `AssignUserRolesCommand`
- `POST /{id}/force-password-change` → `[HasPermission(Permissions.Users.Update)]` → `ForcePasswordChangeCommand`
- `POST /{id}/reset-password` → `[HasPermission(Permissions.Users.Update)]` → `AdminResetPasswordCommand`

Il controller inietta `ISender` e arricchisce i comandi con `UserId` dal JWT claim e `IpAddress`/`UserAgent` dall'HttpContext.

### Step 7: Test

**File da creare:**

Unit tests (in `backend/tests/Seed.UnitTests/Admin/Users/`):
- `CreateUserCommandHandlerTests.cs` — creazione ok, email duplicata, validazione
- `UpdateUserCommandHandlerTests.cs` — aggiornamento ok, utente non trovato
- `DeleteUserCommandHandlerTests.cs` — soft delete ok, no auto-eliminazione, no eliminazione SuperAdmin
- `ToggleUserStatusCommandHandlerTests.cs` — toggle ok, no disattivazione SuperAdmin, token blacklist su disattivazione
- `AssignUserRolesCommandHandlerTests.cs` — assegnazione ok, no modifica SuperAdmin, invalidazione cache
- `GetUsersQueryHandlerTests.cs` — paginazione, filtri

Integration tests (in `backend/tests/Seed.IntegrationTests/Admin/`):
- `AdminUsersEndpointsTests.cs` — tutti gli endpoint con verifica permessi, protezioni, paginazione

## Criteri di completamento

1. **Build**: `dotnet build Seed.slnx` compila senza errori
2. **Migration**: migration creata e applicabile
3. **Endpoint funzionanti**: tutti i 9 endpoint rispondono correttamente
4. **Autorizzazione**: ogni endpoint rifiuta richieste senza il permesso corretto (403)
5. **Protezioni**: no auto-eliminazione, no eliminazione/disattivazione SuperAdmin, no auto-modifica ruoli SuperAdmin
6. **Soft delete**: utenti eliminati non compaiono nelle liste ma restano nel DB
7. **Token blacklist**: token invalidati su disattivazione e cambio ruoli
8. **Audit log**: tutte le operazioni loggate con dettagli prima/dopo
9. **Paginazione**: ricerca, filtri, ordinamento funzionanti
10. **Validazione**: tutti i comandi validati con FluentValidation
11. **Unit test**: tutti i test passano (`dotnet test tests/Seed.UnitTests`)
12. **Integration test**: tutti i test passano (`dotnet test tests/Seed.IntegrationTests`)
