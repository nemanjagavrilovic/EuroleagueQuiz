using System.Security.Cryptography;
using System.Text;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class DailyGridRepository(EuroleagueDbContext db) : IDailyGridRepository
{
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

    public Task<DailyGridAttempt?> FindAttemptAsync(Guid guestId, DateOnly date, CancellationToken cancellationToken) =>
        db.DailyGridAttempts.Include(x => x.Challenge).SingleOrDefaultAsync(x => x.GuestId == guestId && x.Date == date, cancellationToken);

    public async Task<DailyGridChallenge> GetOrCreateChallengeAsync(DateOnly date, string difficulty, Func<Task<string>> createDefinition, CancellationToken cancellationToken)
    {
        var challenge = await FindChallengeAsync(date, difficulty, cancellationToken);
        if (challenge is not null) return challenge;
        await LockAsync($"grid-challenge:{date:yyyy-MM-dd}:{difficulty}", cancellationToken);
        challenge = await FindChallengeAsync(date, difficulty, cancellationToken);
        if (challenge is not null) return challenge;
        challenge = new DailyGridChallenge { Date = date, Difficulty = difficulty, DefinitionJson = await createDefinition() };
        db.DailyGridChallenges.Add(challenge);
        await SaveAsync(cancellationToken);
        return challenge;
    }

    private Task<DailyGridChallenge?> FindChallengeAsync(DateOnly date, string difficulty, CancellationToken cancellationToken) =>
        db.DailyGridChallenges.SingleOrDefaultAsync(x => x.Date == date && x.Difficulty == difficulty, cancellationToken);

    private Task<int> LockAsync(string key, CancellationToken cancellationToken)
    {
        var lockId = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0);
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})", cancellationToken);
    }

    public void Add(DailyGridAttempt attempt) => db.DailyGridAttempts.Add(attempt);
    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
