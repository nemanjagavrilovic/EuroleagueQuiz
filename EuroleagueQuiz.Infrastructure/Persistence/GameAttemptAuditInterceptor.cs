using System.Text.Json;
using EuroleagueQuiz.Domain.Auditing;
using EuroleagueQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EuroleagueQuiz.Infrastructure.Persistence;

/// <summary>
/// Records guest and registered daily starts/completions in the same database
/// transaction as the attempt. Resuming, polling and repeated guesses do not
/// create another start or completion event. Practice is not persisted here.
/// </summary>
public sealed class GameAttemptAuditInterceptor : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        AddCompletionEvents(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        AddCompletionEvents(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void AddCompletionEvents(DbContext? context)
    {
        if (context is not EuroleagueDbContext db) return;

        foreach (var entry in db.ChangeTracker.Entries<DailyGridAttempt>().ToArray())
        {
            if (entry.Entity.UserId is null && !entry.Entity.Ranked && !entry.Entity.LeaderboardEligible)
                AddGuestEvents(db, entry, entry.Entity.GuestId, "grid", entry.Entity.Date,
                    entry.Entity.ChallengeId, entry.Entity.UpdatedAtUtc, entry.Entity.CompletedAtUtc,
                    entry.Entity.Score, entry.Entity.Challenge?.Difficulty);
            if (entry.Entity.UserId is null) continue;
            var mode = entry.Entity.LeaderboardEligible ? "ranked" : "daily";
            if (BecameRegisteredAttempt(entry))
                AddRegisteredStart(db, entry.Entity.UserId.Value, "grid", mode, entry.Entity.Date,
                    entry.Entity.ChallengeId, entry.Entity.UpdatedAtUtc, entry.Entity.Challenge?.Difficulty);
            if (!BecameCompleted(entry)) continue;
            AddIfMissing(db, new AuditLog(
                entry.Entity.CompletedAtUtc ?? DateTime.UtcNow,
                entry.Entity.UserId,
                "User",
                GameAuditCatalog.Action("grid", mode, true),
                "Success",
                "DailyGridAttempt",
                entry.Entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    game = "grid",
                    difficulty = entry.Entity.Challenge?.Difficulty,
                    score = entry.Entity.Score,
                    date = entry.Entity.Date,
                    challengeId = entry.Entity.ChallengeId,
                    mode,
                    leaderboardEligible = entry.Entity.LeaderboardEligible
                }, JsonOptions)));
        }

        foreach (var entry in db.ChangeTracker.Entries<DailyGameAttempt>().ToArray())
        {
            if (entry.Entity.UserId is null && !entry.Entity.LeaderboardEligible)
                AddGuestEvents(db, entry, entry.Entity.GuestId, entry.Entity.Game, entry.Entity.Date,
                    entry.Entity.ChallengeId, entry.Entity.UpdatedAtUtc, entry.Entity.CompletedAtUtc, entry.Entity.Score);
            if (entry.Entity.UserId is null) continue;
            var mode = entry.Entity.LeaderboardEligible ? "ranked" : "daily";
            if (BecameRegisteredAttempt(entry))
                AddRegisteredStart(db, entry.Entity.UserId.Value, entry.Entity.Game, mode, entry.Entity.Date,
                    entry.Entity.ChallengeId, entry.Entity.UpdatedAtUtc);
            if (!BecameCompleted(entry)) continue;
            AddIfMissing(db, new AuditLog(
                entry.Entity.CompletedAtUtc ?? DateTime.UtcNow,
                entry.Entity.UserId,
                "User",
                GameAuditCatalog.Action(entry.Entity.Game, mode, true),
                "Success",
                "DailyGameAttempt",
                entry.Entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    game = entry.Entity.Game,
                    score = entry.Entity.Score,
                    date = entry.Entity.Date,
                    challengeId = entry.Entity.ChallengeId,
                    mode,
                    leaderboardEligible = entry.Entity.LeaderboardEligible
                }, JsonOptions)));
        }
    }

    private static bool BecameRegisteredAttempt<TEntity>(EntityEntry<TEntity> entry) where TEntity : class =>
        entry.State == EntityState.Added || entry.State == EntityState.Modified
        && entry.Property(nameof(DailyGameAttempt.UserId)).OriginalValue is null
        && !(bool)entry.Property(nameof(DailyGameAttempt.Completed)).OriginalValue!;

    private static void AddRegisteredStart(EuroleagueDbContext db, Guid userId, string game, string mode,
        DateOnly date, long challengeId, DateTime occurredAt, string? difficulty = null) =>
        AddIfMissing(db, new AuditLog(occurredAt == default ? DateTime.UtcNow : occurredAt, userId, "User",
            GameAuditCatalog.Action(game, mode, false), "Started",
            game == "grid" ? "DailyGridChallenge" : "DailyGameChallenge", challengeId.ToString(), null,
            JsonSerializer.Serialize(new { game, mode, date, challengeId, difficulty, leaderboardEligible = mode == "ranked" }, JsonOptions)));

    private static void AddGuestEvents<TEntity>(
        EuroleagueDbContext db, EntityEntry<TEntity> entry, Guid guestId, string game, DateOnly date,
        long challengeId, DateTime updatedAtUtc, DateTime? completedAtUtc, int score, string? difficulty = null)
        where TEntity : class
    {
        if (guestId == Guid.Empty) return;
        var definition = GuestGameAudit.Games.FirstOrDefault(x => x.Game == game);
        if (definition is null) return;
        var started = entry.State == EntityState.Added;
        var completed = BecameCompleted(entry);
        if (!started && !completed) return;

        var key = GuestGameAudit.AttemptKey(guestId, game, date, challengeId);
        var occurredAtUtc = updatedAtUtc == default ? DateTime.UtcNow : updatedAtUtc;
        var details = JsonSerializer.Serialize(new
        {
            game, date, challengeId, difficulty, score, mode = "guest-daily", leaderboardEligible = false
        }, JsonOptions);
        if (started)
            AddIfMissing(db, new AuditLog(occurredAtUtc, null, GuestGameAudit.ActorType,
                definition.StartedAction, "Started", GuestGameAudit.EntityType, key, null, details));
        if (completed)
            AddIfMissing(db, new AuditLog(completedAtUtc ?? occurredAtUtc, null, GuestGameAudit.ActorType,
                definition.CompletedAction, "Success", GuestGameAudit.EntityType, key, null, details));
    }

    private static bool BecameCompleted<TEntity>(EntityEntry<TEntity> entry) where TEntity : class =>
        entry.State switch
        {
            EntityState.Added => (bool)entry.Property(nameof(DailyGameAttempt.Completed)).CurrentValue!,
            EntityState.Modified =>
                (bool)entry.Property(nameof(DailyGameAttempt.Completed)).CurrentValue!
                && !(bool)entry.Property(nameof(DailyGameAttempt.Completed)).OriginalValue!,
            _ => false
        };

    private static void AddIfMissing(EuroleagueDbContext db, AuditLog auditLog)
    {
        var alreadyTracked = db.ChangeTracker.Entries<AuditLog>().Any(entry =>
            entry.Entity.Action == auditLog.Action
            && entry.Entity.ActorUserId == auditLog.ActorUserId
            && entry.Entity.EntityType == auditLog.EntityType
            && entry.Entity.EntityId == auditLog.EntityId);
        if (!alreadyTracked) db.AuditLogs.Add(auditLog);
    }

}
