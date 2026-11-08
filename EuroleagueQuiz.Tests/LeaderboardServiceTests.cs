using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;

namespace EuroleagueQuiz.Tests;

public sealed class LeaderboardServiceTests
{
    [Fact]
    public async Task Today_all_games_has_six_hundred_point_maximum_and_ranks_by_score()
    {
        var repository = new FakeRepository([
            Row("Second", 410, 6, 6, 1, new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc)),
            Row("First", 520, 6, 5, 2, new DateTime(2026, 11, 4, 13, 0, 0, DateTimeKind.Utc))
        ]);
        var service = new LeaderboardService(repository, new FakeClock());

        var result = await service.GetAsync("today", "all", 1, 20, null, default);

        Assert.Equal(600, result.MaximumScore);
        Assert.Equal(new DateOnly(2026, 11, 4), repository.FromDate);
        Assert.Equal(new DateOnly(2026, 11, 5), repository.ToDateExclusive);
        Assert.Equal(["First", "Second"], result.Entries.Select(x => x.DisplayName));
        Assert.Equal([1, 2], result.Entries.Select(x => x.Rank));
    }

    [Fact]
    public async Task Week_is_monday_to_today_and_single_game_maximum_is_period_to_date()
    {
        var repository = new FakeRepository([]);
        var service = new LeaderboardService(repository, new FakeClock());

        var result = await service.GetAsync("week", "grid", 1, 20, null, default);

        Assert.Equal(new DateOnly(2026, 11, 2), result.FromDate);
        Assert.Equal(300, result.MaximumScore);
        Assert.Equal("grid", repository.Game);
    }

    [Fact]
    public async Task Current_user_is_returned_even_when_outside_current_page()
    {
        var currentId = Guid.NewGuid();
        var rows = Enumerable.Range(1, 101).Select(index => new LeaderboardAggregate(
            index == 101 ? currentId : Guid.NewGuid(), $"Player {index}", 1000 - index, 6, 6, 0, null)).ToList();
        var service = new LeaderboardService(new FakeRepository(rows), new FakeClock());

        var result = await service.GetAsync("all-time", "all", 1, 20, currentId, default);

        Assert.Equal(20, result.Entries.Count);
        Assert.Equal(101, result.TotalEntries);
        Assert.Equal(6, result.TotalPages);
        Assert.NotNull(result.CurrentUser);
        Assert.Equal(101, result.CurrentUser!.Rank);
        Assert.True(result.CurrentUser.IsCurrentUser);
        Assert.Null(result.MaximumScore);
    }

    [Fact]
    public async Task Requested_page_returns_only_that_page()
    {
        var rows = Enumerable.Range(1, 45).Select(index =>
            Row($"Player {index}", 1000 - index, 6, 6, 0, null)).ToList();
        var service = new LeaderboardService(new FakeRepository(rows), new FakeClock());

        var result = await service.GetAsync("today", "all", 2, 20, null, default);

        Assert.Equal(2, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(45, result.TotalEntries);
        Assert.Equal(3, result.TotalPages);
        Assert.Equal(21, result.Entries[0].Rank);
        Assert.Equal(40, result.Entries[^1].Rank);
    }

    private static LeaderboardAggregate Row(string name, int score, int played, int completed, int perfect, DateTime? completedAt) =>
        new(Guid.NewGuid(), name, score, played, completed, perfect, completedAt);

    private sealed class FakeRepository(IReadOnlyList<LeaderboardAggregate> rows) : ILeaderboardRepository
    {
        public DateOnly? FromDate { get; private set; }
        public DateOnly ToDateExclusive { get; private set; }
        public string? Game { get; private set; }
        public Task<IReadOnlyList<LeaderboardAggregate>> GetScoresAsync(DateOnly? fromDate, DateOnly toDateExclusive, string? game, CancellationToken cancellationToken)
        {
            FromDate = fromDate; ToDateExclusive = toDateExclusive; Game = game;
            return Task.FromResult(rows);
        }
    }

    private sealed class FakeClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 4, 14, 0, 0, TimeSpan.Zero);
    }
}
