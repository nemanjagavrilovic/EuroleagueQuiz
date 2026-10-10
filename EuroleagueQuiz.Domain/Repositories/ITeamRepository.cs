using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface ITeamRepository
{
    Task<Team?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Team?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Team team, CancellationToken cancellationToken);
}
