using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyTeamHistoryService
{
    Task<DailyTeamHistoryDto> StartAsync(DailyAttemptIdentity identity, CancellationToken cancellationToken);
    Task<DailyTeamHistoryDto> GuessAsync(DailyAttemptIdentity identity, DailyTeamHistoryGuessRequest request, CancellationToken cancellationToken);
}
