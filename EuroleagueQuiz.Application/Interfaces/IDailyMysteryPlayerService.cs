using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyMysteryPlayerService
{
    Task<DailyMysteryPlayerDto> StartAsync(DailyAttemptIdentity identity, CancellationToken cancellationToken);
    Task<DailyMysteryPlayerDto> GuessAsync(DailyAttemptIdentity identity, DailyMysteryPlayerGuessRequest request, CancellationToken cancellationToken);
}
