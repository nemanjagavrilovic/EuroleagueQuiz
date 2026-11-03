using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class DailyTeamHistoryServiceTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 75)]
    [InlineData(2, 50)]
    [InlineData(3, 25)]
    public async Task Correct_guess_awards_points_for_current_clue_count(int skips, int expected)
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        for (var i = 0; i < skips; i++) state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), null, true), default);
        Assert.Equal(expected, state.AvailablePoints);
        Assert.Equal(0, state.Score);
        state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 42), default);
        Assert.True(state.Round.Completed);
        Assert.True(state.Round.Correct);
        Assert.Equal(expected, state.Score);
        Assert.Equal(0, state.AvailablePoints);
        Assert.Equal(42, state.Round.CorrectPlayer!.Id);
        Assert.False(state.LeaderboardEligible);
        var restored = await f.Service.StartAsync(guest, default);
        Assert.Equal(expected, restored.Score);
        Assert.True(restored.Round.Completed);
    }

    [Fact]
    public async Task Daily_definition_and_clue_order_are_shared_but_progress_is_independent()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        Assert.Null(first.Round.CorrectPlayer);
        Assert.Single(first.Round.RevealedTeams);
        var wrong = await f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), 1), default);
        Assert.Equal(2, wrong.Round.RevealedTeamCount);
        Assert.Equal(75, wrong.AvailablePoints);
        Assert.Equal(0, wrong.Score);
        var other = await f.Service.StartAsync(Guid.NewGuid(), default);
        Assert.Equal(first.ChallengeId, other.ChallengeId);
        Assert.Equal(first.Round.RevealedTeams[0], other.Round.RevealedTeams[0]);
        Assert.Single(other.Round.RevealedTeams);
        Assert.NotEqual(first.Round.RoundId, other.Round.RoundId);
        Assert.Equal(1, f.Games.Created);
    }

    [Fact]
    public async Task Final_miss_or_reveal_awards_zero_and_cannot_be_replayed()
    {
        foreach (var skip in new[] { true, false })
        {
            var f = new Fixture(); var guest = Guid.NewGuid();
            var state = await f.Service.StartAsync(guest, default);
            for (var i = 0; i < 4; i++) state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), skip ? null : i + 1, skip), default);
            Assert.True(state.Round.Completed);
            Assert.False(state.Round.Correct);
            Assert.Equal(0, state.Score);
            Assert.NotNull(state.Round.CorrectPlayer);
            var after = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 42), default);
            Assert.False(after.Round.Correct);
            Assert.Equal(0, after.Score);
            Assert.Equal(4, after.Round.Attempts);
        }
    }

    [Fact]
    public async Task Duplicate_commands_and_invalid_players_do_not_reveal_extra_clues()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        var request = new DailyTeamHistoryGuessRequest(state.ChallengeId, Guid.NewGuid(), 1);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Service.GuessAsync(guest, request, default)));
        state = await f.Service.StartAsync(guest, default);
        Assert.Equal(1, state.Round.Attempts);
        Assert.Equal(2, state.Round.RevealedTeamCount);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), -1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.Empty, null, true), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), null), default));
        var after = await f.Service.StartAsync(guest, default);
        Assert.Equal(1, after.Round.Attempts);
        Assert.Equal(75, after.AvailablePoints);
    }

    [Fact]
    public async Task New_service_restores_progress_and_utc_midnight_starts_new_challenge()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        await f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), null, true), default);
        var afterRestart = await f.Service.StartAsync(guest, default);
        Assert.Equal(2, afterRestart.Round.RevealedTeamCount);
        Assert.Equal(first.Round.RoundId, afterRestart.Round.RoundId);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var next = await f.Service.StartAsync(guest, default);
        Assert.NotEqual(first.ChallengeId, next.ChallengeId);
        Assert.Equal(100, next.AvailablePoints);
        Assert.Equal(DateTimeKind.Utc, next.ResetsAtUtc.Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), 42), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(Guid.NewGuid(), new(next.ChallengeId, Guid.NewGuid(), 42), default));
    }

    private sealed class Fixture
    {
        public MemoryRepository Repository { get; } = new();
        public FakeGames Games { get; } = new();
        public FakePlayers Players { get; } = new();
        public FakeClock Clock { get; } = new();
        public DailyTeamHistoryService Service => new(Repository, Games, Players, Clock);
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 23, 59, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MemoryRepository : IDailyGameRepository
    {
        private readonly SemaphoreSlim gate = new(1);
        private readonly List<DailyGameAttempt> attempts = [];
        private readonly List<DailyGameChallenge> challenges = [];
        public async Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken ct)
        { await gate.WaitAsync(ct); try { return await action(); } finally { gate.Release(); } }
        public Task<DailyGameAttempt?> FindAttemptAsync(string game, Guid guest, DateOnly date, CancellationToken ct) =>
            Task.FromResult(attempts.SingleOrDefault(x => x.Game == game && x.GuestId == guest && x.Date == date));
        public async Task<DailyGameChallenge> GetOrCreateChallengeAsync(string game, DateOnly date, Func<Task<string>> create, CancellationToken ct)
        {
            var existing = challenges.SingleOrDefault(x => x.Game == game && x.Date == date);
            if (existing is not null) return existing;
            var challenge = new DailyGameChallenge { Id = challenges.Count + 1, Game = game, Date = date, DefinitionJson = await create() };
            challenges.Add(challenge); return challenge;
        }
        public void Add(DailyGameAttempt attempt) => attempts.Add(attempt);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private static PlayerDto Player(long id) => new(id, id.ToString(), "Player " + id, null, null, null, null, null, null);
    private sealed class FakeGames : ITeamHistoryGameService
    {
        public int Created { get; private set; }
        public Task<TeamHistoryChallengeDto> CreateChallengeAsync(CancellationToken ct)
        {
            Created++;
            return Task.FromResult(new TeamHistoryChallengeDto(Player(42), Enumerable.Range(1, 4)
                .Select(i => new TeamDto(i, i.ToString(), "Club " + i, null, null, null, null, null)).ToList()));
        }
        public Task<TeamHistoryRoundDto> StartAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<TeamHistoryRoundDto> GuessAsync(TeamHistoryGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class FakePlayers : IPlayerService
    {
        public Task<PlayerDto?> GetByIdAsync(long id, CancellationToken ct) => Task.FromResult(id > 0 ? Player(id) : null);
        public Task<IReadOnlyList<PlayerDto>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerDto>> SearchAsync(string term, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerDto>> GetCommonPlayersAsync(long first, long second, CancellationToken ct) => throw new NotSupportedException();
        public Task<GridPlayerMatchResponse?> GetGridPlayerMatchAsync(GridPlayerMatchRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
