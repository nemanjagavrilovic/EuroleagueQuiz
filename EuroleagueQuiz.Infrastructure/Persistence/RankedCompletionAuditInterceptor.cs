using System.Text.Json;
using EuroleagueQuiz.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace EuroleagueQuiz.Infrastructure.Persistence;

/// <summary>
/// Adds one audit event in the same database transaction when a ranked daily
/// attempt changes from incomplete to completed. Practice and guest attempts
/// are deliberately excluded.
/// </summary>
public sealed class RankedCompletionAuditInterceptor : SaveChangesInterceptor
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
            if (!BecameCompleted(entry) || !entry.Entity.LeaderboardEligible || entry.Entity.UserId is null) continue;
            AddIfMissing(db, new AuditLog(
                entry.Entity.CompletedAtUtc ?? DateTime.UtcNow,
                entry.Entity.UserId,
                "User",
                "Game.GridRankedCompleted",
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
                    challengeId = entry.Entity.ChallengeId
                }, JsonOptions)));
        }

        foreach (var entry in db.ChangeTracker.Entries<DailyGameAttempt>().ToArray())
        {
            if (!BecameCompleted(entry) || !entry.Entity.LeaderboardEligible || entry.Entity.UserId is null) continue;
            AddIfMissing(db, new AuditLog(
                entry.Entity.CompletedAtUtc ?? DateTime.UtcNow,
                entry.Entity.UserId,
                "User",
                ActionFor(entry.Entity.Game),
                "Success",
                "DailyGameAttempt",
                entry.Entity.Id.ToString(),
                null,
                JsonSerializer.Serialize(new
                {
                    game = entry.Entity.Game,
                    score = entry.Entity.Score,
                    date = entry.Entity.Date,
                    challengeId = entry.Entity.ChallengeId
                }, JsonOptions)));
        }
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
            && entry.Entity.EntityType == auditLog.EntityType
            && entry.Entity.EntityId == auditLog.EntityId);
        if (!alreadyTracked) db.AuditLogs.Add(auditLog);
    }

    private static string ActionFor(string game) => game switch
    {
        "team-history" => "Game.TeamHistoryRankedCompleted",
        "missing-club" => "Game.MissingClubRankedCompleted",
        "career-order" => "Game.CareerOrderRankedCompleted",
        "mystery-player" => "Game.MysteryPlayerRankedCompleted",
        "five-player" => "Game.StartingFiveRankedCompleted",
        _ => "Game.RankedCompleted"
    };
}
