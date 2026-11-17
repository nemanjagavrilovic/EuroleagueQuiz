using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface IDailyGridRepository
{
    Task<DateOnly> GetWeightedScoringStartDateAsync(CancellationToken cancellationToken);
    Task<T> WithLockAsync<T>(string key, Func<Task<T>> action, CancellationToken cancellationToken);
    Task<DailyGridAttempt?> FindAttemptAsync(Guid attemptId, DateOnly date, bool ranked, string? difficulty, long? challengeId, CancellationToken cancellationToken);
    Task<DailyGridChallenge> GetOrCreateChallengeAsync(DateOnly date, string difficulty, bool ranked, Func<IReadOnlyList<DailyGridChallenge>, Task<string>> createDefinition, CancellationToken cancellationToken);
    void Add(DailyGridAttempt attempt);
    Task SaveAsync(CancellationToken cancellationToken);
}
