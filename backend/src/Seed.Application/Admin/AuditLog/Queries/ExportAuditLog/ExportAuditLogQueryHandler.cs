using System.Text;
using MediatR;
using Seed.Application.Common;
using Seed.Application.Common.Interfaces;

namespace Seed.Application.Admin.AuditLog.Queries.ExportAuditLog;

public sealed class ExportAuditLogQueryHandler(
    IAuditLogReader auditLogReader)
    : IRequestHandler<ExportAuditLogQuery, Result<byte[]>>
{
    private const int MaxRows = 10_000;

    public Task<Result<byte[]>> Handle(
        ExportAuditLogQuery request, CancellationToken cancellationToken)
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

        // Limit rows
        var entries = query.Take(MaxRows).ToList();

        // Build CSV
        var sb = new StringBuilder();

        // UTF-8 BOM will be added when converting to bytes
        sb.AppendLine("Id,Timestamp,UserId,Action,EntityType,EntityId,Details,IpAddress,UserAgent");

        foreach (var e in entries)
        {
            sb.Append(e.Id).Append(',');
            sb.Append(e.Timestamp.ToString("o")).Append(',');
            sb.Append(e.UserId?.ToString() ?? string.Empty).Append(',');
            sb.Append(CsvEscape(e.Action)).Append(',');
            sb.Append(CsvEscape(e.EntityType)).Append(',');
            sb.Append(CsvEscape(e.EntityId)).Append(',');
            sb.Append(CsvEscape(e.Details)).Append(',');
            sb.Append(CsvEscape(e.IpAddress)).Append(',');
            sb.AppendLine(CsvEscape(e.UserAgent));
        }

        // Prepend UTF-8 BOM
        var bom = Encoding.UTF8.GetPreamble();
        var csvBytes = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + csvBytes.Length];
        bom.CopyTo(result, 0);
        csvBytes.CopyTo(result, bom.Length);

        return Task.FromResult(Result<byte[]>.Success(result));
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
