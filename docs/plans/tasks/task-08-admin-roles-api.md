# Task 08: API gestione ruoli (AdminRolesController)

## Contesto

### Stato attuale del codice rilevante
- **Domain**: `ApplicationRole` (IdentityRole<Guid>) ha `Description`, `CreatedAt`, `IsSystemRole`, `RolePermissions` nav property. `Permission` ha `Id`, `Name`, `Description`, `Category`. `RolePermission` è la join table con `RoleId`, `PermissionId`.
- **Permessi**: `Permissions.Roles.*` definiti: `Read`, `Create`, `Update`, `Delete`.
- **Audit actions**: `AuditActions.RoleCreated`, `RoleUpdated`, `RoleDeleted` già definiti.
- **Autorizzazione**: `HasPermissionAttribute` per decorare endpoint. SuperAdmin bypassa tutti i controlli.
- **Servizi**: `IPermissionService` (cache con invalidazione per userId), `ITokenBlacklistService`, `IAuditService`.
- **DbContext**: `ApplicationDbContext` espone `Permissions`, `RolePermissions`. Ruoli accessibili via `RoleManager<ApplicationRole>`.
- **Pattern consolidato**: T-07 (AdminUsersController) fornisce il pattern esatto per controller, comandi, query, validatori e test.
- **Seeder**: `RolesAndPermissionsSeeder` crea i 3 ruoli di sistema (SuperAdmin, Admin, User) con `IsSystemRole = true`.

### Dipendenze e vincoli
- **T-03** (autorizzazione permessi): ✅ Completato
- **T-06** (audit log): ✅ Completato
- Ruoli di sistema (`IsSystemRole = true`) non eliminabili
- Permessi SuperAdmin non modificabili (mantiene sempre tutti)
- Alla modifica permessi ruolo: invalidare cache permessi di tutti gli utenti con quel ruolo + blacklist token
- Non esiste `PagedResult<T>` per i ruoli (non necessario: i ruoli sono pochi, non serve paginazione server-side)

## Piano di esecuzione

### Step 1: Creare DTOs per Admin Roles

**File da creare:**
- `backend/src/Seed.Application/Admin/Roles/Models/AdminRoleDto.cs`

```csharp
public sealed record AdminRoleDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    DateTime CreatedAt);
```

- `backend/src/Seed.Application/Admin/Roles/Models/AdminRoleDetailDto.cs`

```csharp
public sealed record AdminRoleDetailDto(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    DateTime CreatedAt,
    IReadOnlyList<string> Permissions);
```

- `backend/src/Seed.Application/Admin/Roles/Models/PermissionDto.cs`

```csharp
public sealed record PermissionDto(
    Guid Id,
    string Name,
    string? Description,
    string Category);
```

### Step 2: Creare Queries

**File da creare:**

1. `backend/src/Seed.Application/Admin/Roles/Queries/GetRoles/GetRolesQuery.cs`
   - Record vuoto: `IRequest<Result<IReadOnlyList<AdminRoleDto>>>`
   - Handler: query via `RoleManager` + conteggio utenti per ruolo via `UserManager.GetUsersInRoleAsync()` o query diretta su `UserRoles`

2. `backend/src/Seed.Application/Admin/Roles/Queries/GetRoles/GetRolesQueryHandler.cs`
   - Usa `dbContext.Roles` per elenco ruoli
   - Per ogni ruolo: conta utenti via join su `AspNetUserRoles` (query aggregata, non N+1)
   - Mappa a `AdminRoleDto`

3. `backend/src/Seed.Application/Admin/Roles/Queries/GetRoleById/GetRoleByIdQuery.cs`
   - Input: `Guid Id`
   - Ritorna: `Result<AdminRoleDetailDto>`

4. `backend/src/Seed.Application/Admin/Roles/Queries/GetRoleById/GetRoleByIdQueryHandler.cs`
   - Carica ruolo con `RolePermissions` include
   - Conta utenti
   - Mappa a `AdminRoleDetailDto` con lista permessi

5. `backend/src/Seed.Application/Admin/Roles/Queries/GetPermissions/GetPermissionsQuery.cs`
   - Record vuoto: `IRequest<Result<IReadOnlyList<PermissionDto>>>`

6. `backend/src/Seed.Application/Admin/Roles/Queries/GetPermissions/GetPermissionsQueryHandler.cs`
   - Query `dbContext.Permissions` ordinata per `Category`, `Name`
   - Mappa a `PermissionDto`

### Step 3: Creare Commands

**Per ogni command: Command.cs, CommandHandler.cs, CommandValidator.cs**

1. **CreateRole**: `backend/src/Seed.Application/Admin/Roles/Commands/CreateRole/`
   - Input: `Name`, `Description`, `PermissionNames[]`
   - Enrichment (JsonIgnore): `CurrentUserId`, `IpAddress`, `UserAgent`
   - Handler:
     - Verifica nome non duplicato via `RoleManager.RoleExistsAsync()`
     - Crea `ApplicationRole` con `IsSystemRole = false`
     - Risolvi `PermissionNames` → `Permission` entità via `dbContext.Permissions`
     - Crea `RolePermission` entries
     - Log audit `RoleCreated`
   - Validator: `Name` non vuoto, max 100 chars; `PermissionNames` non null

2. **UpdateRole**: `backend/src/Seed.Application/Admin/Roles/Commands/UpdateRole/`
   - Input: `RoleId`, `Name`, `Description`, `PermissionNames[]`
   - Enrichment: `CurrentUserId`, `IpAddress`, `UserAgent`
   - Handler:
     - Trova ruolo via `RoleManager.FindByIdAsync()`
     - Blocca modifica permessi SuperAdmin (`if IsSystemRole && Name == SuperAdmin`)
     - Verifica nome non duplicato (se cambiato)
     - Aggiorna nome e descrizione via `RoleManager.UpdateAsync()`
     - Rimuovi vecchi `RolePermission`, crea nuovi
     - Invalida cache permessi per tutti gli utenti del ruolo
     - Blacklist token per tutti gli utenti del ruolo
     - Log audit `RoleUpdated` con diff prima/dopo
   - Validator: `RoleId` non vuoto, `Name` non vuoto, max 100 chars

3. **DeleteRole**: `backend/src/Seed.Application/Admin/Roles/Commands/DeleteRole/`
   - Input: `RoleId`
   - Enrichment: `CurrentUserId`, `IpAddress`, `UserAgent`
   - Handler:
     - Trova ruolo via `RoleManager.FindByIdAsync()`
     - Blocca eliminazione ruoli di sistema (`IsSystemRole`)
     - Blocca eliminazione se il ruolo ha utenti assegnati
     - Rimuovi `RolePermission` entries
     - Elimina ruolo via `RoleManager.DeleteAsync()`
     - Log audit `RoleDeleted`
   - Validator: `RoleId` non vuoto

### Step 4: Creare AdminRolesController

**File da creare:**
- `backend/src/Seed.Api/Controllers/AdminRolesController.cs`

```
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/roles")]
[Authorize]
```

Endpoint:
- `GET /` → `[HasPermission(Permissions.Roles.Read)]` → `GetRolesQuery`
- `GET /{id}` → `[HasPermission(Permissions.Roles.Read)]` → `GetRoleByIdQuery`
- `POST /` → `[HasPermission(Permissions.Roles.Create)]` → `CreateRoleCommand`
- `PUT /{id}` → `[HasPermission(Permissions.Roles.Update)]` → `UpdateRoleCommand`
- `DELETE /{id}` → `[HasPermission(Permissions.Roles.Delete)]` → `DeleteRoleCommand`
- `GET /permissions` → `[HasPermission(Permissions.Roles.Read)]` → `GetPermissionsQuery`

Pattern: stesso di `AdminUsersController` — inietta `ISender`, arricchisce comandi con `CurrentUserId`, `IpAddress`, `UserAgent`.

### Step 5: Invalidazione cache e blacklist per utenti del ruolo

**Logica chiave in UpdateRoleCommandHandler:**
- Dopo modifica permessi, trovare tutti gli utenti con quel ruolo: `userManager.GetUsersInRoleAsync(roleName)`
- Per ciascuno: `permissionService.InvalidateUserPermissionsCacheAsync(userId)` + `tokenBlacklistService.BlacklistUserTokensAsync(userId)`
- Questo è necessario solo se i permessi cambiano (confrontare prima/dopo)

### Step 6: Test

**Unit tests da creare** (in `backend/tests/Seed.UnitTests/Admin/Roles/`):
- `CreateRoleCommandHandlerTests.cs` — creazione ok, nome duplicato, validazione
- `UpdateRoleCommandHandlerTests.cs` — aggiornamento ok, blocco SuperAdmin, invalidazione cache
- `DeleteRoleCommandHandlerTests.cs` — eliminazione ok, blocco ruoli sistema, blocco ruoli con utenti

**Integration tests da creare** (in `backend/tests/Seed.IntegrationTests/Admin/`):
- `AdminRolesEndpointsTests.cs` — tutti gli endpoint con verifica permessi, protezioni, CRUD completo

## Criteri di completamento

1. **Build**: `dotnet build Seed.slnx` compila senza errori
2. **Endpoint funzionanti**: tutti i 6 endpoint rispondono correttamente
3. **Autorizzazione**: ogni endpoint rifiuta richieste senza il permesso corretto (403)
4. **Protezioni**: no eliminazione ruoli sistema, no modifica permessi SuperAdmin, no eliminazione ruoli con utenti assegnati
5. **Cache invalidation**: alla modifica permessi di un ruolo, la cache di tutti gli utenti del ruolo viene invalidata
6. **Token blacklist**: alla modifica permessi, i token degli utenti impattati vengono invalidati
7. **Audit log**: tutte le operazioni loggate con dettaglio prima/dopo
8. **Validazione**: tutti i comandi validati con FluentValidation
9. **Permissions endpoint**: ritorna la lista completa di tutti i permessi disponibili (per la matrice UI)
10. **Unit test**: tutti i test passano (`dotnet test tests/Seed.UnitTests`)
11. **Integration test**: tutti i test passano (`dotnet test tests/Seed.IntegrationTests`)

## Risultato

### File modificati/creati

**Nuovi file (Application layer - DTOs):**
- `backend/src/Seed.Application/Admin/Roles/Models/AdminRoleDto.cs`
- `backend/src/Seed.Application/Admin/Roles/Models/AdminRoleDetailDto.cs`
- `backend/src/Seed.Application/Admin/Roles/Models/PermissionDto.cs`

**Nuovi file (Application layer - Queries):**
- `backend/src/Seed.Application/Admin/Roles/Queries/GetRoles/GetRolesQuery.cs`
- `backend/src/Seed.Application/Admin/Roles/Queries/GetRoles/GetRolesQueryHandler.cs`
- `backend/src/Seed.Application/Admin/Roles/Queries/GetRoleById/GetRoleByIdQuery.cs`
- `backend/src/Seed.Application/Admin/Roles/Queries/GetRoleById/GetRoleByIdQueryHandler.cs`
- `backend/src/Seed.Application/Admin/Roles/Queries/GetPermissions/GetPermissionsQuery.cs`
- `backend/src/Seed.Application/Admin/Roles/Queries/GetPermissions/GetPermissionsQueryHandler.cs`

**Nuovi file (Application layer - Commands):**
- `backend/src/Seed.Application/Admin/Roles/Commands/CreateRole/CreateRoleCommand.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/CreateRole/CreateRoleCommandHandler.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/CreateRole/CreateRoleCommandValidator.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/UpdateRole/UpdateRoleCommand.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/UpdateRole/UpdateRoleCommandHandler.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/UpdateRole/UpdateRoleCommandValidator.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/DeleteRole/DeleteRoleCommand.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/DeleteRole/DeleteRoleCommandHandler.cs`
- `backend/src/Seed.Application/Admin/Roles/Commands/DeleteRole/DeleteRoleCommandValidator.cs`

**Nuovo file (API layer):**
- `backend/src/Seed.Api/Controllers/AdminRolesController.cs`

**File modificati:**
- `backend/src/Seed.Application/Common/Interfaces/IPermissionService.cs` — aggiunti 4 metodi per gestione permessi ruolo
- `backend/src/Seed.Infrastructure/Services/PermissionService.cs` — implementati i 4 nuovi metodi

**Nuovi file (Test):**
- `backend/tests/Seed.UnitTests/Admin/Roles/CreateRoleCommandHandlerTests.cs` (5 test)
- `backend/tests/Seed.UnitTests/Admin/Roles/UpdateRoleCommandHandlerTests.cs` (6 test)
- `backend/tests/Seed.UnitTests/Admin/Roles/DeleteRoleCommandHandlerTests.cs` (5 test)
- `backend/tests/Seed.IntegrationTests/Admin/AdminRolesEndpointsTests.cs` (10 test)

### Scelte implementative e motivazioni

1. **Estensione di `IPermissionService`** anziché accesso diretto al DbContext: il layer Application non referenzia EF Core né Infrastructure. Anziché creare una nuova interfaccia repository, ho esteso `IPermissionService` con 4 metodi (`GetAllPermissionsAsync`, `GetRolePermissionNamesAsync`, `SetRolePermissionsAsync`, `RemoveAllRolePermissionsAsync`) — coerente con la responsabilità esistente del servizio (gestione permessi) e implementato nell'infrastruttura esistente.

2. **Nessuna paginazione per i ruoli**: come indicato nel mini-plan, i ruoli sono pochi e non necessitano paginazione server-side. GetRolesQuery ritorna una lista semplice.

3. **Cache invalidation solo se i permessi cambiano**: in UpdateRoleCommandHandler, il confronto prima/dopo dei permessi evita invalidazioni inutili quando si modifica solo nome/descrizione.

4. **Pattern controller identico a AdminUsersController**: stessa struttura con `CurrentUserId`, `IpAddress`, `UserAgent` enrichment, stessi status code HTTP (200, 201, 204, 400, 404).

### Deviazioni dal piano

1. **Accesso dati tramite IPermissionService anziché DbContext diretto**: il piano suggeriva `dbContext.Roles` e `dbContext.Permissions` nei query handler, ma il layer Application non ha accesso al DbContext. Ho usato `RoleManager` + `UserManager` + `IPermissionService` (esteso) per mantenere la separazione architetturale esistente.

2. **Conteggio utenti via `UserManager.GetUsersInRoleAsync`** anziché join su `AspNetUserRoles`: il piano suggeriva una query aggregata diretta, ma senza accesso al DbContext dal layer Application, ho usato l'API Identity. Per il numero limitato di ruoli questo è accettabile.

### Risultati test
- **Build**: `dotnet build Seed.slnx` — 0 errori
- **Unit test**: 152 test passati (16 nuovi per admin roles)
- **Integration test**: 73 test passati (10 nuovi per admin roles endpoints)
