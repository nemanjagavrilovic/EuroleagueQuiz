using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyGridService
{
    Task<DailyGridAttemptDto> StartAsync(Guid guestId, GridDifficulty difficulty, CancellationToken cancellationToken);
    Task<DailyGridAttemptDto> GuessAsync(Guid guestId, DailyGridGuessRequest request, CancellationToken cancellationToken);
    Task<DailyGridAttemptDto> PlaceAsync(Guid guestId, DailyGridPlaceRequest request, CancellationToken cancellationToken);
    Task<DailyGridAttemptDto> RevealAsync(Guid guestId, DailyGridRevealRequest request, CancellationToken cancellationToken);
}
