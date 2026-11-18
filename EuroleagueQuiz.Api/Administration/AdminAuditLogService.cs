using EuroleagueQuiz.Infrastructure.Persistence;
using EuroleagueQuiz.Domain.Auditing;
using EuroleagueQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Api.Administration;

public sealed record AdminAuditLogRecord(
    long Id,
    DateTime OccurredAtUtc,
    Guid? ActorUserId,
    Guid? ActorGuestId,
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

public sealed record GuestGameActivityResponse(string Game, string Name, int UniqueGuests, int GamesPlayed, int CompletedGames);
public sealed record GuestActivityResponse(
    int UniqueGuests, int GamesPlayed, int CompletedGames, double CompletionRate,
    DateTime? FromUtc, DateTime? ToUtc, DateTime? TrackingStartedAtUtc,
    IReadOnlyList<GuestGameActivityResponse> Games);

public sealed class AdminAuditLogService(EuroleagueDbContext db)
{
    public async Task<AdminAuditLogPageResponse> GetPageAsync(
        string? search,
        string? action,
        string? result,
        string? actorType,
        DateTime? fromUtc,
        DateTime? toUtc,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 10, 100);
        var query = InPeriod(db.AuditLogs.AsNoTracking(), fromUtc, toUtc);

        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(entry => entry.Action == action.Trim());
        if (!string.IsNullOrWhiteSpace(result)) query = query.Where(entry => entry.Result == result.Trim());
        if (!string.IsNullOrWhiteSpace(actorType)) query = query.Where(entry => entry.ActorType == actorType.Trim());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            Guid? searchedUserId = Guid.TryParse(search.Trim(), out var id) ? id : null;
            var matchingUserIds = db.Users.Where(user => EF.Functions.ILike(user.DisplayName, pattern)).Select(user => user.Id);
            query = query.Where(entry =>
                EF.Functions.ILike(entry.Action, pattern)
                || EF.Functions.ILike(entry.EntityType ?? string.Empty, pattern)
                || EF.Functions.ILike(entry.EntityId ?? string.Empty, pattern)
                || EF.Functions.ILike(entry.CorrelationId ?? string.Empty, pattern)
                || entry.ActorUserId.HasValue && (matchingUserIds.Contains(entry.ActorUserId.Value)
                    || searchedUserId.HasValue && entry.ActorUserId == searchedUserId));
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
            entry.ActorType == GuestGameAudit.ActorType ? GuestGameAudit.BrowserId(entry.EntityId) : null,
            entry.ActorType,
            ActorName(entry, actors),
            entry.Action,
            entry.Result,
            entry.EntityType,
            entry.EntityId,
            entry.CorrelationId,
            entry.DetailsJson)).ToList();

        return new AdminAuditLogPageResponse(entries, page, pageSize, totalCount, totalPages);
    }

    public async Task<GuestActivityResponse> GetGuestActivityAsync(
        DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var allActivity = GuestActivityQueries.Tracked(db.AuditLogs.AsNoTracking());
        var query = InPeriod(allActivity, fromUtc, toUtc);
        var uniqueGuests = await GuestActivityQueries.UniqueGuestIds(query).CountAsync(cancellationToken);
        var rows = await GuestActivityQueries.ByGame(query).ToListAsync(cancellationToken);
        var games = GuestGameAudit.Games.Select(game =>
        {
            var row = rows.SingleOrDefault(x => x.Key == game.StartedAction.Replace("GuestStarted", ""));
            return new GuestGameActivityResponse(game.Game, game.Name, row?.UniqueGuests ?? 0,
                row?.GamesPlayed ?? 0, row?.CompletedGames ?? 0);
        }).ToArray();
        var played = games.Sum(x => x.GamesPlayed);
        var completed = games.Sum(x => x.CompletedGames);
        var trackingStarted = await allActivity.Select(x => (DateTime?)x.OccurredAtUtc).MinAsync(cancellationToken);
        return new GuestActivityResponse(uniqueGuests, played, completed,
            played == 0 ? 0 : Math.Round(100d * completed / played, 1),
            fromUtc?.ToUniversalTime(), toUtc?.ToUniversalTime(), trackingStarted, games);
    }

    private static IQueryable<AuditLog> InPeriod(IQueryable<AuditLog> query, DateTime? fromUtc, DateTime? toUtc)
    {
        var from = fromUtc?.ToUniversalTime();
        var to = toUtc?.ToUniversalTime();
        if (from > to) throw new ArgumentException("The start date must be before the end date.");
        if (from.HasValue) query = query.Where(x => x.OccurredAtUtc >= from.Value);
        if (to.HasValue) query = query.Where(x => x.OccurredAtUtc <= to.Value);
        return query;
    }

    private static string ActorName(AuditLog entry, IReadOnlyDictionary<Guid, string> actors)
    {
        if (entry.ActorUserId.HasValue) return actors.GetValueOrDefault(entry.ActorUserId.Value, "Deleted user");
        var guestId = entry.ActorType == GuestGameAudit.ActorType ? GuestGameAudit.BrowserId(entry.EntityId) : null;
        return guestId.HasValue ? $"Guest · {guestId.Value.ToString("N")[..8]}" : entry.ActorType;
    }
}
