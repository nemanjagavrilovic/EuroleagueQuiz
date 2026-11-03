using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class DailyFivePlayerServiceTests
{
    [Fact]
    public async Task Shared_daily_clubs_have_independent_guest_deadlines_and_hide_future_rounds()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        Assert.Equal(24, first.Round.SecondsRemaining); Assert.Empty(first.Round.Players);
        Assert.Equal(0, first.Score); Assert.False(first.LeaderboardEligible);
        Assert.DoesNotContain("Club 2", System.Text.Json.JsonSerializer.Serialize(first));
        f.Clock.Now = f.Clock.Now.AddSeconds(7);
        var resumed = await f.Service.StartAsync(guest, default);
        Assert.Equal(first.DeadlineUtc, resumed.DeadlineUtc); Assert.Equal(17, resumed.Round.SecondsRemaining);
        var other = await f.Service.StartAsync(Guid.NewGuid(), default);
        Assert.Equal(first.ChallengeId, other.ChallengeId); Assert.Equal(first.Round.Team, other.Round.Team);
        Assert.Equal(24, other.Round.SecondsRemaining); Assert.Equal(1, f.Games.Created);
    }

    [Fact]
    public async Task Any_five_roster_players_earn_four_points_each_and_five_rounds_total_one_hundred()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        for (var round = 1; round <= 5; round++)
        {
            // Player 42 appears at every club; the sixth roster player must also be accepted.
            foreach (var player in new long[] { 42, round * 100 + 6, round * 100 + 5, round * 100 + 4, round * 100 + 3 })
                state = await f.Service.GuessAsync(guest, Request(state, player), default);
            Assert.True(state.Round.Completed); Assert.True(state.Round.RoundCorrect);
            Assert.Equal(round * 20, state.Score); Assert.Empty(state.RevealedPlayerIds);
            Assert.Equal(5, state.Round.Players.Count);
            if (round < 5) state = await f.Service.NextAsync(guest, Request(state), default);
        }
        Assert.True(state.Round.GameCompleted); Assert.Equal(100, state.Score); Assert.Equal(25, state.CorrectGuesses);
        Assert.Equal(100, (await f.Service.StartAsync(guest, default)).Score);
        Assert.Equal(100, (await f.Service.NextAsync(guest, Request(state), default)).Score);
        Assert.Equal(100, (await f.Service.GuessAsync(guest, Request(state, 507), default)).Score);
    }

    [Fact]
    public async Task Duplicate_and_wrong_guesses_do_not_add_points_or_extend_clock()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default); var deadline = state.DeadlineUtc;
        var request = Request(state, 106);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Service.GuessAsync(guest, request, default)));
        state = await f.Service.StartAsync(guest, default);
        Assert.Equal(4, state.Score); Assert.Single(state.Round.Players);
        state = await f.Service.GuessAsync(guest, Request(state, 106), default);
        Assert.False(state.Round.LastGuessCorrect); Assert.Equal(4, state.Score);
        f.Clock.Now = f.Clock.Now.AddSeconds(3);
        state = await f.Service.GuessAsync(guest, Request(state, 999), default);
        Assert.False(state.Round.LastGuessCorrect); Assert.Equal(4, state.Score);
        Assert.Equal(deadline, state.DeadlineUtc); Assert.Equal(21, state.Round.SecondsRemaining);
    }

    [Fact]
    public async Task Revealed_players_stay_red_after_refresh_and_preserve_earned_points()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        state = await f.Service.GuessAsync(guest, Request(state, 101), default);
        state = await f.Service.GuessAsync(guest, Request(state, 102), default);
        state = await f.Service.GuessAsync(guest, Request(state, skip: true), default);
        Assert.True(state.Round.Completed); Assert.False(state.Round.RoundCorrect);
        Assert.Equal(8, state.Score); Assert.Equal(3, state.RevealedPlayerIds.Count); Assert.Equal(5, state.Round.Players.Count);
        Assert.DoesNotContain(101, state.RevealedPlayerIds); Assert.DoesNotContain(102, state.RevealedPlayerIds);
        var restored = await f.Service.StartAsync(guest, default);
        Assert.Equal(state.RevealedPlayerIds, restored.RevealedPlayerIds); Assert.Null(restored.DeadlineUtc);
        f.Clock.Now = f.Clock.Now.AddMinutes(5);
        var next = await f.Service.NextAsync(guest, Request(restored), default);
        Assert.Equal(2, next.Round.RoundNumber); Assert.Equal(24, next.Round.SecondsRemaining);
        Assert.Empty(next.Round.Players); Assert.Empty(next.RevealedPlayerIds); Assert.Equal(8, next.Score);
    }

    [Fact]
    public async Task Exact_deadline_rejects_late_correct_guess_and_fills_only_five_slots()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        f.Clock.Now = f.Clock.Now.AddSeconds(24);
        state = await f.Service.GuessAsync(guest, Request(state, 101), default);
        Assert.True(state.Round.Completed); Assert.Equal(0, state.Score); Assert.Equal(0, state.Round.SecondsRemaining);
        Assert.Equal(5, state.Round.Players.Count); Assert.Equal(5, state.RevealedPlayerIds.Count);
    }

    [Fact]
    public async Task Returning_after_deadline_restores_timeout_without_resetting_round()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        state = await f.Service.GuessAsync(guest, Request(state, 101), default);
        f.Clock.Now = f.Clock.Now.AddSeconds(25);
        var restored = await f.Service.StartAsync(guest, default);
        Assert.True(restored.Round.Completed); Assert.Equal(1, restored.Round.RoundNumber);
        Assert.Equal(4, restored.Score); Assert.Equal(4, restored.RevealedPlayerIds.Count);
    }

    [Fact]
    public async Task Retried_next_never_skips_a_round_even_after_the_next_deadline_expires()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.NextAsync(guest, Request(state), default));
        state = await f.Service.GuessAsync(guest, Request(state, skip: true), default);
        var next = Request(state);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Service.NextAsync(guest, next, default)));
        state = await f.Service.StartAsync(guest, default); Assert.Equal(2, state.Round.RoundNumber);
        f.Clock.Now = f.Clock.Now.AddSeconds(25);
        state = await f.Service.NextAsync(guest, next, default);
        Assert.Equal(2, state.Round.RoundNumber); Assert.True(state.Round.Completed);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, next with { CommandId = Guid.NewGuid(), PlayerId = 201 }, default));
    }

    [Fact]
    public async Task Utc_reset_rejects_old_challenge_and_invalid_guesses_do_not_score()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var old = await f.Service.StartAsync(guest, default);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, Request(old), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, Request(old, 101) with { CommandId = Guid.Empty }, default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, Request(old, -1), default));
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var fresh = await f.Service.StartAsync(guest, default);
        Assert.NotEqual(old.ChallengeId, fresh.ChallengeId); Assert.Equal(0, fresh.Score); Assert.Equal(24, fresh.Round.SecondsRemaining);
        Assert.Equal(DateTimeKind.Utc, fresh.DeadlineUtc!.Value.Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, Request(old, 101), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(Guid.NewGuid(), Request(fresh, 101), default));
    }

    private static DailyFivePlayerRequest Request(DailyFivePlayerDto state, long? player = null, bool skip = false) => new(state.ChallengeId, state.Round.RoundNumber, Guid.NewGuid(), player, skip);
    private sealed class Fixture
    {
        public MemoryRepository Repository { get; } = new();
        public FakeGames Games { get; } = new();
        public FakeClock Clock { get; } = new();
        public DailyFivePlayerService Service => new(Repository, Games, Clock);
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
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
    private sealed class FakeGames : IFivePlayerGameService
    {
        public int Created { get; private set; }
        public Task<FivePlayerChallengeDto> CreateChallengeAsync(CancellationToken ct)
        {
            Created++;
            var clubs = Enumerable.Range(1, 5).Select(i => new FivePlayerClubChallengeDto(
                new(i, i.ToString(), "Club " + i, null, null, null, null, null),
                Enumerable.Range(1, 7).Select(p => Player(i * 100 + p)).Append(Player(42)).ToList())).ToList();
            return Task.FromResult(new FivePlayerChallengeDto(clubs));
        }
        private static PlayerDto Player(long id) => new(id, id.ToString(), "Player " + id, null, null, null, null, null, null);
        public Task<FivePlayerGameRoundDto> StartAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<FivePlayerGameRoundDto> GuessAsync(FivePlayerGameGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<FivePlayerGameRoundDto> NextAsync(Guid roundId, CancellationToken ct) => throw new NotSupportedException();
    }
}
