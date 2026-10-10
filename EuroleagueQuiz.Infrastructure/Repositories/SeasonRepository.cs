using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class SeasonRepository : ISeasonRepository
{
    private readonly EuroleagueDbContext _db;
    public SeasonRepository(EuroleagueDbContext db) => _db = db;

    public Task<Season?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        _db.Seasons.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Season?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken) =>
        _db.Seasons.FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);

    public async Task<IReadOnlyList<Season>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.Seasons.AsNoTracking().OrderByDescending(x => x.StartYear).ToListAsync(cancellationToken);

    public Task AddAsync(Season season, CancellationToken cancellationToken) => _db.Seasons.AddAsync(season, cancellationToken).AsTask();
}
