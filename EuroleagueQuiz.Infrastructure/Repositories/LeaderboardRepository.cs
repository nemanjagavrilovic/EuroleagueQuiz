using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class LeaderboardRepository(EuroleagueDbContext db) : ILeaderboardRepository
{
    public async Task<IReadOnlyList<LeaderboardAggregate>> GetScoresAsync(
        DateOnly? fromDate,
        DateOnly toDateExclusive,
        string? game,
        CancellationToken ct)
    {
        var partials = new List<PartialAggregate>();
        if (game is null or "grid")
        {
            var query = db.DailyGridAttempts.AsNoTracking()
                .Where(x => x.LeaderboardEligible && x.UserId != null && x.Date < toDateExclusive);
            if (fromDate is not null) query = query.Where(x => x.Date >= fromDate.Value);
            partials.AddRange(await query.GroupBy(x => x.UserId!.Value).Select(group => new PartialAggregate(
                group.Key,
                group.Sum(x => x.Score),
                group.Count(),
                group.Count(x => x.Completed),
                group.Count(x => x.Completed && x.Score == 100),
                group.Max(x => x.Completed ? x.CompletedAtUtc : null))).ToListAsync(ct));
        }

        if (game != "grid")
        {
            var query = db.DailyGameAttempts.AsNoTracking()
                .Where(x => x.LeaderboardEligible && x.UserId != null && x.Date < toDateExclusive);
            if (fromDate is not null) query = query.Where(x => x.Date >= fromDate.Value);
            if (game is not null) query = query.Where(x => x.Game == game);
            partials.AddRange(await query.GroupBy(x => x.UserId!.Value).Select(group => new PartialAggregate(
                group.Key,
                group.Sum(x => x.Score),
                group.Count(),
                group.Count(x => x.Completed),
                group.Count(x => x.Completed && x.Score == 100),
                group.Max(x => x.Completed ? x.CompletedAtUtc : null))).ToListAsync(ct));
        }

        var combined = partials.GroupBy(x => x.UserId).Select(group => new PartialAggregate(
            group.Key,
            group.Sum(x => x.Score),
            group.Sum(x => x.GamesPlayed),
            group.Sum(x => x.CompletedGames),
            group.Sum(x => x.PerfectGames),
            group.Max(x => x.LastCompletedAtUtc))).ToList();
        var userIds = combined.Select(x => x.UserId).ToList();
        var names = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, x => x.DisplayName, ct);

        return combined.Where(x => names.ContainsKey(x.UserId)).Select(x => new LeaderboardAggregate(
            x.UserId, names[x.UserId], x.Score, x.GamesPlayed, x.CompletedGames, x.PerfectGames, x.LastCompletedAtUtc)).ToList();
    }

    private sealed record PartialAggregate(
        Guid UserId,
        int Score,
        int GamesPlayed,
        int CompletedGames,
        int PerfectGames,
        DateTime? LastCompletedAtUtc);
}
