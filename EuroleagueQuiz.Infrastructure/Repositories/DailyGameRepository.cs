using System.Security.Cryptography;
using System.Text;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class DailyGameRepository(EuroleagueDbContext db) : IDailyGameRepository
{
    public Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await LockAsync(key, ct);
            var result = await action();
            await transaction.CommitAsync(ct);
            return result;
        });

    public Task<DailyGameAttempt?> FindAttemptAsync(string game, Guid guestId, DateOnly date, CancellationToken ct) =>
        db.DailyGameAttempts.Include(x => x.Challenge).SingleOrDefaultAsync(x => x.Game == game && x.GuestId == guestId && x.Date == date, ct);

    public async Task<DailyGameChallenge> GetOrCreateChallengeAsync(string game, DateOnly date, Func<Task<string>> createDefinition, CancellationToken ct)
    {
        var challenge = await FindChallengeAsync(game, date, ct);
        if (challenge is not null) return challenge;
        await LockAsync($"daily-challenge:{game}:{date:yyyy-MM-dd}", ct);
        challenge = await FindChallengeAsync(game, date, ct);
        if (challenge is not null) return challenge;
        challenge = new() { Game = game, Date = date, DefinitionJson = await createDefinition() };
        db.DailyGameChallenges.Add(challenge);
        await SaveAsync(ct);
        return challenge;
    }

    private Task<DailyGameChallenge?> FindChallengeAsync(string game, DateOnly date, CancellationToken ct) =>
        db.DailyGameChallenges.SingleOrDefaultAsync(x => x.Game == game && x.Date == date, ct);

    private Task<int> LockAsync(string key, CancellationToken ct)
    {
        var id = BitConverter.ToInt64(SHA256.HashData(Encoding.UTF8.GetBytes(key)), 0);
        return db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({id})", ct);
    }

    public void Add(DailyGameAttempt attempt) => db.DailyGameAttempts.Add(attempt);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
