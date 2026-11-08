using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ILeaderboardService
{
    Task<LeaderboardDto> GetAsync(string period, string game, int page, int pageSize, Guid? currentUserId,
        CancellationToken cancellationToken);
}
