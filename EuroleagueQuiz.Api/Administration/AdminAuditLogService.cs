using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Api.Administration;

public sealed record AdminAuditLogRecord(
    long Id,
    DateTime OccurredAtUtc,
    Guid? ActorUserId,
    string ActorType,
    string ActorDisplayName,
    string Action,
    string Result,
    string? EntityType,
    string? EntityId,
    string? CorrelationId,
    string? DetailsJson);

public sealed record AdminAuditLogPageResponse(
    IReadOnlyList<AdminAuditLogRecord> Entries,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed class AdminAuditLogService(EuroleagueDbContext db)
{
    public async Task<AdminAuditLogPageResponse> GetPageAsync(
        string? search,
        string? action,
        string? result,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var query = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(entry => entry.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(result)) query = query.Where(entry => entry.Result == result.Trim());
        if (fromUtc.HasValue) query = query.Where(entry => entry.OccurredAtUtc >= fromUtc.Value.ToUniversalTime());
        if (toUtc.HasValue) query = query.Where(entry => entry.OccurredAtUtc <= toUtc.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            query = query.Where(entry =>
                EF.Functions.ILike(entry.Action, pattern)
                || EF.Functions.ILike(entry.EntityType ?? string.Empty, pattern)
                || EF.Functions.ILike(entry.EntityId ?? string.Empty, pattern)
                || EF.Functions.ILike(entry.CorrelationId ?? string.Empty, pattern));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Min(page, totalPages);
        var rows = await query
            .OrderByDescending(entry => entry.OccurredAtUtc)
            .ThenByDescending(entry => entry.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var actorIds = rows.Where(entry => entry.ActorUserId.HasValue)
            .Select(entry => entry.ActorUserId!.Value).Distinct().ToArray();
        var actors = await db.Users.AsNoTracking()
            .Where(user => actorIds.Contains(user.Id))
            .Select(user => new { user.Id, user.DisplayName })
            .ToDictionaryAsync(user => user.Id, user => user.DisplayName, cancellationToken);

        var entries = rows.Select(entry => new AdminAuditLogRecord(
            entry.Id,
            entry.OccurredAtUtc,
            entry.ActorUserId,
            entry.ActorType,
            entry.ActorUserId.HasValue ? actors.GetValueOrDefault(entry.ActorUserId.Value, "Deleted user") : entry.ActorType,
            entry.Action,
            entry.Result,
            entry.EntityType,
            entry.EntityId,
            entry.CorrelationId,
            entry.DetailsJson)).ToList();

        return new AdminAuditLogPageResponse(entries, page, pageSize, totalCount, totalPages);
    }
}
