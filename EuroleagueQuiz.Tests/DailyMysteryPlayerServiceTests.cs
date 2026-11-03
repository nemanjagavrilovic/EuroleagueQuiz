using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class DailyMysteryPlayerServiceTests
{
    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 80)]
    [InlineData(2, 60)]
    [InlineData(3, 40)]
    [InlineData(4, 20)]
    public async Task Correct_guess_awards_points_for_visible_clues_and_restores_final_result(int skips, int score)
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        Assert.Single(state.Round.RevealedClues); Assert.Null(state.Round.CorrectPlayer);
        for (var i = 0; i < skips; i++) state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), null, true), default);
        Assert.Equal(score, state.AvailablePoints); Assert.Equal(0, state.Score);
        state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 42), default);
        Assert.True(state.Round.Completed); Assert.True(state.Round.Correct);
        Assert.Equal(score, state.Score); Assert.Equal(0, state.AvailablePoints);
        Assert.Equal(42, state.Round.CorrectPlayer!.Id); Assert.False(state.LeaderboardEligible);
        Assert.Equal(score, (await f.Service.StartAsync(guest, default)).Score);
    }

    [Fact]
    public async Task Shared_clues_have_independent_guest_progress()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        var wrong = await f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), 1), default);
        Assert.Equal(2, wrong.Round.RevealedClueCount); Assert.Equal(80, wrong.AvailablePoints); Assert.Equal(0, wrong.Score);
        var second = await f.Service.StartAsync(Guid.NewGuid(), default);
        Assert.Equal(first.ChallengeId, second.ChallengeId);
        Assert.Equal(first.Round.RevealedClues, second.Round.RevealedClues);
        Assert.Null(second.Round.CorrectPlayer); Assert.Equal(100, second.AvailablePoints);
        Assert.Equal(1, f.Games.Created);
    }

    [Fact]
    public async Task Final_miss_or_reveal_awards_zero_and_cannot_be_replayed()
    {
        foreach (var skip in new[] { true, false })
        {
            var f = new Fixture(); var guest = Guid.NewGuid();
            var state = await f.Service.StartAsync(guest, default);
            for (var i = 0; i < 5; i++) state = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), skip ? null : i + 1, skip), default);
            Assert.True(state.Round.Completed); Assert.False(state.Round.Correct); Assert.Equal(0, state.Score);
            Assert.Equal(5, state.Round.RevealedClues.Count); Assert.Equal(42, state.Round.CorrectPlayer!.Id);
            var replay = await f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 42), default);
            Assert.Equal(0, replay.Score); Assert.False(replay.Round.Correct);
        }
    }

    [Fact]
    public async Task Duplicate_or_invalid_guesses_do_not_reveal_extra_clues()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var state = await f.Service.StartAsync(guest, default);
        var request = new DailyMysteryPlayerGuessRequest(state.ChallengeId, Guid.NewGuid(), 1);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => f.Service.GuessAsync(guest, request, default)));
        state = await f.Service.StartAsync(guest, default);
        Assert.Equal(2, state.Round.RevealedClueCount); Assert.Single(state.UsedPlayerIds);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), 1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), -1), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.NewGuid(), null), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(state.ChallengeId, Guid.Empty, null, true), default));
        Assert.Equal(80, (await f.Service.StartAsync(guest, default)).AvailablePoints);
    }

    [Fact]
    public async Task Progress_survives_service_restart_and_utc_midnight_rejects_stale_guesses()
    {
        var f = new Fixture(); var guest = Guid.NewGuid();
        var first = await f.Service.StartAsync(guest, default);
        await f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), null, true), default);
        var restored = await f.Service.StartAsync(guest, default);
        Assert.Equal(first.Round.RoundId, restored.Round.RoundId); Assert.Equal(2, restored.Round.RevealedClueCount);
        f.Clock.Now = f.Clock.Now.AddDays(1);
        var next = await f.Service.StartAsync(guest, default);
        Assert.NotEqual(first.ChallengeId, next.ChallengeId); Assert.Equal(100, next.AvailablePoints);
        Assert.Equal(DateTimeKind.Utc, next.ResetsAtUtc.Kind);
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(guest, new(first.ChallengeId, Guid.NewGuid(), 42), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.GuessAsync(Guid.NewGuid(), new(next.ChallengeId, Guid.NewGuid(), 42), default));
        await Assert.ThrowsAsync<ArgumentException>(() => f.Service.StartAsync(Guid.Empty, default));
    }

    private sealed class Fixture
    {
        public MemoryRepository Repository { get; } = new();
        public FakeGames Games { get; } = new();
        public FakeClock Clock { get; } = new();
        public DailyMysteryPlayerService Service => new(Repository, Games, new FakePlayers(), Clock);
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 2, 23, 59, 0, TimeSpan.Zero);
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
    private sealed class FakeGames : ICareerGamesService
    {
        public int Created { get; private set; }
        public Task<MysteryPlayerChallengeDto> CreateMysteryPlayerChallengeAsync(CancellationToken ct)
        { Created++; return Task.FromResult(new MysteryPlayerChallengeDto(Player(42), Enumerable.Range(1, 5).Select(i => "Clue " + i).ToList())); }
        public Task<CareerOrderChallengeDto> CreateCareerOrderChallengeAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MissingClubChallengeDto> CreateMissingClubChallengeAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MissingClubRoundDto> StartMissingClubAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MissingClubRoundDto> GuessMissingClubAsync(MissingClubGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderRoundDto> StartCareerOrderAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<CareerOrderRoundDto> GuessCareerOrderAsync(CareerOrderGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
        public Task<MysteryPlayerRoundDto> StartMysteryPlayerAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<MysteryPlayerRoundDto> GuessMysteryPlayerAsync(MysteryPlayerGuessRequest request, CancellationToken ct) => throw new NotSupportedException();
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
