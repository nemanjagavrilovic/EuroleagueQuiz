using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ITeamService
{
    Task<IReadOnlyList<TeamDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<TeamDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
