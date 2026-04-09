using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Seed.Application.Common.Interfaces;
using Seed.Infrastructure.Services;
using Seed.Shared.Configuration;

namespace Seed.UnitTests.Services;

public class DataRetentionBackgroundServiceTests
{
    private readonly IDataCleanupService _cleanupService;
    private readonly DataRetentionBackgroundService _sut;

    public DataRetentionBackgroundServiceTests()
    {
        _cleanupService = Substitute.For<IDataCleanupService>();
        _cleanupService.PurgeSoftDeletedUsersAsync(Arg.Any<CancellationToken>()).Returns(3);
        _cleanupService.CleanupExpiredRefreshTokensAsync(Arg.Any<CancellationToken>()).Returns(150);
        _cleanupService.CleanupOldAuditLogEntriesAsync(Arg.Any<CancellationToken>()).Returns(0);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IDataCleanupService)).Returns(_cleanupService);

        var serviceScope = Substitute.For<IServiceScope>();
        serviceScope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(serviceScope);

        var settings = Options.Create(new DataRetentionSettings { CleanupIntervalHours = 24 });
        var logger = Substitute.For<ILogger<DataRetentionBackgroundService>>();

        _sut = new DataRetentionBackgroundService(scopeFactory, settings, logger);
    }

    [Fact]
    public async Task RunCleanupCycle_Calls_All_Three_Cleanup_Methods()
    {
        await _sut.RunCleanupCycleAsync(CancellationToken.None);

        await _cleanupService.Received(1).PurgeSoftDeletedUsersAsync(Arg.Any<CancellationToken>());
        await _cleanupService.Received(1).CleanupExpiredRefreshTokensAsync(Arg.Any<CancellationToken>());
        await _cleanupService.Received(1).CleanupOldAuditLogEntriesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCleanupCycle_Continues_When_PurgeSoftDeletedUsers_Throws()
    {
        _cleanupService.PurgeSoftDeletedUsersAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _sut.RunCleanupCycleAsync(CancellationToken.None);

        await _cleanupService.Received(1).CleanupExpiredRefreshTokensAsync(Arg.Any<CancellationToken>());
        await _cleanupService.Received(1).CleanupOldAuditLogEntriesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCleanupCycle_Continues_When_CleanupExpiredRefreshTokens_Throws()
    {
        _cleanupService.CleanupExpiredRefreshTokensAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _sut.RunCleanupCycleAsync(CancellationToken.None);

        await _cleanupService.Received(1).PurgeSoftDeletedUsersAsync(Arg.Any<CancellationToken>());
        await _cleanupService.Received(1).CleanupOldAuditLogEntriesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunCleanupCycle_Continues_When_CleanupOldAuditLogEntries_Throws()
    {
        _cleanupService.CleanupOldAuditLogEntriesAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB error"));

        await _sut.RunCleanupCycleAsync(CancellationToken.None);

        await _cleanupService.Received(1).PurgeSoftDeletedUsersAsync(Arg.Any<CancellationToken>());
        await _cleanupService.Received(1).CleanupExpiredRefreshTokensAsync(Arg.Any<CancellationToken>());
    }
}
