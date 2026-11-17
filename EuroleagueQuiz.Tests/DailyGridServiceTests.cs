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
        var stored = await f.Repository.FindAttemptAsync(identity.UserId!.Value, f.Date, true, "Normal", null, default);
        Assert.Equal(identity.UserId, stored!.UserId);
        Assert.True(stored.LeaderboardEligible);
    }

    [Theory]
    [InlineData(GridDifficulty.Easy, 7, 10)]
    [InlineData(GridDifficulty.Normal, 14, 20)]
    [InlineData(GridDifficulty.Hard, 21, 30)]
    [InlineData(GridDifficulty.Legend, 28, 40)]
    public async Task Ranked_level_weights_partial_scores_and_preserves_points_on_reveal(
        GridDifficulty difficulty, int partialPoints, int maximumPoints)
    {
        var f = new Fixture();
        f.Repository.WeightedFromDate = f.Date.AddDays(1);
        var identity = new DailyAttemptIdentity(Guid.NewGuid(), Guid.NewGuid(), true);
        var round = await f.Service.StartAsync(identity, difficulty, default);
        Assert.Equal(2, round.ScoringVersion);
        Assert.Equal(0, round.LeaderboardPoints);
        Assert.Equal(maximumPoints, round.MaxLeaderboardPoints);
        for (var i = 1; i <= 7; i++)
            round = await f.Service.GuessAsync(identity, new(round.ChallengeId, i, Guid.NewGuid()), default);

        Assert.Equal(70, round.Score);
        Assert.Equal(partialPoints, round.LeaderboardPoints);
        var revealed = await f.Service.RevealAsync(identity, new(round.ChallengeId), default);
        Assert.Equal(70, revealed.Score);
        Assert.Equal(partialPoints, revealed.LeaderboardPoints);
        var restored = await f.Service.StartAsync(identity, difficulty, default);
        Assert.Equal(partialPoints, restored.LeaderboardPoints);
        Assert.True(restored.Completed);

        var secondUser = new DailyAttemptIdentity(Guid.NewGuid(), Guid.NewGuid(), true);
        var perfect = await f.Service.StartAsync(secondUser, difficulty, default);
        for (var i = 1; i <= 9; i++)
            perfect = await f.Service.GuessAsync(secondUser, new(perfect.ChallengeId, i, Guid.NewGuid()), default);
        Assert.Equal(100, perfect.Score);
        Assert.Equal(maximumPoints, perfect.LeaderboardPoints);
        Assert.True(perfect.Completed);
    }

    [Fact]
    public async Task Saved_legacy_scores_keep_their_rule_and_new_challenges_use_weights_immediately()
    {
        var f = new Fixture();
        f.Repository.WeightedFromDate = f.Date.AddDays(1);
        var identity = new DailyAttemptIdentity(Guid.NewGuid(), Guid.NewGuid(), true);
        var first = await f.Service.StartAsync(identity, GridDifficulty.Normal, default);
        var stored = await f.Repository.FindAttemptAsync(identity.UserId!.Value, f.Date, true, "Normal", null, default);
        stored!.Challenge.ScoringVersion = 1;
        for (var i = 1; i <= 7; i++)
            first = await f.Service.GuessAsync(identity, new(first.ChallengeId, i, Guid.NewGuid()), default);
        Assert.Equal(1, first.ScoringVersion);
        Assert.Equal(70, first.LeaderboardPoints);
        Assert.Equal(100, first.MaxLeaderboardPoints);
        var oldDate = f.Date;

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        var next = await f.Service.StartAsync(identity, GridDifficulty.Normal, default);
        Assert.Equal(2, next.ScoringVersion);
        Assert.Equal(20, next.MaxLeaderboardPoints);
        Assert.NotEqual(first.ChallengeId, next.ChallengeId);
        var old = await f.Repository.FindAttemptAsync(identity.UserId!.Value, oldDate, true, "Normal", null, default);
        Assert.Equal(70, old!.Score);
        Assert.Equal(1, old.Challenge.ScoringVersion);
    }

    [Fact]
    public async Task Scoring_notice_uses_the_announcement_date_and_expires_after_three_days()
    {
        var f = new Fixture();
        f.Repository.WeightedFromDate = f.Date;
        var upcoming = await f.Service.GetScoringNoticeAsync(default);
        Assert.NotNull(upcoming);
        Assert.Equal(DateTimeKind.Utc, upcoming!.StartsAtUtc.Kind);
        Assert.Equal(upcoming.StartsAtUtc.AddDays(3), upcoming.ExpiresAtUtc);

        f.Clock.Now = new DateTimeOffset(upcoming.StartsAtUtc);
        var active = await f.Service.GetScoringNoticeAsync(default);
        Assert.Equal(upcoming, active);
        f.Clock.Now = new DateTimeOffset(upcoming.ExpiresAtUtc).AddTicks(-1);
        Assert.NotNull(await f.Service.GetScoringNoticeAsync(default));
        f.Clock.Now = new DateTimeOffset(upcoming.ExpiresAtUtc);
        Assert.Null(await f.Service.GetScoringNoticeAsync(default));
    }

    [Fact]
    public async Task Signing_in_starts_a_separate_ranked_grid_at_the_same_difficulty()
    {
        var f = new Fixture();
        var guestId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var guestAttempt = await f.Service.StartAsync(guestId, GridDifficulty.Normal, default);

        guestAttempt = await f.Service.GuessAsync(guestId, new(guestAttempt.ChallengeId, 1, Guid.NewGuid()), default);
        var identity = new DailyAttemptIdentity(guestId, userId, true);
        var ranked = await f.Service.StartAsync(identity, GridDifficulty.Normal, default);

        Assert.NotEqual(guestAttempt.ChallengeId, ranked.ChallengeId);
        Assert.NotEqual(guestAttempt.Grid.RowTeams.Select(x => x.Id), ranked.Grid.RowTeams.Select(x => x.Id));
        Assert.True(ranked.LeaderboardEligible);
        Assert.Equal(0, ranked.Score);
        Assert.Equal(guestAttempt.Score, (await f.Service.StartAsync(guestId, GridDifficulty.Normal, default)).Score);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.RevealAsync(identity, new(guestAttempt.ChallengeId), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.RevealAsync(guestId, new(ranked.ChallengeId), default));
    }

    [Fact]
    public async Task Guests_get_one_independent_shared_daily_grid_per_difficulty()
    {
        var f = new Fixture();
        var guest = Guid.NewGuid();
        var other = Guid.NewGuid();
        var ids = new HashSet<long>();
        foreach (var level in Enum.GetValues<GridDifficulty>())
        {
            var first = await f.Service.StartAsync(guest, level, default);
            var shared = await f.Service.StartAsync(other, level, default);
            Assert.True(ids.Add(first.ChallengeId));
            Assert.Equal(first.ChallengeId, shared.ChallengeId);
            Assert.Equal(level.ToString(), first.Grid.Difficulty);
            Assert.False(first.LeaderboardEligible);
            Assert.Null(first.LeaderboardPoints);
            Assert.Null(first.MaxLeaderboardPoints);
            await f.Service.RevealAsync(guest, new(first.ChallengeId), default);
            Assert.True((await f.Service.StartAsync(guest, level, default)).Completed);
            Assert.False((await f.Service.StartAsync(other, level, default)).Completed);
        }
    }

    [Fact]
    public async Task Ranked_levels_save_separate_attempts_and_resume_across_devices()
    {
        var f = new Fixture();
        var identity = new DailyAttemptIdentity(Guid.NewGuid(), Guid.NewGuid(), true);
        var anotherDevice = identity with { GuestId = Guid.NewGuid() };
        var attempts = new Dictionary<GridDifficulty, DailyGridAttemptDto>();
        foreach (var level in Enum.GetValues<GridDifficulty>())
        {
            var ranked = await f.Service.StartAsync(identity, level, default);
            var guest = await f.Service.StartAsync(identity.GuestId, level, default);
            Assert.Equal(level.ToString(), ranked.Grid.Difficulty);
            Assert.True(ranked.LeaderboardEligible);
            Assert.NotEqual(guest.ChallengeId, ranked.ChallengeId);
            attempts.Add(level, ranked);
        }

        Assert.Equal(4, attempts.Values.Select(x => x.ChallengeId).Distinct().Count());
        await f.Service.RevealAsync(identity, new(attempts[GridDifficulty.Hard].ChallengeId), default);
        foreach (var (level, attempt) in attempts)
        {
            var refreshed = await f.Service.StartAsync(anotherDevice, level, default);
            Assert.Equal(attempt.ChallengeId, refreshed.ChallengeId);
            Assert.Equal(level == GridDifficulty.Hard, refreshed.Completed);
        }

        var differentUser = identity with { UserId = Guid.NewGuid() };
        await Assert.ThrowsAsync<ArgumentException>(() =>
            f.Service.RevealAsync(differentUser, new(attempts[GridDifficulty.Hard].ChallengeId), default));
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
        var restored = await new DailyGridService(f.Repository, f.Grids, f.Players, f.Clock).StartAsync(guest, GridDifficulty.Normal, default);
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
    public async Task Singleton_exception_fills_only_unique_cells_but_never_prefills_new_grid()
    {
        var f = new Fixture(); f.Players.Shared = true; f.Players.Singleton = true;
        var guest = Guid.NewGuid(); var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);
        Assert.Empty(round.Answers);
        round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 50, Guid.NewGuid()), default);
        var answer = Assert.Single(round.Answers);
        Assert.Equal((0, 0), (answer.Row, answer.Column));
        Assert.Equal(10, round.Score);
        Assert.Equal(1, round.GuessesUsed);
    }

    [Fact]
    public async Task Westermann_capture_fills_only_the_two_singletons_from_six_matching_cells()
    {
        var f = new Fixture();
        f.Players.WestermannCapture = true;
        var guest = Guid.NewGuid();
        var round = await f.Service.StartAsync(guest, GridDifficulty.Normal, default);

        round = await f.Service.GuessAsync(guest, new(round.ChallengeId, 313, Guid.NewGuid()), default);

        Assert.Equal(2, round.Answers.Count);
        Assert.Equal([(0, 0), (2, 0)], round.Answers.Select(x => (x.Row, x.Column)).Order().ToList());
        Assert.All(round.Answers, answer => Assert.Equal(313, answer.Player.Id));
        Assert.Empty(round.PendingCells);
        Assert.Null(round.PendingPlayer);
        Assert.Contains(313, round.UsedPlayerIds);
        Assert.Equal(20, round.Score);
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
        var stored = (await f.Repository.FindAttemptAsync(guest, f.Date, false, "Normal", null, default))!;
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
        public DateOnly Date => DateOnly.FromDateTime(Clock.Now.UtcDateTime);
        public DailyGridService Service => new(Repository, Grids, Players, Clock);
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 1, 23, 59, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class MemoryRepository : IDailyGridRepository
    {
        public DateOnly WeightedFromDate { get; set; } = new(2026, 11, 1);
        private readonly SemaphoreSlim gate = new(1);
        private readonly List<DailyGridAttempt> attempts = [];
        private readonly List<DailyGridChallenge> challenges = [];
        public Task<DateOnly> GetWeightedScoringStartDateAsync(CancellationToken ct) => Task.FromResult(WeightedFromDate);
        public async Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken ct)
        { await gate.WaitAsync(ct); try { return await action(); } finally { gate.Release(); } }
        public Task<DailyGridAttempt?> FindAttemptAsync(Guid guest, DateOnly date, bool ranked, string? difficulty, long? challengeId, CancellationToken ct) =>
            Task.FromResult(attempts.SingleOrDefault(x => (ranked ? x.UserId == guest : x.GuestId == guest) &&
                x.Date == date && x.Ranked == ranked &&
                (challengeId.HasValue ? x.ChallengeId == challengeId : x.Challenge.Difficulty == difficulty)));
        public async Task<DailyGridChallenge> GetOrCreateChallengeAsync(DateOnly date, string difficulty, bool ranked, Func<IReadOnlyList<DailyGridChallenge>, Task<string>> create, CancellationToken ct)
        {
            var today = challenges.Where(x => x.Date == date).ToArray();
            var challenge = today.SingleOrDefault(x => x.Difficulty == difficulty && x.Ranked == ranked);
            if (challenge is not null) return challenge;
            challenge = new()
            {
                Id = challenges.Count + 1, Date = date, Difficulty = difficulty, Ranked = ranked,
                DefinitionJson = await create(today), ScoringVersion = 2
            };
            challenges.Add(challenge); return challenge;
        }
        public void Add(DailyGridAttempt attempt) => attempts.Add(attempt);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private sealed class FakeGrids : IGridService
    {
        public Task<GridDefinitionDto> CreateGridAsync(CancellationToken ct, bool forceNew = false,
            GridDifficulty difficulty = GridDifficulty.Normal, IReadOnlyCollection<GridDefinitionDto>? excludedGrids = null)
        {
            var teams = Enumerable.Range(1, 6).Select(i => new TeamDto(i, i.ToString(), "Club " + i, null, null, null, null, null)).ToList();
            for (var second = 1; second < teams.Count - 1; second++)
            for (var third = second + 1; third < teams.Count; third++)
            {
                var rows = new[] { teams[0], teams[second], teams[third] };
                if (excludedGrids?.Any(grid => grid.RowTeams.SequenceEqual(rows)) == true) continue;
                return Task.FromResult(new GridDefinitionDto(rows, teams.Except(rows).ToList(), difficulty.ToString()));
            }
            throw new InvalidOperationException("No unused grid.");
        }
        public Task<GridRevealResponse> RevealAsync(GridRevealRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class FakePlayers : IPlayerService
    {
        public bool Shared { get; set; }
        public bool Singleton { get; set; }
        public bool WestermannCapture { get; set; }
        private static PlayerDto Player(long id) => new(id, id.ToString(), "Player " + id, null, null, null, null, null, null);
        public Task<IReadOnlyList<PlayerDto>> GetCommonPlayersAsync(long row, long column, CancellationToken ct)
        {
            var index = (row - 1) * 3 + column - 4;
            if (WestermannCapture)
            {
                int[] counts = [1, 3, 7, 2, 5, 8, 1, 8, 10];
                bool[] containsWestermann = [true, false, true, true, false, true, true, false, true];
                var captured = Enumerable.Range(0, counts[index])
                    .Select(offset => Player(2_000 + index * 20 + offset)).ToList();
                if (containsWestermann[index]) captured[0] = Player(313);
                return Task.FromResult<IReadOnlyList<PlayerDto>>(captured);
            }
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
