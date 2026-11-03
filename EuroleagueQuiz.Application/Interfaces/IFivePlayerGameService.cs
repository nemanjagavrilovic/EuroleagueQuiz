using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IFivePlayerGameService
{
    Task<FivePlayerChallengeDto> CreateChallengeAsync(CancellationToken cancellationToken);
    Task<FivePlayerGameRoundDto> StartAsync(CancellationToken cancellationToken);
    Task<FivePlayerGameRoundDto> GuessAsync(FivePlayerGameGuessRequest request, CancellationToken cancellationToken);
    Task<FivePlayerGameRoundDto> NextAsync(Guid roundId, CancellationToken cancellationToken);
}
