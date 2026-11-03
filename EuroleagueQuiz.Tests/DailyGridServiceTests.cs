using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class DailyGridServiceTests
{
    [Fact]
    public async Task New_authenticated_ranked_attempt_is_leaderboard_eligible()
    {
        var f = new Fixture();
        var identity = new DailyAttemptIdentity(Guid.NewGuid(), Guid.NewGuid(), true);

        var attempt = await f.Service.StartAsync(identity, GridDifficulty.Normal, default);

        Assert.True(attempt.LeaderboardEligible);
        var stored = await f.Repository.FindAttemptAsync(identity.UserId!.Value, DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), default);
        Assert.Equal(identity.UserId, stored!.UserId);
        Assert.True(stored.LeaderboardEligible);
    }

    [Fact]
    public async Task Guest_attempt_claimed_after_sign_in_remains_unranked()
    {
        var f = new Fixture();
        var guestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var guestAttempt = await f.Service.StartAsync(guestId, GridDifficulty.Normal, default);

        var claimed = await f.Service.StartAsync(new DailyAttemptIdentity(guestId, userId, true), GridDifficulty.Legend, default);

        Assert.Equal(guestAttempt.ChallengeId, claimed.ChallengeId);
        Assert.False(claimed.LeaderboardEligible);
        var stored = await f.Repository.FindAttemptAsync(userId, DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), default);
        Assert.Equal(userId, stored!.UserId);
        Assert.False(stored.LeaderboardEligible);
    }

    [Fact]
    public async Task Daily_grid_is_shared_and_first_difficulty_is_locked_across_refreshes()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, GridDifficulty.Easy, default);
        var refreshed = await f.Service.StartAsync(guest, GridDifficulty.Legend, default);
        var other = await f.Service.StartAsync(Guid.NewGuid(), GridDifficulty.Easy, default);
        Assert.Equal(first.ChallengeId, refreshed.ChallengeId);
        Assert.Equal(first.ChallengeId, other.ChallengeId);
        Assert.Equal("Easy", refreshed.Grid.Difficulty);
        Assert.False(first.LeaderboardEligible);
    }

    [Fact]
    public async Task Correct_cells_award_ten_points_and_full_grid_awards_one_hundred()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        for (var i = 0; i < 9; i++)
        {
            round = await f.Service.GuessAsync(guest, new(round.ChallengeId, i + 1, Guid.NewGuid()), default);
            Assert.Equal(i == 8 ? 100 : (i + 1) * 10, round.Score);
        }
        Assert.True(round.Completed);
        Assert.False(round.Revealed);
        Assert.Equal(9, round.GuessesUsed);
    }

    [Fact]
    public async Task Many_wrong_answers_do_not_end_attempt_and_correct_guesses_still_score()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        for (var i = 0; i < 30; i++) round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 100 + i, Guid.NewGuid()), default);
        Assert.False(round.Completed);
        Assert.Equal(0, round.Score);
        Assert.Empty(round.Answers);
        var again = await f.Service.GuessAsync(guest, new(round.ChallengeId, 1, Guid.NewGuid()), default);
        Assert.Equal(31, again.GuessesUsed);
        Assert.Equal(10, again.Score);
        Assert.False(again.Completed);
    }

    [Fact]
    public async Task Reveal_preserves_earned_points_and_a_new_service_restores_the_attempt()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        await f.Service.GuessAsync(guest, new(round.ChallengeId, 1, Guid.NewGuid()), default);
        var revealed = await f.Service.RevealAsync(guest, new(round.ChallengeId), default);
        var restored = await new DailyGridService(f.Repository, f.Grids, f.Players, f.Clock).StartAsync(guest, GridDifficulty.Hard, default);
        Assert.True(restored.Completed);
        Assert.True(restored.Revealed);
        Assert.Equal(10, restored.Score);
        Assert.Single(revealed.Answers.Where(x => !x.Revealed));
        Assert.Equal(8, revealed.Answers.Count(x => x.Revealed));
        Assert.Equal(9, revealed.Answers.Select(x => x.Player.Id).Distinct().Count());
    }

    [Fact]
    public async Task Pending_choice_survives_refresh_and_placement_costs_no_extra_guess()
    {
        var f = new Fixture(); f.Players.Shared = true;
        var guest = Guid.NewGuid(); var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 50, Guid.NewGuid()), default);
        Assert.Equal(2, round.PendingCells.Count);
        Assert.Empty(round.Answers);
        round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        Assert.Equal(50, round.PendingPlayer!.Id);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.PlaceAsync(guest, new(round.ChallengeId, 2, 2), default));
        round = await f.Service.PlaceAsync(guest, new(round.ChallengeId, 0, 1), default);
        Assert.Equal(10, round.Score);
        Assert.Equal(1, round.GuessesUsed);
        Assert.Empty(round.PendingCells);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(round.ChallengeId, 50, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Singleton_exception_fills_both_cells_but_never_prefills_new_grid()
    {
        var f = new Fixture(); f.Players.Shared = true; f.Players.Singleton = true;
        var guest = Guid.NewGuid(); var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        Assert.Empty(round.Answers);
        round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 50, Guid.NewGuid()), default);
        Assert.Equal(2, round.Answers.Count);
        Assert.Equal(20, round.Score);
        Assert.Equal(1, round.GuessesUsed);
    }

    [Fact]
    public async Task Ninth_guess_placement_keeps_incomplete_grid_open()
    {
        var f = new Fixture(); f.Players.Shared = true;
        var guest = Guid.NewGuid(); var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        for (var i = 0; i < 8; i++) round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 100 + i, Guid.NewGuid()), default);
        round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 50, Guid.NewGuid()), default);
        Assert.Equal(9, round.GuessesUsed);
        Assert.False(round.Completed);
        round = await f.Service.PlaceAsync(guest, new(round.ChallengeId, 0, 0), default);
        Assert.False(round.Completed);
        Assert.Equal(10, round.Score);
        Assert.Single(round.Answers);
    }

    [Fact]
    public async Task Old_guess_limit_completion_resumes_without_revealed_answers()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        await f.Service.GuessAsync(guest, new(round.ChallengeId, 1, Guid.NewGuid()), default);
        var stored = (await f.Repository.FindAttemptAsync(guest, DateOnly.FromDateTime(f.Clock.Now.UtcDateTime), default))!;
        var state = System.Text.Json.JsonSerializer.Deserialize<DailyGridService.AttemptState>(stored.StateJson)!;
        state.Completed = true; state.GuessesUsed = 9;
        state.Answers.Add(new(0, 1, new(2, "2", "Player 2", null, null, null, null, null, null), true));
        stored.StateJson = System.Text.Json.JsonSerializer.Serialize(state);
        var restored = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        Assert.False(restored.Completed);
        Assert.Equal(10, restored.Score);
        Assert.Single(restored.Answers);
        Assert.False(restored.Answers[0].Revealed);
    }

    [Fact]
    public async Task Duplicate_command_and_concurrent_retries_do_not_consume_extra_guesses()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        var request = new DailyGridGuessRequest(round.ChallengeId, 1, Guid.NewGuid());
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => f.Service.GuessAsync(guest, request, default)));
        round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        Assert.Equal(1, round.GuessesUsed);
        Assert.Equal(10, round.Score);
    }

    [Fact]
    public async Task Utc_midnight_creates_new_attempt_and_rejects_stale_challenge_commands()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var old = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var next = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        Assert.NotEqual(old.ChallengeId, next.ChallengeId);
        Assert.Equal(0, next.Score);
        Assert.Equal(DateTimeKind.Utc, next.ResetsAtUtc.Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(old.ChallengeId, 1, Guid.NewGuid()), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.RevealAsync(guest, new(old.ChallengeId), default));
    }

    private sealed class Fixture
    {
        public MemoryRepository Repository { get; } = new();
        public FakeGrids Grids { get; } = new();
        public FakePlayers Players { get; } = new();
        public FakeClock Clock { get; } = new();
        public DailyGridService Service => new(Repository, Grids, Players, Clock);
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 23, 59, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MemoryRepository : IDailyGridRepository
    {
        private readonly SemaphoreSlim gate = new(1);
        private readonly List<DailyGridAttempt> attempts = [];
        private readonly List<DailyGridChallenge> challenges = [];
        public async Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken ct)
        { await gate.WaitAsync(ct); try { return await action(); } finally { gate.Release(); } }
        public Task<DailyGridAttempt?> FindAttemptAsync(Guid guest, DateOnly date, CancellationToken ct) => Task.FromResult(attempts.SingleOrDefault(x => x.GuestId == guest && x.Date == date));
        public async Task<DailyGridChallenge> GetOrCreateChallengeAsync(DateOnly date, string difficulty, Func<Task<string>> create, CancellationToken ct)
        {
            var challenge = challenges.SingleOrDefault(x => x.Date == date && x.Difficulty == difficulty);
            if (challenge is not null) return challenge;
            challenge = new() { Id = challenges.Count + 1, Date = date, Difficulty = difficulty, DefinitionJson = await create() };
            challenges.Add(challenge); return challenge;
        }
        public void Add(DailyGridAttempt attempt) => attempts.Add(attempt);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeGrids : IGridService
    {
        public Task<GridDefinitionDto> CreateGridAsync(CancellationToken ct, bool forceNew = false, GridDifficulty difficulty = GridDifficulty.Normal)
        {
            var teams = Enumerable.Range(1, 6).Select(i => new TeamDto(i, i.ToString(), "Club " + i, null, null, null, null, null)).ToList();
            return Task.FromResult(new GridDefinitionDto(teams.Take(3).ToList(), teams.Skip(3).ToList(), difficulty.ToString()));
        }
        public Task<GridRevealResponse> RevealAsync(GridRevealRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class FakePlayers : IPlayerService
    {
        public bool Shared { get; set; }
        public bool Singleton { get; set; }
        private static PlayerDto Player(long id) => new(id, id.ToString(), "Player " + id, null, null, null, null, null, null);
        public Task<IReadOnlyList<PlayerDto>> GetCommonPlayersAsync(long row, long column, CancellationToken ct)
        {
            var index = (row - 1) * 3 + column - 4;
            var list = new List<PlayerDto> { Player(index + 1) };
            if (Shared && index < 2) { if (Singleton && index == 0) list.Clear(); list.Add(Player(50)); }
            return Task.FromResult<IReadOnlyList<PlayerDto>>(list);
        }
        public Task<PlayerDto?> GetByIdAsync(long id, CancellationToken ct) => Task.FromResult<PlayerDto?>(Player(id));
        public Task<IReadOnlyList<PlayerDto>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerDto>> SearchAsync(string term, CancellationToken ct) => throw new NotSupportedException();
        public Task<GridPlayerMatchResponse?> GetGridPlayerMatchAsync(GridPlayerMatchRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
