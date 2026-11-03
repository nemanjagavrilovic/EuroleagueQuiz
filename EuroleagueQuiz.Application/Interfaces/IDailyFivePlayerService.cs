using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyFivePlayerService
{
    Task<DailyFivePlayerDto> StartAsync(DailyAttemptIdentity identity, CancellationToken cancellationToken);
    Task<DailyFivePlayerDto> GuessAsync(DailyAttemptIdentity identity, DailyFivePlayerRequest request, CancellationToken cancellationToken);
    Task<DailyFivePlayerDto> NextAsync(DailyAttemptIdentity identity, DailyFivePlayerRequest request, CancellationToken cancellationToken);
}
