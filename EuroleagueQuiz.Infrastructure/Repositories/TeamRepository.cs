using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class TeamRepository : ITeamRepository
{
    private readonly EuroleagueDbContext _db;
    public TeamRepository(EuroleagueDbContext db) => _db = db;

    public Task<Team?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        _db.Teams.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Team?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken) =>
        _db.Teams.FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);

    public async Task<IReadOnlyList<Team>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.Teams.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);

    public Task AddAsync(Team team, CancellationToken cancellationToken) => _db.Teams.AddAsync(team, cancellationToken).AsTask();
}
