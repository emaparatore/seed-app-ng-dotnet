namespace Seed.Application.Common.Interfaces;

public interface IUserPurgeService
{
    Task PurgeUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
