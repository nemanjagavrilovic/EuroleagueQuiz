using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ICareerGamesService
{
    Task<MysteryPlayerChallengeDto> CreateMysteryPlayerChallengeAsync(CancellationToken cancellationToken);
    Task<CareerOrderChallengeDto> CreateCareerOrderChallengeAsync(CancellationToken cancellationToken);
    Task<MissingClubChallengeDto> CreateMissingClubChallengeAsync(CancellationToken cancellationToken);
    Task<MissingClubRoundDto> StartMissingClubAsync(CancellationToken cancellationToken);
    Task<MissingClubRoundDto> GuessMissingClubAsync(MissingClubGuessRequest request, CancellationToken cancellationToken);
    Task<CareerOrderRoundDto> StartCareerOrderAsync(CancellationToken cancellationToken);
    Task<CareerOrderRoundDto> GuessCareerOrderAsync(CareerOrderGuessRequest request, CancellationToken cancellationToken);
    Task<MysteryPlayerRoundDto> StartMysteryPlayerAsync(CancellationToken cancellationToken);
    Task<MysteryPlayerRoundDto> GuessMysteryPlayerAsync(MysteryPlayerGuessRequest request, CancellationToken cancellationToken);
}
