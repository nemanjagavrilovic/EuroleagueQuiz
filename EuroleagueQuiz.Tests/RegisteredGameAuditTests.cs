using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json;
using EuroleagueQuiz.Api.Auditing;
using EuroleagueQuiz.Api.Controllers;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EuroleagueQuiz.Tests;

public sealed class RegisteredGameAuditTests
{
    [Theory]
    [InlineData("grid", "Grid")]
    [InlineData("team-history", "TeamHistory")]
    [InlineData("missing-club", "MissingClub")]
    [InlineData("career-order", "CareerOrder")]
    [InlineData("mystery-player", "MysteryPlayer")]
    [InlineData("five-player", "StartingFive")]
    public async Task Practice_start_and_finish_are_attributed_once_to_the_registered_player(string game, string actionName)
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter();
        var auditor = new PracticeGameAuditor(sessions, writer);
        var owner = Guid.NewGuid(); var context = Context(owner); var round = Guid.NewGuid();
        await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => auditor.StartAsync(context, game, round)));
        await auditor.CompleteAsync(context, game, round, false);
        Assert.Single(writer.Events);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => auditor.CompleteAsync(context, game, round, true, false, true)));
        Assert.Equal(new[] { $"Game.{actionName}PracticeStarted", $"Game.{actionName}PracticeCompleted" }, writer.Events.Select(x => x.Action));
        Assert.All(writer.Events, entry =>
        {
            Assert.Equal(owner, entry.ActorUserId);
            Assert.Equal("User", entry.ActorType);
            Assert.Equal(round.ToString(), entry.EntityId);
            Assert.Equal("practice", entry.Details.GetProperty("mode").GetString());
            Assert.False(entry.Details.GetProperty("leaderboardEligible").GetBoolean());
            Assert.False(entry.Details.TryGetProperty("answers", out _));
            Assert.False(entry.Details.TryGetProperty("email", out _));
        });
    }

    [Fact]
    public async Task Other_players_and_anonymous_requests_cannot_complete_an_owned_round()
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter(); var auditor = new PracticeGameAuditor(sessions, writer);
        var owner = Context(Guid.NewGuid()); var other = Context(Guid.NewGuid()); var round = Guid.NewGuid();
        await auditor.StartAsync(owner, "team-history", round);
        Assert.False(auditor.CanUse(other, "team-history", round));
        Assert.False(auditor.CanUse(new DefaultHttpContext(), "team-history", round));
        await auditor.CompleteAsync(other, "team-history", round, true);
        Assert.Single(writer.Events);
        await auditor.CompleteAsync(owner, "team-history", round, true);
        Assert.Equal(2, writer.Events.Count);
    }

    [Fact]
    public async Task Anonymous_legacy_practice_remains_usable_and_does_not_create_registered_events()
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter(); var auditor = new PracticeGameAuditor(sessions, writer);
        var context = new DefaultHttpContext(); var round = Guid.NewGuid();
        await auditor.StartAsync(context, "missing-club", round);
        Assert.True(auditor.CanUse(context, "missing-club", round));
        await auditor.CompleteAsync(context, "missing-club", round, true);
        Assert.Empty(writer.Events);
    }

    [Fact]
    public async Task Practice_grid_completion_requires_all_nine_server_validated_cells_and_is_idempotent()
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter(); var auditor = new PracticeGameAuditor(sessions, writer);
        var context = Context(Guid.NewGuid()); var round = Guid.NewGuid(); var grid = Grid();
        await auditor.StartAsync(context, "grid", round, grid);
        auditor.ValidateGrid(context, round, [1, 2, 3], [4, 5, 6]);
        var answers = Answers();
        await Assert.ThrowsAsync<ArgumentException>(() => auditor.CompleteGridAsync(context, round,
            Enumerable.Repeat<PracticeGridAnswer>(null!, 9).ToArray()));
        await Assert.ThrowsAsync<ArgumentException>(() => auditor.CompleteGridAsync(context, round, answers));
        foreach (var answer in answers)
            await auditor.RememberGridMatchAsync(context, round, Match(answer.PlayerId, [Cell(answer.Row, answer.Column, 2)]));
        await Assert.ThrowsAsync<ArgumentException>(() => auditor.CompleteGridAsync(context, round, answers.Take(8).ToArray()));
        await Assert.ThrowsAsync<ArgumentException>(() => auditor.CompleteGridAsync(context, round, [.. answers.Take(8), answers[0]]));
        await auditor.CompleteGridAsync(context, round, answers);
        await auditor.CompleteGridAsync(context, round, answers);
        var completed = Assert.Single(writer.Events.Where(x => x.Action.EndsWith("Completed")));
        Assert.True(completed.Details.GetProperty("correct").GetBoolean());
        Assert.Equal("Normal", completed.Details.GetProperty("difficulty").GetString());
        Assert.Equal(2, writer.Events.Count);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    public async Task Reused_grid_players_only_complete_when_each_reused_cell_is_a_sole_player_cell(int playerCount, bool allowed)
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter(); var auditor = new PracticeGameAuditor(sessions, writer);
        var context = Context(Guid.NewGuid()); var round = Guid.NewGuid();
        await auditor.StartAsync(context, "grid", round, Grid());
        var answers = Answers().Select(x => x.Row == 0 ? x with { PlayerId = 100 } : x).ToArray();
        await auditor.RememberGridMatchAsync(context, round, Match(100, [Cell(0, 0, playerCount), Cell(0, 1, playerCount), Cell(0, 2, playerCount)]));
        foreach (var answer in answers.Where(x => x.Row > 0))
            await auditor.RememberGridMatchAsync(context, round, Match(answer.PlayerId, [Cell(answer.Row, answer.Column, 2)]));
        if (allowed) await auditor.CompleteGridAsync(context, round, answers);
        else await Assert.ThrowsAsync<ArgumentException>(() => auditor.CompleteGridAsync(context, round, answers));
        Assert.Equal(allowed ? 2 : 1, writer.Events.Count);
    }

    [Fact]
    public async Task Grid_session_cannot_be_reused_for_another_player_or_layout_and_reveal_closes_it_once()
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter(); var auditor = new PracticeGameAuditor(sessions, writer);
        var owner = Context(Guid.NewGuid()); var other = Context(Guid.NewGuid()); var round = Guid.NewGuid();
        await auditor.StartAsync(owner, "grid", round, Grid());
        Assert.Throws<UnauthorizedAccessException>(() => auditor.ValidateGrid(other, round, [1,2,3], [4,5,6]));
        Assert.Throws<ArgumentException>(() => auditor.ValidateGrid(owner, round, [1,2,3], [4,5,7]));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => auditor.CompleteGridAsync(other, round, Answers()));
        await auditor.CompleteAsync(owner, "grid", round, true, false, true);
        // A late duplicate callback cannot replace a revealed result with a solved result.
        await auditor.CompleteGridAsync(owner, round, Answers());
        Assert.Equal(2, writer.Events.Count);
        Assert.True(writer.Events.Last().Details.GetProperty("revealed").GetBoolean());
        Assert.False(writer.Events.Last().Details.GetProperty("correct").GetBoolean());
    }

    [Fact]
    public void Claimed_guest_attempt_records_registered_daily_activity_without_granting_ranked_points()
    {
        using var db = Database();
        var attempt = new DailyGameAttempt { Game = "team-history", GuestId = Guid.NewGuid(), Date = new(2026,11,18), ChallengeId = 9 };
        db.Attach(attempt);
        attempt.UserId = Guid.NewGuid();
        attempt.UpdatedAtUtc = DateTime.UtcNow;
        attempt.Completed = true;
        Capture(db);
        Assert.Equal(2, db.AuditLogs.Local.Count);
        Assert.Contains(db.AuditLogs.Local, x => x.Action == "Game.TeamHistoryDailyStarted");
        Assert.Contains(db.AuditLogs.Local, x => x.Action == "Game.TeamHistoryDailyCompleted");
        Assert.DoesNotContain(db.AuditLogs.Local, x => x.ActorType == "Guest" || x.Action.Contains("Ranked"));
        Assert.False(attempt.LeaderboardEligible);
    }

    [Fact]
    public void Different_registered_players_starting_the_same_challenge_each_have_an_event()
    {
        using var db = Database();
        for (var i = 0; i < 2; i++) db.Add(new DailyGameAttempt { Game = "team-history", UserId = Guid.NewGuid(), GuestId = Guid.NewGuid(),
            LeaderboardEligible = true, Date = new(2026,11,18), ChallengeId = 9, UpdatedAtUtc = DateTime.UtcNow });
        Capture(db); Capture(db);
        Assert.Equal(2, db.AuditLogs.Local.Count);
        Assert.All(db.AuditLogs.Local, x => Assert.Equal("Game.TeamHistoryRankedStarted", x.Action));
    }

    [Fact]
    public void Claiming_an_already_finished_guest_attempt_does_not_invent_a_registered_game_start()
    {
        using var db = Database();
        var attempt = new DailyGameAttempt { Game = "team-history", GuestId = Guid.NewGuid(), Completed = true,
            Date = new(2026,11,18), ChallengeId = 9 };
        db.Attach(attempt);
        attempt.UserId = Guid.NewGuid();
        attempt.UpdatedAtUtc = DateTime.UtcNow;
        Capture(db);
        Assert.Empty(db.AuditLogs.Local);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ranked_refreshes_and_repeated_completed_responses_do_not_duplicate_events(bool grid)
    {
        using var db = Database();
        var user = Guid.NewGuid();
        object attempt = grid
            ? new DailyGridAttempt { Id = 21, UserId = user, GuestId = user, Ranked = true, LeaderboardEligible = true,
                Date = new(2026,11,18), ChallengeId = 9, UpdatedAtUtc = DateTime.UtcNow }
            : new DailyGameAttempt { UserId = user, GuestId = user, Game = "team-history", LeaderboardEligible = true,
                Date = new(2026,11,18), ChallengeId = 9, UpdatedAtUtc = DateTime.UtcNow };
        db.Add(attempt); Capture(db); Accept(db);
        db.Entry(attempt).Property("UpdatedAtUtc").CurrentValue = DateTime.UtcNow.AddMinutes(1);
        Capture(db); Accept(db);
        Assert.Single(db.AuditLogs.Local);
        db.Entry(attempt).Property("Completed").CurrentValue = true;
        Capture(db); Accept(db); Capture(db);
        Assert.Equal(2, db.AuditLogs.Local.Count);
        Assert.Single(db.AuditLogs.Local.Where(x => x.Action.EndsWith("Completed")));
    }

    [Fact]
    public async Task Starting_five_controller_records_whole_game_completion_only_after_round_five()
    {
        using var sessions = new PracticeAuditSessions();
        var writer = new RecordingWriter(); var auditor = new PracticeGameAuditor(sessions, writer);
        var game = new FiveService();
        var owner = Context(Guid.NewGuid());
        var controller = new FivePlayerGameController(game, auditor) { ControllerContext = new() { HttpContext = owner } };
        await controller.Start(default);
        game.Completed = true; game.Score = 3;
        await controller.Guess(new(game.Id, null, true), default);
        Assert.Single(writer.Events);
        game.Round = 5; game.Score = 12;
        await controller.Guess(new(game.Id, null, true), default);
        await controller.Next(game.Id, default);
        Assert.Equal(2, writer.Events.Count);
        var finish = writer.Events.Last();
        Assert.Equal("Game.StartingFivePracticeCompleted", finish.Action);
        Assert.Equal(12, finish.Details.GetProperty("correctPlayers").GetInt32());
        Assert.True(finish.Details.GetProperty("revealed").GetBoolean());
        controller.ControllerContext = new() { HttpContext = Context(Guid.NewGuid()) };
        Assert.IsType<ForbidResult>(await controller.Guess(new(game.Id, null, true), default));
        Assert.Equal(2, game.GuessCalls);
    }

    private static HttpContext Context(Guid owner) => new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, owner.ToString()), new Claim(ClaimTypes.Role, "Free")], "unit-test")) };
    private static GridDefinitionDto Grid() => new(Enumerable.Range(1,3).Select(Team).ToArray(), Enumerable.Range(4,3).Select(Team).ToArray(), "Normal");
    private static TeamDto Team(int id) => new(id, $"T{id}", $"Club {id}", null, null, null, null, null);
    private static PracticeGridAnswer[] Answers() => Enumerable.Range(0,9).Select(i => new PracticeGridAnswer(i/3, i%3, i+1)).ToArray();
    private static GridCellDto Cell(int row, int col, int count) => new(row, col, row+1, col+4, count);
    private static GridPlayerMatchResponse Match(long playerId, IReadOnlyList<GridCellDto> cells) =>
        new(new(playerId, $"P{playerId}", $"Player {playerId}", null, null, null, null, null, null), cells);
    private static EuroleagueDbContext Database() => new(new DbContextOptionsBuilder<EuroleagueDbContext>().UseNpgsql("Host=127.0.0.1;Database=audit_unit_tests;Username=unused").Options);
    private static void Capture(EuroleagueDbContext db) => new GameAttemptAuditInterceptor().SavingChanges(new DbContextEventData(null!, (_,_) => "", db), default);
    private static void Accept(EuroleagueDbContext db)
    {
        var next = db.AuditLogs.Local.Where(x => x.Id > 0).Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
        foreach (var log in db.ChangeTracker.Entries<AuditLog>().Where(x => x.State == EntityState.Added))
        {
            log.Property(x => x.Id).CurrentValue = next++;
            log.Property(x => x.Id).IsTemporary = false;
        }
        db.ChangeTracker.AcceptAllChanges();
    }

    private sealed class FiveService : IFivePlayerGameService
    {
        internal Guid Id { get; } = Guid.NewGuid();
        internal int Round { get; set; } = 1;
        internal int Score { get; set; }
        internal bool Completed { get; set; }
        internal int GuessCalls { get; private set; }
        private FivePlayerGameRoundDto Current() => new(Id, Round, 5, 24, 0, Team(1), [], Completed, null, Completed && Round == 5, Score, null);
        public Task<FivePlayerGameRoundDto> StartAsync(CancellationToken ct) => Task.FromResult(Current());
        public Task<FivePlayerGameRoundDto> GuessAsync(FivePlayerGameGuessRequest request, CancellationToken ct)
        { GuessCalls++; return Task.FromResult(Current()); }
        public Task<FivePlayerGameRoundDto> NextAsync(Guid id, CancellationToken ct) => Task.FromResult(Current());
        public Task<FivePlayerChallengeDto> CreateChallengeAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed record RecordedEvent(string Action, string? EntityId, Guid? ActorUserId, string? ActorType, JsonElement Details);
    private sealed class RecordingWriter : IAuditLogWriter
    {
        public ConcurrentQueue<RecordedEvent> Events { get; } = new();
        public async Task WriteAsync(string action, string result, string? entityType = null, string? entityId = null,
            object? details = null, Guid? actorUserId = null, string? actorType = null, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1, cancellationToken);
            Events.Enqueue(new(action, entityId, actorUserId, actorType,
                JsonSerializer.SerializeToElement(details, new JsonSerializerOptions(JsonSerializerDefaults.Web))));
        }
    }
}
