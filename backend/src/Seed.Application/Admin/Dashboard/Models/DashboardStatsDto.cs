namespace Seed.Application.Admin.Dashboard.Models;

public sealed record DashboardStatsDto(
    int TotalUsers,
    int ActiveUsers,
    int InactiveUsers,
    int RegistrationsLast7Days,
    int RegistrationsLast30Days,
    IReadOnlyList<DailyRegistrationDto> RegistrationTrend,
    IReadOnlyList<RoleDistributionDto> UsersByRole,
    IReadOnlyList<RecentActivityDto> RecentActivity);
