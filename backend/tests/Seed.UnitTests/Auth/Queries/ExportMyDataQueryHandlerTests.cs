using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Seed.Application.Auth.Queries.ExportMyData;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;
using Seed.Domain.Entities;

namespace Seed.UnitTests.Auth.Queries;

public class ExportMyDataQueryHandlerTests
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditLogReader _auditLogReader;
    private readonly IAuditService _auditService;
    private readonly ExportMyDataQueryHandler _handler;

    public ExportMyDataQueryHandlerTests()
    {
        var store = Substitute.For<IUserStore<ApplicationUser>>();
        _userManager = Substitute.For<UserManager<ApplicationUser>>(
            store, null, null, null, null, null, null, null, null);
        _auditLogReader = Substitute.For<IAuditLogReader>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new ExportMyDataQueryHandler(_userManager, _auditLogReader, _auditService);
    }

    [Fact]
    public async Task Should_Return_UserData_When_User_Exists()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            PrivacyPolicyAcceptedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TermsAcceptedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ConsentVersion = "1.0"
        };
        var auditEntries = new List<AuditLogEntry>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Timestamp = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                UserId = userId,
                Action = AuditActions.LoginSuccess,
                EntityType = "User",
                EntityId = userId.ToString(),
                IpAddress = "127.0.0.1"
            }
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string> { "User" });
        _auditLogReader.GetQueryable().Returns(auditEntries.AsQueryable());

        var query = new ExportMyDataQuery(userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.Profile.Id.Should().Be(userId);
        result.Data.Profile.Email.Should().Be("user@test.com");
        result.Data.Profile.FirstName.Should().Be("John");
        result.Data.Profile.LastName.Should().Be("Doe");
        result.Data.Profile.IsActive.Should().BeTrue();
        result.Data.Consent.PrivacyPolicyAcceptedAt.Should().NotBeNull();
        result.Data.Consent.ConsentVersion.Should().Be("1.0");
        result.Data.Roles.Should().Contain("User");
        result.Data.AuditLog.Should().HaveCount(1);
        result.Data.AuditLog[0].Action.Should().Be(AuditActions.LoginSuccess);
    }

    [Fact]
    public async Task Should_Fail_When_User_Not_Found()
    {
        var query = new ExportMyDataQuery(Guid.NewGuid());
        _userManager.FindByIdAsync(query.UserId.ToString()).Returns((ApplicationUser?)null);

        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeFalse();
        result.Errors.Should().Contain("User not found.");
    }

    [Fact]
    public async Task Should_Write_Audit_Log_For_Export()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _auditLogReader.GetQueryable().Returns(new List<AuditLogEntry>().AsQueryable());

        var query = new ExportMyDataQuery(userId);
        await _handler.Handle(query, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.DataExported,
            "User",
            userId.ToString(),
            "User data exported",
            userId,
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Should_Return_Empty_AuditLog_When_No_Entries()
    {
        var userId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@test.com",
            FirstName = "John",
            LastName = "Doe",
            IsActive = true
        };

        _userManager.FindByIdAsync(userId.ToString()).Returns(user);
        _userManager.GetRolesAsync(user).Returns(new List<string>());
        _auditLogReader.GetQueryable().Returns(new List<AuditLogEntry>().AsQueryable());

        var query = new ExportMyDataQuery(userId);
        var result = await _handler.Handle(query, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data!.AuditLog.Should().BeEmpty();
    }
}
