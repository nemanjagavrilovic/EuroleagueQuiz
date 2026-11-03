using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class DailyMissingClubServiceTests
{
    [Theory]
    [InlineData(4L, false, 100)]
    [InlineData(5L, false, 0)]
    [InlineData(null, true, 0)]
    public async Task Correct_wrong_and_revealed_answers_receive_the_expected_final_score(long? choice, bool skip, int score)
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        Assert.Equal(0, state.Score);
        Assert.Null(state.Round.CorrectTeam);
        Assert.Null(state.Round.Correct);
        Assert.False(state.LeaderboardEligible);
        state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), choice, skip), default);
        Assert.True(state.Round.Completed);
        Assert.Equal(score, state.Score);
        Assert.Equal(score == 100, state.Round.Correct);
        Assert.Equal(4, state.Round.CorrectTeam!.Id);
        Assert.Equal(skip ? null : choice, state.SelectedTeamId);
        var resumed = await f.Service.StartAsync(guest, default);
        Assert.Equal(score, resumed.Score);
        Assert.Equal(state.Round.RoundId, resumed.Round.RoundId);
        Assert.True(resumed.Round.Completed);
        var replay = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 4), default);
        Assert.Equal(score, replay.Score);
        Assert.Equal(state.SelectedTeamId, replay.SelectedTeamId);
    }

    [Fact]
    public async Task Daily_question_and_option_order_are_shared_but_attempts_are_independent()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        await f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), 5), default);
        var second = await f.Service.StartAsync(Guid.NewGuid(), default);
        Assert.Equal(first.ChallengeId, second.ChallengeId);
        Assert.Equal(first.Round.Options, second.Round.Options);
        Assert.Equal(first.Round.KnownTeams, second.Round.KnownTeams);
        Assert.False(second.Round.Completed);
        Assert.Null(second.Round.CorrectTeam);
        Assert.Equal(0, second.Score);
        Assert.Equal(1, f.Games.Created);
    }

    [Fact]
    public async Task Duplicate_and_competing_submissions_do_not_replace_the_first_result()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var initial = await f.Service.StartAsync(guest, default);
        var request = new DailyMissingClubGuessRequest(initial.ChallengeId, Guid.NewGuid(), 5);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Service.GuessAsync(guest, request, default)));
        var result = await f.Service.GuessAsync(guest, new(initial.ChallengeId, Guid.NewGuid(), 4), default);
        Assert.Equal(0, result.Score);
        Assert.Equal(5, result.SelectedTeamId);
        Assert.False(result.Round.Correct);
    }

    [Fact]
    public async Task Invalid_choices_and_commands_do_not_consume_the_attempt()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 999), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.Empty, 4), default));
        var restored = await f.Service.StartAsync(guest, default);
        Assert.False(restored.Round.Completed);
        Assert.Null(restored.Round.CorrectTeam);
        Assert.Equal(100, (await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 4), default)).Score);
    }

    [Fact]
    public async Task Utc_reset_creates_new_attempt_and_rejects_stale_requests()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var old = await f.Service.StartAsync(guest, default);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var next = await f.Service.StartAsync(guest, default);
        Assert.NotEqual(old.ChallengeId, next.ChallengeId);
        Assert.Equal(DateTimeKind.Utc, next.ResetsAtUtc.Kind);
        Assert.False(next.Round.Completed);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(old.ChallengeId, Guid.NewGuid(), 4), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(Guid.NewGuid(), new(next.ChallengeId, Guid.NewGuid(), 4), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.StartAsync(Guid.Empty, default));
    }

    [Fact]
    public async Task Ambiguous_or_invalid_option_layout_is_rejected_before_creating_an_attempt()
    {
        var f = new Fixture(); f.Games.Invalid = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.Service.StartAsync(Guid.NewGuid(), default));
    }

    private sealed class Fixture
    {
        public MemoryRepository Repository { get; } = new();
        public FakeGames Games { get; } = new();
        public FakeClock Clock { get; } = new();
        public DailyMissingClubService Service => new(Repository, Games, Clock);
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
    private sealed class FakeGames : ICareerGamesService
    {
        public Task<MysteryPlayerChallengeDto> CreateMysteryPlayerChallengeAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderChallengeDto> CreateCareerOrderChallengeAsync(CancellationToken ct) => throw new NotSupportedException();
        public int Created { get; private set; }
        public bool Invalid { get; set; }
        public Task<MissingClubChallengeDto> CreateMissingClubChallengeAsync(CancellationToken ct)
        {
            Created++;
            var teams = Enumerable.Range(1, 7).Select(i => new TeamDto(i, i.ToString(), "Club " + i, null, null, null, null, null)).ToList();
            var player = new PlayerDto(42, "42", "Player 42", null, null, null, null, null, null);
            return Task.FromResult(new MissingClubChallengeDto(player, teams.Take(3).ToList(),
                Invalid ? teams.Take(4).ToList() : teams.Skip(3).ToList(), teams[3]));
        }
        public Task<MissingClubRoundDto> StartMissingClubAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MissingClubRoundDto> GuessMissingClubAsync(MissingClubGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderRoundDto> StartCareerOrderAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderRoundDto> GuessCareerOrderAsync(CareerOrderGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<MysteryPlayerRoundDto> StartMysteryPlayerAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MysteryPlayerRoundDto> GuessMysteryPlayerAsync(MysteryPlayerGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
