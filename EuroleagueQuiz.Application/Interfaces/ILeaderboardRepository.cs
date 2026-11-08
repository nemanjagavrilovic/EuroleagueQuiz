using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ILeaderboardRepository
{
    Task<IReadOnlyList<LeaderboardAggregate>> GetScoresAsync(
        DateOnly? fromDate,
        DateOnly toDateExclusive,
        string? game,
        CancellationToken cancellationToken);
}
