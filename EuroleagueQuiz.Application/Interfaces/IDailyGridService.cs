using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyGridService
{
    Task<DailyGridAttemptDto> StartAsync(DailyAttemptIdentity identity, GridDifficulty difficulty, CancellationToken cancellationToken);
    Task<DailyGridAttemptDto> GuessAsync(DailyAttemptIdentity identity, DailyGridGuessRequest request, CancellationToken cancellationToken);
    Task<DailyGridAttemptDto> PlaceAsync(DailyAttemptIdentity identity, DailyGridPlaceRequest request, CancellationToken cancellationToken);
    Task<DailyGridAttemptDto> RevealAsync(DailyAttemptIdentity identity, DailyGridRevealRequest request, CancellationToken cancellationToken);
}
