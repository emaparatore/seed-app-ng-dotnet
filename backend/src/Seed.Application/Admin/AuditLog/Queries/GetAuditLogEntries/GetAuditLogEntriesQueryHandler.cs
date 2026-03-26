using MediatR;
using Seed.Application.Admin.AuditLog.Models;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;
using Seed.Application.Common.Models;

namespace Seed.Application.Admin.AuditLog.Queries.GetAuditLogEntries;

public sealed class GetAuditLogEntriesQueryHandler(
    IAuditLogReader auditLogReader)
    : IRequestHandler<GetAuditLogEntriesQuery, Result<PagedResult<AuditLogEntryDto>>>
{
    public Task<Result<PagedResult<AuditLogEntryDto>>> Handle(
        GetAuditLogEntriesQuery request, CancellationToken cancellationToken)
    {
        var query = auditLogReader.GetQueryable();

        // Filter by action
        if (!string.IsNullOrWhiteSpace(request.ActionFilter))
            query = query.Where(e => e.Action == request.ActionFilter);

        // Filter by user
        if (request.UserId.HasValue)
            query = query.Where(e => e.UserId == request.UserId.Value);

        // Filter by date range
        if (request.DateFrom.HasValue)
            query = query.Where(e => e.Timestamp >= request.DateFrom.Value);
        if (request.DateTo.HasValue)
            query = query.Where(e => e.Timestamp <= request.DateTo.Value);

        // Search in Details
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var term = request.SearchTerm.ToLower();
            query = query.Where(e => e.Details != null && e.Details.ToLower().Contains(term));
        }

        // Sorting
        query = request.SortDescending
            ? query.OrderByDescending(e => e.Timestamp)
            : query.OrderBy(e => e.Timestamp);

        // Get total count
        var allEntries = query.ToList();
        var totalCount = allEntries.Count;

        // Pagination
        var pagedEntries = allEntries
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(e => new AuditLogEntryDto(
                e.Id, e.Timestamp, e.UserId, e.Action, e.EntityType,
                e.EntityId, e.Details, e.IpAddress, e.UserAgent))
            .ToList();

        var pagedResult = new PagedResult<AuditLogEntryDto>(pagedEntries, request.PageNumber, request.PageSize, totalCount);
        return Task.FromResult(Result<PagedResult<AuditLogEntryDto>>.Success(pagedResult));
    }
}
