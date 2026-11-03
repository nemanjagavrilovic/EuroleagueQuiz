using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyTeamHistoryService
{
    Task<DailyTeamHistoryDto> StartAsync(Guid guestId, CancellationToken cancellationToken);
    Task<DailyTeamHistoryDto> GuessAsync(Guid guestId, DailyTeamHistoryGuessRequest request, CancellationToken cancellationToken);
}
