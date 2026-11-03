using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyMysteryPlayerService
{
    Task<DailyMysteryPlayerDto> StartAsync(Guid guestId, CancellationToken cancellationToken);
    Task<DailyMysteryPlayerDto> GuessAsync(Guid guestId, DailyMysteryPlayerGuessRequest request, CancellationToken cancellationToken);
}
