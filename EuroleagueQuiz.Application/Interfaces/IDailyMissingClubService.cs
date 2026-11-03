using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyMissingClubService
{
    Task<DailyMissingClubDto> StartAsync(DailyAttemptIdentity identity, CancellationToken cancellationToken);
    Task<DailyMissingClubDto> GuessAsync(DailyAttemptIdentity identity, DailyMissingClubGuessRequest request, CancellationToken cancellationToken);
}
