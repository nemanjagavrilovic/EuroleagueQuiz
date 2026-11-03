using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ITeamHistoryGameService
{
    Task<TeamHistoryChallengeDto> CreateChallengeAsync(CancellationToken cancellationToken);
    Task<TeamHistoryRoundDto> StartAsync(CancellationToken cancellationToken);
    Task<TeamHistoryRoundDto> GuessAsync(TeamHistoryGuessRequest request, CancellationToken cancellationToken);
}
