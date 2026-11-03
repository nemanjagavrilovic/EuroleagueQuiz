using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface IDailyGameRepository
{
    Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken cancellationToken);
    Task<DailyGameAttempt?> FindAttemptAsync(string game, Guid guestId, DateOnly date, CancellationToken cancellationToken);
    Task<DailyGameChallenge> GetOrCreateChallengeAsync(string game, DateOnly date, Func<Task<string>> createDefinition, CancellationToken cancellationToken);
    void Add(DailyGameAttempt attempt);
    Task SaveAsync(CancellationToken cancellationToken);
}
