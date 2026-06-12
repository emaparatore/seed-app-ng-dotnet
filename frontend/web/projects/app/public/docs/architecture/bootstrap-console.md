# Bootstrap Console Application

The `Seed.Bootstrap` console application is a dedicated tool for initializing production environments. It handles:
- Configuration validation for production safety
- Database seeding (roles, permissions, admin user, system settings)
- Runs separately from the main API to avoid runtime overhead

## When to use

Run `Seed.Bootstrap` **once** during initial production deployment, before or after the API starts:

1. First-time deploy: Run after applying migrations to seed required data
2. Normal operation: The API starts without needing the bootstrap runner
3. Adding new seeders: Create a new seeder and register it (see below)

## Running the bootstrap

### Locally

Run from the `backend/` directory:

```bash
dotnet run --project src/Seed.Bootstrap
```

### Production (Docker)

In your deployment/CI pipeline, run the bootstrap container after migrations apply:

```bash
docker run --rm \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Host=postgres;Database=seeddb;Username=seed;Password=seed_password" \
  -e JwtSettings__Secret="your-production-secret-here" \
  your-registry/seed-bootstrap:latest
```

Or with docker-compose:

```yaml
services:
  bootstrap:
    image: your-registry/seed-bootstrap:latest
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnection: "Host=postgres;Database=seeddb;Username=seed;Password=seed_password"
      JwtSettings__Secret: ${JWT_SECRET}
    depends_on:
      - postgres
    networks:
      - seed-network
```

## Configuration validation

In **production**, the bootstrap validates:

- **JWT Secret:** Must be set via `JwtSettings__Secret` env var and NOT contain `ForDevelopmentOnly`
- **Database connection:** Must be set via `ConnectionStrings__DefaultConnection` and NOT contain the dev password `seed_password`

If validation fails, the bootstrap will exit with an error — preventing accidental misconfigurations.

In **development/staging**, validation is skipped.

## Existing seeders

The bootstrap runs these seeders in order:

### 1. RolesAndPermissionsSeeder
Creates system roles (SuperAdmin, Admin, User) and their permissions. Idempotent — skips existing roles/permissions.

### 2. SuperAdminSeeder
Creates the initial SuperAdmin user (email: `admin@example.com`, password: see source code). Update or remove after first login.

### 3. SystemSettingsSeeder
Creates default system settings (e.g., email notifications on/off, feature flags). Idempotent — skips existing settings.

## Adding a new seeder

### 1. Create the seeder class

In `backend/src/Seed.Infrastructure/Persistence/Seeders/`, create a new file:

```csharp
using Microsoft.Extensions.Logging;

namespace Seed.Infrastructure.Persistence.Seeders;

public class MyDataSeeder
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<MyDataSeeder> _logger;

    public MyDataSeeder(
        ApplicationDbContext dbContext,
        ILogger<MyDataSeeder> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        // Check if data already exists (idempotent)
        if (await _dbContext.MyEntities.AnyAsync())
        {
            _logger.LogDebug("MyEntities already exist, skipping");
            return;
        }

        // Seed data
        var entities = new[]
        {
            new MyEntity { /* ... */ },
        };

        _dbContext.MyEntities.AddRange(entities);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Seeded {Count} MyEntities", entities.Length);
    }
}
```

### 2. Register in DI

Add to `InfrastructureServiceCollectionExtensions.cs` (in `backend/src/Seed.Infrastructure/`):

```csharp
services.AddScoped<RolesAndPermissionsSeeder>();
services.AddScoped<SuperAdminSeeder>();
services.AddScoped<SystemSettingsSeeder>();
services.AddScoped<MyDataSeeder>();  // Add this line
```

### 3. Call in bootstrap

Update `backend/src/Seed.Bootstrap/Program.cs`:

```csharp
static async Task SeedApplicationDataAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();

    var rolesSeeder = scope.ServiceProvider.GetRequiredService<RolesAndPermissionsSeeder>();
    await rolesSeeder.SeedAsync();

    var adminSeeder = scope.ServiceProvider.GetRequiredService<SuperAdminSeeder>();
    await adminSeeder.SeedAsync();

    var settingsSeeder = scope.ServiceProvider.GetRequiredService<SystemSettingsSeeder>();
    await settingsSeeder.SeedAsync();

    var myDataSeeder = scope.ServiceProvider.GetRequiredService<MyDataSeeder>();
    await myDataSeeder.SeedAsync();  // Add this line
}
```

## Best practices

- **Idempotent:** Always check if data exists before seeding (use `.Any()`, `.FirstOrDefault()`, etc.). Running the seeder multiple times should be safe.
- **Logging:** Log what you seed at `LogInformation` level, skip cases at `LogDebug`. Helps ops teams understand what happened.
- **Order matters:** Seeders run in the order they're called. If one seeder depends on another (e.g., admin user depends on roles), order them correctly.
- **No API calls:** Seeders run offline, only use `DbContext` and internal services. Don't call external APIs.
- **Small scope:** Each seeder should handle one concern (roles, one user, settings, etc.). Keep them focused.

## Environment variables

Required for production:

| Variable | Default (Dev) | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Development` | Set to `Production` for production runs |
| `ConnectionStrings__DefaultConnection` | Dev PostgreSQL string | Must be set to production DB |
| `JwtSettings__Secret` | Dev secret | Must be set to production secret |
| `ASPNETCORE_URLS` | `http://localhost:5000` | Optional, bootstrapper doesn't listen for HTTP |

## Logs

Seeders use Serilog. Logs are written to:
- **Console:** Always, for quick feedback
- **Seq:** If configured in `appsettings.json` (optional)

Check logs to verify seeding completed:
```
[11:23:45 INF] Seeded 3 permissions
[11:23:46 INF] Created system role SuperAdmin
[11:23:47 INF] Created system role Admin
[11:23:47 INF] Created system role User
```

## Troubleshooting

**"JWT Secret must be configured for production"**
- Set the `JwtSettings__Secret` environment variable to a production-safe value
- Don't use development values like `YourSuperSecret...ForDevelopmentOnly`

**"Database connection string must be configured for production"**
- Set `ConnectionStrings__DefaultConnection` to your production PostgreSQL URI
- Don't use the hardcoded dev password `seed_password`

**Seeder times out**
- Check database connectivity: `ping <db-host>`
- Check if migrations have been applied: `SELECT name FROM __EFMigrationsHistory`
- Check database user permissions: user must have `CREATE`, `INSERT`, `UPDATE` on all tables

**"Role X already exists"**
- This is expected on subsequent runs (seeder is idempotent)
- No action needed — seeding is safe to run multiple times
