using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;

namespace EuroleagueQuiz.Application.Services;

public sealed class LeaderboardService(ILeaderboardRepository repository, TimeProvider clock) : ILeaderboardService
{
    public static readonly string[] Games = ["all", "grid", "team-history", "missing-club", "career-order", "mystery-player", "five-player"];
    public static readonly string[] Periods = ["today", "week", "month", "all-time"];

    public async Task<LeaderboardDto> GetAsync(string period, string game, int page, int pageSize, Guid? currentUserId,
        CancellationToken ct)
    {
        period = period.Trim().ToLowerInvariant();
        game = game.Trim().ToLowerInvariant();
        if (!Periods.Contains(period)) throw new ArgumentException("Unknown leaderboard period.");
        if (!Games.Contains(game)) throw new ArgumentException("Unknown leaderboard game.");
        if (page < 1) throw new ArgumentException("Page must be greater than zero.");
        if (pageSize is < 5 or > 50) throw new ArgumentException("Page size must be between 5 and 50.");

        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        var fromDate = period switch
        {
            "today" => today,
            "week" => today.AddDays(-(((int)today.DayOfWeek + 6) % 7)),
            "month" => new DateOnly(today.Year, today.Month, 1),
            _ => (DateOnly?)null
        };
        var rows = await repository.GetScoresAsync(fromDate, today.AddDays(1), game == "all" ? null : game, ct);
        var ordered = rows.OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.CompletedGames)
            .ThenByDescending(x => x.PerfectGames)
            .ThenBy(x => x.LastCompletedAtUtc ?? DateTime.MaxValue)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var entries = new List<LeaderboardEntryDto>(ordered.Count);
        LeaderboardEntryDto? current = null;
        var rank = 0;
        LeaderboardAggregate? previous = null;
        for (var index = 0; index < ordered.Count; index++)
        {
            var row = ordered[index];
            if (previous is null || !Tied(previous, row)) rank = index + 1;
            var entry = new LeaderboardEntryDto(rank, row.DisplayName, row.Score, row.GamesPlayed, row.CompletedGames,
                row.PerfectGames, currentUserId == row.UserId);
            entries.Add(entry);
            if (entry.IsCurrentUser) current = entry;
            previous = row;
        }

        var days = fromDate is null ? 0 : today.DayNumber - fromDate.Value.DayNumber + 1;
        int? maximum = fromDate is null ? null : days * (game == "all" ? 600 : 100);
        var totalEntries = entries.Count;
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalEntries / (double)pageSize));
        var effectivePage = Math.Min(page, totalPages);
        var pagedEntries = entries.Skip((effectivePage - 1) * pageSize).Take(pageSize).ToList();
        return new LeaderboardDto(period, game, fromDate, today, maximum, pagedEntries, current, effectivePage,
            pageSize, totalEntries, totalPages);
    }

    private static bool Tied(LeaderboardAggregate left, LeaderboardAggregate right) =>
        left.Score == right.Score && left.CompletedGames == right.CompletedGames && left.PerfectGames == right.PerfectGames &&
        left.LastCompletedAtUtc == right.LastCompletedAtUtc;
}
