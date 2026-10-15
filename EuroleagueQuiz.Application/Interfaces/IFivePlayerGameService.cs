using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IFivePlayerGameService
{
    Task<FivePlayerGameRoundDto> StartAsync(CancellationToken cancellationToken);
    Task<FivePlayerGameRoundDto> GuessAsync(FivePlayerGameGuessRequest request, CancellationToken cancellationToken);
    Task<FivePlayerGameRoundDto> NextAsync(Guid roundId, CancellationToken cancellationToken);
}
