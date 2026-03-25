using FluentAssertions;
using NSubstitute;
using Seed.Application.Auth.Commands.Logout;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Authorization;

namespace Seed.UnitTests.Auth.Commands;

public class LogoutCommandHandlerTests
{
    private readonly ITokenService _tokenService;
    private readonly IAuditService _auditService;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _tokenService = Substitute.For<ITokenService>();
        _auditService = Substitute.For<IAuditService>();
        _handler = new LogoutCommandHandler(_tokenService, _auditService);
    }

    [Fact]
    public async Task Should_Revoke_Token_And_Return_Success()
    {
        var command = new LogoutCommand("some-refresh-token");

        var result = await _handler.Handle(command, CancellationToken.None);

        result.Succeeded.Should().BeTrue();
        result.Data.Should().BeTrue();
        await _tokenService.Received(1).RevokeTokenAsync("some-refresh-token");
    }

    [Fact]
    public async Task Should_Log_Logout_Audit()
    {
        var userId = Guid.NewGuid();
        var command = new LogoutCommand("some-refresh-token") { UserId = userId };

        await _handler.Handle(command, CancellationToken.None);

        await _auditService.Received(1).LogAsync(
            AuditActions.Logout, "User",
            userId.ToString(), Arg.Any<string?>(), userId,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }
}
