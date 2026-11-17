using System.Security.Cryptography;
using System.Text;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class DailyGridRepository(EuroleagueDbContext db) : IDailyGridRepository
{
    public Task<DateOnly> GetWeightedScoringStartDateAsync(CancellationToken cancellationToken) =>
        db.GridScoringSchedules.AsNoTracking().Select(x => x.WeightedFromDate).SingleAsync(cancellationToken);

    public async Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken cancellationToken)
    {
        // PostgreSQL transaction locks also serialize requests from different API instances.
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await LockAsync(key, cancellationToken);
            var result = await action();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    public Task<DailyGridAttempt?> FindAttemptAsync(
        Guid attemptId, DateOnly date, bool ranked, string? difficulty, long? challengeId, CancellationToken cancellationToken)
    {
        var attempts = db.DailyGridAttempts.Include(x => x.Challenge)
            .Where(x => x.Date == date && x.Ranked == ranked);
        attempts = ranked
            ? attempts.Where(x => x.UserId == attemptId)
            : attempts.Where(x => x.GuestId == attemptId);
        attempts = challengeId.HasValue
            ? attempts.Where(x => x.ChallengeId == challengeId)
            : attempts.Where(x => x.Challenge.Difficulty == difficulty);
        return attempts.SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<DailyGridChallenge> GetOrCreateChallengeAsync(
        DateOnly date, string difficulty, bool ranked, Func<IReadOnlyList<DailyGridChallenge>, Task<string>> createDefinition, CancellationToken cancellationToken)
    {
        var challenge = await db.DailyGridChallenges.SingleOrDefaultAsync(
            x => x.Date == date && x.Difficulty == difficulty && x.Ranked == ranked, cancellationToken);
        if (challenge is not null) return challenge;
        // Creation across levels and modes must see every layout already used today.
        await LockAsync($"grid-challenge:{date:yyyy-MM-dd}", cancellationToken);
        var today = await db.DailyGridChallenges.Where(x => x.Date == date).ToListAsync(cancellationToken);
        challenge = today.SingleOrDefault(x => x.Difficulty == difficulty && x.Ranked == ranked);
        if (challenge is not null) return challenge;
        challenge = new DailyGridChallenge
        {
            Date = date, Difficulty = difficulty, Ranked = ranked, DefinitionJson = await createDefinition(today),
            ScoringVersion = GridRankedScoring.WeightedVersion
        };
        db.DailyGridChallenges.Add(challenge);
        await SaveAsync(cancellationToken);
        return challenge;
    }

    private Task<int> LockAsync(string key, CancellationToken cancellationToken)
    {
        var lockId = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0);
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
    }

    public void Add(DailyGridAttempt attempt) => db.DailyGridAttempts.Add(attempt);
    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
