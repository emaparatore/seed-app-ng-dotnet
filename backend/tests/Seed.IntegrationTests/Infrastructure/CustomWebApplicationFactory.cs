using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Seed.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Seed.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres;
    private readonly string? _externalConnectionString;
    private readonly string _instanceDbName;

    public CustomWebApplicationFactory()
    {
        // If TEST_CONNECTION_STRING is set (e.g. in Docker), use it directly
        // with a unique DB name per factory instance so test classes can run in parallel.
        // Otherwise, spin up a Testcontainers PostgreSQL instance.
        _externalConnectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
        _instanceDbName = $"seeddb_test_{Guid.NewGuid():N}";

        if (_externalConnectionString is null)
        {
            _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        }
    }

    private string ConnectionString
    {
        get
        {
            if (_postgres is not null)
                return _postgres.GetConnectionString();

            var builder = new NpgsqlConnectionStringBuilder(_externalConnectionString!);
            builder.Database = _instanceDbName;
            return builder.ConnectionString;
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // Add test DbContext
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(ConnectionString));

            // Replace health check to use the test connection string
            services.Configure<HealthCheckServiceOptions>(options =>
            {
                options.Registrations.Clear();
            });
            services.AddHealthChecks()
                .AddNpgSql(ConnectionString, name: "postgresql", tags: ["db", "ready"]);

            // Ensure database is migrated
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
        });

        // Disable rate limiting for tests
        builder.UseSetting("RateLimiting:Auth:PermitLimit", int.MaxValue.ToString());
        builder.UseSetting("RateLimiting:AuthSensitive:PermitLimit", int.MaxValue.ToString());

        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.StartAsync();
        }
        else if (_externalConnectionString is not null)
        {
            // Create a unique database for this factory instance.
            // Each statement must be executed separately — Npgsql pipelines
            // do not allow CREATE/DROP DATABASE inside a batch.
            await using var conn = await OpenAdminConnectionAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"""CREATE DATABASE "{_instanceDbName}" """;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
        else if (_externalConnectionString is not null)
        {
            // Clean up the unique database after tests complete.
            await using var conn = await OpenAdminConnectionAsync();

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"""
                    SELECT pg_terminate_backend(pid)
                    FROM pg_stat_activity
                    WHERE datname = '{_instanceDbName}' AND pid <> pg_backend_pid()
                    """;
                await cmd.ExecuteNonQueryAsync();
            }

            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = $"""DROP DATABASE IF EXISTS "{_instanceDbName}" """;
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<NpgsqlConnection> OpenAdminConnectionAsync()
    {
        var builder = new NpgsqlConnectionStringBuilder(_externalConnectionString!);
        builder.Database = "postgres";
        var conn = new NpgsqlConnection(builder.ConnectionString);
        await conn.OpenAsync();
        return conn;
    }
}
