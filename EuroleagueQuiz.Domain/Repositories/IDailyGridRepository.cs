using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface IDailyGridRepository
{
    Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken cancellationToken);
    Task<DailyGridAttempt?> FindAttemptAsync(Guid guestId, DateOnly date, CancellationToken cancellationToken);
    Task<DailyGridChallenge> GetOrCreateChallengeAsync(DateOnly date, string difficulty, Func<Task<string>> createDefinition, CancellationToken cancellationToken);
    void Add(DailyGridAttempt attempt);
    Task SaveAsync(CancellationToken cancellationToken);
}
