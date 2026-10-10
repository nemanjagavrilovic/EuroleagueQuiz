using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Player?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<Player>> SearchAsync(string term, int limit, CancellationToken cancellationToken);
    Task AddAsync(Player player, CancellationToken cancellationToken);
}
