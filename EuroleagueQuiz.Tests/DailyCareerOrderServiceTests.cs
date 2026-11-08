using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class DailyCareerOrderServiceTests
{
    [Theory]
    [InlineData(1, 2, 3, 4, 3, 100)]
    [InlineData(2, 1, 3, 4, 1, 33)]
    [InlineData(2, 3, 1, 4, 1, 33)]
    [InlineData(3, 2, 1, 4, 0, 0)]
    [InlineData(3, 4, 1, 2, 2, 67)]
    [InlineData(3, 4, 2, 1, 1, 33)]
    [InlineData(4, 3, 2, 1, 0, 0)]
    [InlineData(4, 1, 2, 3, 2, 67)]
    [InlineData(2, 1, 4, 3, 0, 0)]
    public async Task Scores_only_consecutive_transfers_in_the_correct_direction(int a, int b, int c, int d, int transfers, int score)
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        Assert.Null(state.CorrectTransfers); Assert.Null(state.Round.CorrectOrder); Assert.Equal(0, state.Score);
        Assert.Equal(3, state.TotalTransfers); Assert.Equal(2, state.ScoringVersion);
        state = await f.Service.SubmitAsync(guest, new(state.ChallengeId, Guid.NewGuid(), new long[] { a, b, c, d }), default);
        Assert.True(state.Round.Completed); Assert.Equal(transfers, state.CorrectTransfers); Assert.Equal(score, state.Score);
        Assert.Equal(score == 100, state.Round.Correct); Assert.False(state.LeaderboardEligible);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, state.Round.CorrectOrder!.Select(x => x.Id));
        Assert.Equal(new long[] { a, b, c, d }, state.Round.Clubs.Select(x => x.Id));
    }

    [Fact]
    public async Task Shared_daily_question_has_independent_saved_drafts_without_score_hints()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        await f.Service.SaveDraftAsync(guest, new(first.ChallengeId, Guid.NewGuid(), new long[] { 2, 1, 3, 4 }), default);
        var restored = await f.Service.StartAsync(guest, default);
        Assert.Equal(new long[] { 2, 1, 3, 4 }, restored.Round.Clubs.Select(x => x.Id));
        Assert.Equal(first.Round.RoundId, restored.Round.RoundId);
        Assert.False(restored.Round.Completed); Assert.Equal(0, restored.Score);
        Assert.Null(restored.CorrectTransfers); Assert.Null(restored.Round.CorrectOrder); Assert.Null(restored.Round.Correct);
        var other = await f.Service.StartAsync(Guid.NewGuid(), default);
        Assert.Equal(first.ChallengeId, other.ChallengeId);
        Assert.Equal(first.Round.Clubs, other.Round.Clubs);
        Assert.Equal(1, f.Games.Created);
    }

    [Fact]
    public async Task Retried_old_draft_cannot_replace_a_newer_draft_and_late_drafts_cannot_change_score()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        var oldDraft = new DailyCareerOrderRequest(state.ChallengeId, Guid.NewGuid(), new long[] { 4, 3, 2, 1 });
        await f.Service.SaveDraftAsync(guest, oldDraft, default);
        await f.Service.SaveDraftAsync(guest, new(state.ChallengeId, Guid.NewGuid(), new long[] { 2, 1, 3, 4 }), default);
        var retried = await f.Service.SaveDraftAsync(guest, oldDraft, default);
        Assert.Equal(new long[] { 2, 1, 3, 4 }, retried.Round.Clubs.Select(x => x.Id));
        var submit = new DailyCareerOrderRequest(state.ChallengeId, Guid.NewGuid(), new long[] { 2, 1, 3, 4 });
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Service.SubmitAsync(guest, submit, default)));
        var late = await f.Service.SaveDraftAsync(guest, new(state.ChallengeId, Guid.NewGuid(), new long[] { 1, 2, 3, 4 }), default);
        Assert.True(late.Round.Completed); Assert.Equal(33, late.Score);
        var replay = await f.Service.SubmitAsync(guest, new(state.ChallengeId, Guid.NewGuid(), new long[] { 1, 2, 3, 4 }), default);
        Assert.Equal(33, replay.Score);
        Assert.Equal(33, (await f.Service.StartAsync(guest, default)).Score);
    }

    [Fact]
    public async Task Saved_legacy_challenge_keeps_original_scoring_until_utc_reset()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        f.Games.HistoryVersion = 1;
        var first = await f.Service.StartAsync(guest, default);
        // Simulate definitions saved before the version field existed.
        f.Repository.OmitHistoryVersion();
        f.Games.HistoryVersion = CareerOrderRules.CurrentVersion;
        var result = await f.Service.SubmitAsync(guest, new(first.ChallengeId, Guid.NewGuid(), [2, 1, 3, 4]), default);
        Assert.Equal(83, result.Score); Assert.Equal(1, result.ScoringVersion); Assert.Null(result.CorrectTransfers);
        var other = await f.Service.StartAsync(Guid.NewGuid(), default);
        Assert.Equal(first.ChallengeId, other.ChallengeId); Assert.Equal(1, other.ScoringVersion);
        Assert.Equal(83, (await f.Service.StartAsync(guest, default)).Score);

        f.Clock.Now = f.Clock.Now.AddDays(1);
        var next = await f.Service.StartAsync(guest, default);
        var nextResult = await f.Service.SubmitAsync(guest, new(next.ChallengeId, Guid.NewGuid(), [2, 1, 3, 4]), default);
        Assert.Equal(33, nextResult.Score); Assert.Equal(2, nextResult.ScoringVersion); Assert.Equal(1, nextResult.CorrectTransfers);

        f.Clock.Now = f.Clock.Now.AddDays(-1);
        Assert.Equal(83, (await f.Service.StartAsync(guest, default)).Score);
    }

    [Fact]
    public async Task Invalid_permutations_and_commands_do_not_spend_daily_attempt()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        foreach (var ids in new IReadOnlyList<long>?[] { null, [], [1, 2, 3], [1, 1, 3, 4], [1, 2, 3, 999], [1, 2, 3, 4, 5] })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SubmitAsync(guest, new(state.ChallengeId, Guid.NewGuid(), ids), default));
            await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveDraftAsync(guest, new(state.ChallengeId, Guid.NewGuid(), ids), default));
        }
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SubmitAsync(guest, new(state.ChallengeId, Guid.Empty, [1, 2, 3, 4]), default));
        Assert.False((await f.Service.StartAsync(guest, default)).Round.Completed);
        Assert.Equal(100, (await f.Service.SubmitAsync(guest, new(state.ChallengeId, Guid.NewGuid(), [1, 2, 3, 4]), default)).Score);
    }

    [Fact]
    public async Task Utc_reset_creates_new_challenge_and_rejects_stale_drafts_and_submissions()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var old = await f.Service.StartAsync(guest, default);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var next = await f.Service.StartAsync(guest, default);
        Assert.NotEqual(old.ChallengeId, next.ChallengeId);
        Assert.Equal(DateTimeKind.Utc, next.ResetsAtUtc.Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SubmitAsync(guest, new(old.ChallengeId, Guid.NewGuid(), [1, 2, 3, 4]), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SaveDraftAsync(guest, new(old.ChallengeId, Guid.NewGuid(), [1, 2, 3, 4]), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.SubmitAsync(Guid.NewGuid(), new(next.ChallengeId, Guid.NewGuid(), [1, 2, 3, 4]), default));
    }

    private sealed class Fixture
    {
        public MemoryRepository Repository { get; } = new();
        public FakeGames Games { get; } = new();
        public FakeClock Clock { get; } = new();
        public DailyCareerOrderService Service => new(Repository, Games, Clock);
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
        public void OmitHistoryVersion()
        {
            foreach (var challenge in challenges)
                challenge.DefinitionJson = challenge.DefinitionJson.Replace(",\"HistoryVersion\":1", "");
        }
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
        public int Created { get; private set; }
        public int HistoryVersion { get; set; } = CareerOrderRules.CurrentVersion;
        public Task<CareerOrderChallengeDto> CreateCareerOrderChallengeAsync(CancellationToken ct)
        {
            Created++;
            var correct = Enumerable.Range(1, 4).Select(i => new CareerOrderClubDto(i, "Club " + i, null)).ToList();
            var player = new PlayerDto(42, "42", "Player 42", null, null, null, null, null, null);
            return Task.FromResult(new CareerOrderChallengeDto(player, correct.AsEnumerable().Reverse().ToList(), correct, HistoryVersion));
        }
        public Task<MissingClubChallengeDto> CreateMissingClubChallengeAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MissingClubRoundDto> StartMissingClubAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MissingClubRoundDto> GuessMissingClubAsync(MissingClubGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderRoundDto> StartCareerOrderAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderRoundDto> GuessCareerOrderAsync(CareerOrderGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<MysteryPlayerRoundDto> StartMysteryPlayerAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MysteryPlayerRoundDto> GuessMysteryPlayerAsync(MysteryPlayerGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
    }
}
