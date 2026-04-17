namespace Seed.Application.Common.Models;

public sealed record ScheduledDowngradeResult(
    string ScheduleId,
    DateTime ScheduledDate);
