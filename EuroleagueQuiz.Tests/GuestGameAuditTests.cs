using System.Text.Json;
using EuroleagueQuiz.Api.Administration;
using EuroleagueQuiz.Domain.Auditing;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EuroleagueQuiz.Tests;

public sealed class GuestGameAuditTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

    [Theory]
    [InlineData("grid")]
    [InlineData("team-history")]
    [InlineData("missing-club")]
    [InlineData("career-order")]
    [InlineData("mystery-player")]
    [InlineData("five-player")]
    public void Each_game_records_one_guest_start_and_one_completion(string game)
    {
        using var db = Context();
        var guestId = Guid.NewGuid();
        var attempt = AddAttempt(db, game, guestId);
        Capture(db);
        Assert.Single(db.AuditLogs.Local);
        Accept(db);

        // A resumed page, polling request or guess cannot create another start.
        db.Entry(attempt).Property("UpdatedAtUtc").CurrentValue = Now.AddMinutes(1);
        Capture(db);
        Assert.Single(db.AuditLogs.Local);
        Accept(db);

        db.Entry(attempt).Property("Completed").CurrentValue = true;
        db.Entry(attempt).Property("CompletedAtUtc").CurrentValue = Now.AddMinutes(2);
        Capture(db);
        var definition = GuestGameAudit.Games.Single(x => x.Game == game);
        Assert.Equal(new[] { definition.StartedAction, definition.CompletedAction }, db.AuditLogs.Local.OrderBy(x => x.OccurredAtUtc).Select(x => x.Action));
        Assert.All(db.AuditLogs.Local, log =>
        {
            Assert.Equal(GuestGameAudit.ActorType, log.ActorType);
            Assert.Null(log.ActorUserId);
            Assert.Equal(guestId, GuestGameAudit.BrowserId(log.EntityId));
            Assert.Equal(DateTimeKind.Utc, log.OccurredAtUtc.Kind);
        });
        Accept(db);
        Capture(db);
        Assert.Equal(2, db.AuditLogs.Local.Count);
    }

    [Fact]
    public async Task Repeated_save_interception_does_not_duplicate_added_events_and_zero_point_completions_are_recorded()
    {
        using var db = Context();
        var attempt = AddAttempt(db, "grid", Guid.NewGuid());
        db.Entry(attempt).Property("Completed").CurrentValue = true;
        var interceptor = new GameAttemptAuditInterceptor();
        await interceptor.SavingChangesAsync(Event(db), default);
        await interceptor.SavingChangesAsync(Event(db), default);
        Assert.Equal(2, db.AuditLogs.Local.Count);
        Assert.Contains(db.AuditLogs.Local, x => x.Action == "Game.GridGuestCompleted" && x.Result == "Success");
        Assert.All(db.AuditLogs.Local, x => Assert.Equal(0, JsonDocument.Parse(x.DetailsJson!).RootElement.GetProperty("score").GetInt32()));
    }

    [Fact]
    public void Four_grid_levels_are_four_attempts_for_one_browser_even_before_database_ids_are_assigned()
    {
        using var db = Context();
        var guest = Guid.NewGuid();
        foreach (var (level, index) in new[] { "Easy", "Normal", "Hard", "Legend" }.Select((level, index) => (level, index)))
            db.DailyGridAttempts.Add(new DailyGridAttempt
            {
                GuestId = guest, Date = Today, ChallengeId = index + 1,
                Challenge = new() { Id = index + 1, Date = Today, Difficulty = level, Ranked = false },
                StateJson = "{}", UpdatedAtUtc = Now
            });
        Capture(db);
        Assert.Equal(4, db.AuditLogs.Local.Count);
        var activity = GuestActivityQueries.Tracked(db.AuditLogs.Local.AsQueryable());
        Assert.Single(GuestActivityQueries.UniqueGuestIds(activity));
        var grid = Assert.Single(GuestActivityQueries.ByGame(activity));
        Assert.Equal(4, grid.GamesPlayed);
        Assert.Equal(1, grid.UniqueGuests);
        Assert.Equal(0, grid.CompletedGames);
    }

    [Theory]
    [InlineData("grid", "Game.GridRankedCompleted")]
    [InlineData("team-history", "Game.TeamHistoryRankedCompleted")]
    [InlineData("missing-club", "Game.MissingClubRankedCompleted")]
    [InlineData("career-order", "Game.CareerOrderRankedCompleted")]
    [InlineData("mystery-player", "Game.MysteryPlayerRankedCompleted")]
    [InlineData("five-player", "Game.StartingFiveRankedCompleted")]
    public void Ranked_start_is_added_and_the_existing_completion_event_is_preserved(string game, string expectedAction)
    {
        using var db = Context();
        var attempt = AddAttempt(db, game, Guid.NewGuid());
        db.Entry(attempt).Property("UserId").CurrentValue = Guid.NewGuid();
        db.Entry(attempt).Property("LeaderboardEligible").CurrentValue = true;
        db.Entry(attempt).Property("Completed").CurrentValue = true;
        if (attempt is DailyGridAttempt grid) grid.Ranked = true;
        Capture(db);
        Assert.Equal(2, db.AuditLogs.Local.Count);
        Assert.Contains(db.AuditLogs.Local, x => x.Action == expectedAction.Replace("Completed", "Started"));
        var log = Assert.Single(db.AuditLogs.Local.Where(x => x.Action == expectedAction));
        Assert.Equal(expectedAction, log.Action);
        Assert.Equal("User", log.ActorType);
        Assert.NotNull(log.ActorUserId);
    }

    [Fact]
    public void Guest_events_contain_only_game_context_and_never_answer_state_or_contact_information()
    {
        using var db = Context();
        AddAttempt(db, "team-history", Guid.NewGuid());
        Capture(db);
        var log = Assert.Single(db.AuditLogs.Local);
        using var json = JsonDocument.Parse(log.DetailsJson!);
        var allowed = new[] { "game", "date", "challengeId", "difficulty", "score", "mode", "leaderboardEligible" };
        Assert.All(json.RootElement.EnumerateObject(), property => Assert.Contains(property.Name, allowed));
        Assert.False(json.RootElement.GetProperty("leaderboardEligible").GetBoolean());
        Assert.Null(log.ActorUserId);
    }

    [Fact]
    public void Activity_counts_distinct_browsers_and_attempts_across_starts_completions_and_duplicates()
    {
        var guest = Guid.NewGuid();
        var other = Guid.NewGuid();
        var records = new[]
        {
            Log(guest, "grid", 1, false), Log(guest, "grid", 1, true), Log(guest, "grid", 1, true),
            Log(guest, "grid", 2, false), Log(guest, "team-history", 3, false),
            Log(other, "team-history", 3, true),
            new AuditLog(Now, Guid.NewGuid(), "User", "Game.GridRankedCompleted", "Success", "DailyGridAttempt", "42", null, null)
        };
        var activity = GuestActivityQueries.Tracked(records.AsQueryable());
        Assert.Equal(2, GuestActivityQueries.UniqueGuestIds(activity).Count());
        var counts = GuestActivityQueries.ByGame(activity).ToList();
        var grid = counts.Single(x => x.Key == "Game.Grid");
        Assert.Equal((1, 2, 1), (grid.UniqueGuests, grid.GamesPlayed, grid.CompletedGames));
        var history = counts.Single(x => x.Key == "Game.TeamHistory");
        Assert.Equal((2, 2, 1), (history.UniqueGuests, history.GamesPlayed, history.CompletedGames));
    }

    [Fact]
    public void Guest_aggregation_translates_to_postgresql_without_loading_individual_events()
    {
        using var db = Context();
        var activity = GuestActivityQueries.Tracked(db.AuditLogs.Where(x => x.OccurredAtUtc >= Now));
        var sql = GuestActivityQueries.ByGame(activity).ToQueryString();
        Assert.Contains("count(DISTINCT", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GROUP BY", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("substring", GuestActivityQueries.UniqueGuestIds(activity).ToQueryString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Reversed_report_dates_are_rejected_before_any_database_access()
    {
        using var db = Context();
        await Assert.ThrowsAsync<ArgumentException>(() => new AdminAuditLogService(db).GetGuestActivityAsync(Now, Now.AddDays(-1), default));
    }

    private static AuditLog Log(Guid guest, string game, long challengeId, bool completed)
    {
        var definition = GuestGameAudit.Games.Single(x => x.Game == game);
        return new(Now, null, GuestGameAudit.ActorType, completed ? definition.CompletedAction : definition.StartedAction,
            completed ? "Success" : "Started", GuestGameAudit.EntityType,
            GuestGameAudit.AttemptKey(guest, game, Today, challengeId), null, "{}");
    }

    private static object AddAttempt(EuroleagueDbContext db, string game, Guid guest)
    {
        if (game == "grid")
        {
            var grid = new DailyGridAttempt { Id = 11, GuestId = guest, Date = Today, ChallengeId = 7,
                Challenge = new() { Id = 7, Date = Today, Difficulty = "Normal" }, UpdatedAtUtc = Now, StateJson = "{}" };
            db.Add(grid);
            return grid;
        }
        var attempt = new DailyGameAttempt { GuestId = guest, Game = game, Date = Today, ChallengeId = 7,
            Challenge = new() { Id = 7, Game = game, Date = Today }, UpdatedAtUtc = Now, StateJson = "{}" };
        db.Add(attempt);
        return attempt;
    }

    private static EuroleagueDbContext Context() => new(new DbContextOptionsBuilder<EuroleagueDbContext>()
        .UseNpgsql("Host=127.0.0.1;Database=guest_audit_unit_tests;Username=unused").Options);
    private static DbContextEventData Event(EuroleagueDbContext db) => new(null!, (_, _) => "", db);
    private static void Capture(EuroleagueDbContext db) => new GameAttemptAuditInterceptor().SavingChanges(Event(db), default);
    private static void Accept(EuroleagueDbContext db)
    {
        var id = db.AuditLogs.Local.Where(x => x.Id > 0).Select(x => x.Id).DefaultIfEmpty(0).Max() + 1;
        foreach (var log in db.ChangeTracker.Entries<AuditLog>().Where(x => x.State == EntityState.Added))
        {
            log.Property(x => x.Id).CurrentValue = id++;
            log.Property(x => x.Id).IsTemporary = false;
        }
        db.ChangeTracker.AcceptAllChanges();
    }
}
