using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NSubstitute;
using Seed.Application.Admin.Settings.Models;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;
using Seed.Infrastructure.Persistence;
using Seed.Infrastructure.Services;

namespace Seed.UnitTests.Admin.Settings;

public class SystemSettingsServiceTests : IDisposable
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly IAuditService _auditService;
    private readonly SystemSettingsService _service;

    public SystemSettingsServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new ApplicationDbContext(options);
        _cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        _auditService = Substitute.For<IAuditService>();
        _service = new SystemSettingsService(_dbContext, _cache, _auditService);
    }

    [Fact]
    public async Task GetAllAsync_Returns_Settings_From_Database_On_Cache_Miss()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Key", Value = "42", Type = "int", Category = "Test", Description = "A test setting"
        });
        await _dbContext.SaveChangesAsync();

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("Test.Key");
        result[0].Value.Should().Be("42");
    }

    [Fact]
    public async Task GetAllAsync_Returns_Settings_From_Cache_On_Cache_Hit()
    {
        var cached = new List<SystemSettingDto>
        {
            new("Cached.Key", "cached", "string", "Cached", "From cache", null, null)
        };
        await _cache.SetStringAsync("system_settings:all", JsonSerializer.Serialize(cached));

        var result = await _service.GetAllAsync();

        result.Should().HaveCount(1);
        result[0].Key.Should().Be("Cached.Key");
        // DB is empty, so this proves cache was used
        var dbCount = await _dbContext.SystemSettings.CountAsync();
        dbCount.Should().Be(0);
    }

    [Fact]
    public async Task GetAllAsync_Populates_Cache_After_Database_Query()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Key", Value = "value", Type = "string", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();

        await _service.GetAllAsync();

        var cachedValue = await _cache.GetStringAsync("system_settings:all");
        cachedValue.Should().NotBeNull();
        var deserialized = JsonSerializer.Deserialize<List<SystemSettingDto>>(cachedValue!);
        deserialized.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateAsync_Invalidates_Cache()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Key", Value = "old", Type = "string", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();
        await _cache.SetStringAsync("system_settings:all", "cached-data");

        var changes = new List<UpdateSettingItem> { new("Test.Key", "new") };
        await _service.UpdateAsync(changes, Guid.NewGuid(), null, null);

        var cachedValue = await _cache.GetStringAsync("system_settings:all");
        cachedValue.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_Logs_Audit_With_Before_After()
    {
        var userId = Guid.NewGuid();
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Key", Value = "old", Type = "string", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();

        var changes = new List<UpdateSettingItem> { new("Test.Key", "new") };
        await _service.UpdateAsync(changes, userId, "127.0.0.1", "TestAgent");

        await _auditService.Received(1).LogAsync(
            AuditActions.SettingsChanged,
            "SystemSetting",
            Arg.Is<string?>(s => s == null),
            Arg.Is<string?>(d => d != null && d.Contains("old") && d.Contains("new")),
            userId,
            "127.0.0.1",
            "TestAgent",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_Returns_Error_For_Unknown_Key()
    {
        var changes = new List<UpdateSettingItem> { new("NonExistent.Key", "value") };

        var result = await _service.UpdateAsync(changes, Guid.NewGuid(), null, null);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("NonExistent.Key"));
    }

    [Fact]
    public async Task UpdateAsync_Validates_Bool_Type()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Bool", Value = "true", Type = "bool", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();

        var changes = new List<UpdateSettingItem> { new("Test.Bool", "notabool") };
        var result = await _service.UpdateAsync(changes, Guid.NewGuid(), null, null);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("true") && e.Contains("false"));
    }

    [Fact]
    public async Task UpdateAsync_Validates_Int_Type()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Int", Value = "5", Type = "int", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();

        var changes = new List<UpdateSettingItem> { new("Test.Int", "notanumber") };
        var result = await _service.UpdateAsync(changes, Guid.NewGuid(), null, null);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("integer"));
    }

    [Fact]
    public async Task UpdateAsync_Skips_Unchanged_Values()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Key", Value = "same", Type = "string", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();

        var changes = new List<UpdateSettingItem> { new("Test.Key", "same") };
        var result = await _service.UpdateAsync(changes, Guid.NewGuid(), null, null);

        result.Succeeded.Should().BeTrue();
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetValueAsync_Returns_Value_For_Existing_Key()
    {
        _dbContext.SystemSettings.Add(new SystemSetting
        {
            Key = "Test.Key", Value = "hello", Type = "string", Category = "Test"
        });
        await _dbContext.SaveChangesAsync();

        var value = await _service.GetValueAsync("Test.Key");

        value.Should().Be("hello");
    }

    [Fact]
    public async Task GetValueAsync_Returns_Null_For_Missing_Key()
    {
        var value = await _service.GetValueAsync("NonExistent");

        value.Should().BeNull();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
