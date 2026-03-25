using MediatR;
using Microsoft.AspNetCore.Identity;
using Seed.Application.Admin.Dashboard.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Domain.Entities;

namespace Seed.Application.Admin.Dashboard.Queries.GetDashboardStats;

public sealed class GetDashboardStatsQueryHandler(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    IAuditLogReader auditLogReader)
    : IRequestHandler<GetDashboardStatsQuery, Result<DashboardStatsDto>>
{
    public async Task<Result<DashboardStatsDto>> Handle(
        GetDashboardStatsQuery request, CancellationToken cancellationToken)
    {
        var users = userManager.Users.Where(u => !u.IsDeleted).ToList();

        var totalUsers = users.Count;
        var activeUsers = users.Count(u => u.IsActive);
        var inactiveUsers = totalUsers - activeUsers;

        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var registrationsLast7Days = users.Count(u => u.CreatedAt >= sevenDaysAgo);
        var registrationsLast30Days = users.Count(u => u.CreatedAt >= thirtyDaysAgo);

        // Registration trend: last 30 days grouped by day, filling missing days with 0
        var registrationsByDay = users
            .Where(u => u.CreatedAt >= thirtyDaysAgo)
            .GroupBy(u => DateOnly.FromDateTime(u.CreatedAt))
            .ToDictionary(g => g.Key, g => g.Count());

        var trend = new List<DailyRegistrationDto>();
        for (var i = 29; i >= 0; i--)
        {
            var date = DateOnly.FromDateTime(now.AddDays(-i));
            var count = registrationsByDay.GetValueOrDefault(date, 0);
            trend.Add(new DailyRegistrationDto(date, count));
        }

        // Users by role
        var roles = roleManager.Roles.OrderBy(r => r.Name).ToList();
        var usersByRole = new List<RoleDistributionDto>();
        foreach (var role in roles)
        {
            var usersInRole = await userManager.GetUsersInRoleAsync(role.Name!);
            var activeCount = usersInRole.Count(u => !u.IsDeleted);
            usersByRole.Add(new RoleDistributionDto(role.Name!, activeCount));
        }

        // Recent activity: last 5 audit log entries
        var recentEntries = auditLogReader.GetQueryable()
            .OrderByDescending(e => e.Timestamp)
            .Take(5)
            .ToList();

        var recentActivity = recentEntries
            .Select(e => new RecentActivityDto(e.Id, e.Timestamp, e.Action, e.EntityType, e.UserId))
            .ToList();

        var stats = new DashboardStatsDto(
            totalUsers,
            activeUsers,
            inactiveUsers,
            registrationsLast7Days,
            registrationsLast30Days,
            trend,
            usersByRole,
            recentActivity);

        return Result<DashboardStatsDto>.Success(stats);
    }
}
