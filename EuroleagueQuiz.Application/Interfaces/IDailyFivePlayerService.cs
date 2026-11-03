using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyFivePlayerService
{
    Task<DailyFivePlayerDto> StartAsync(Guid guestId, CancellationToken cancellationToken);
    Task<DailyFivePlayerDto> GuessAsync(Guid guestId, DailyFivePlayerRequest request, CancellationToken cancellationToken);
    Task<DailyFivePlayerDto> NextAsync(Guid guestId, DailyFivePlayerRequest request, CancellationToken cancellationToken);
}
