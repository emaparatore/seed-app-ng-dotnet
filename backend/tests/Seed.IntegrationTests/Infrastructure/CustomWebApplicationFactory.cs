using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Seed.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Seed.IntegrationTests.Infrastructure;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer? _postgres;
    private readonly string? _externalConnectionString;

    public CustomWebApplicationFactory()
    {
        // If TEST_CONNECTION_STRING is set (e.g. in Docker), use it directly.
        // Otherwise, spin up a Testcontainers PostgreSQL instance.
        _externalConnectionString = Environment.GetEnvironmentVariable("TEST_CONNECTION_STRING");
        if (_externalConnectionString is null)
        {
            _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
        }
    }

    private string ConnectionString => _externalConnectionString ?? _postgres!.GetConnectionString();

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

            // Ensure database is created and migrated
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.Migrate();
        });

        builder.UseEnvironment("Development");
    }

    public async Task InitializeAsync()
    {
        if (_postgres is not null)
            await _postgres.StartAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
    }
}
