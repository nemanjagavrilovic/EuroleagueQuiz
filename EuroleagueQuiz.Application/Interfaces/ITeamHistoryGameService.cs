using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ITeamHistoryGameService
{
    Task<TeamHistoryRoundDto> StartAsync(CancellationToken cancellationToken);
    Task<TeamHistoryRoundDto> GuessAsync(TeamHistoryGuessRequest request, CancellationToken cancellationToken);
}
