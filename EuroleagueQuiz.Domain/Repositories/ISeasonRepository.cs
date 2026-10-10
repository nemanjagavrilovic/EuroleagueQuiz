using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface ISeasonRepository
{
    Task<Season?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<Season?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken);
    Task AddAsync(Season season, CancellationToken cancellationToken);
}
