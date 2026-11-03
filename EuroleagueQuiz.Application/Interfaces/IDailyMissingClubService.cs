using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyMissingClubService
{
    Task<DailyMissingClubDto> StartAsync(Guid guestId, CancellationToken cancellationToken);
    Task<DailyMissingClubDto> GuessAsync(Guid guestId, DailyMissingClubGuessRequest request, CancellationToken cancellationToken);
}
