using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly EuroleagueDbContext _db;
    public PlayerRepository(EuroleagueDbContext db) => _db = db;

    public Task<Player?> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        _db.Players.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Player?> GetByExternalIdAsync(string externalId, CancellationToken cancellationToken) =>
        _db.Players.FirstOrDefaultAsync(x => x.ExternalId == externalId, cancellationToken);

    public async Task<IReadOnlyList<Player>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.Players.AsNoTracking().OrderBy(x => x.DisplayName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Player>> SearchAsync(string term, int limit, CancellationToken cancellationToken)
    {
        var terms = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var query = _db.Players.AsNoTracking();

        foreach (var searchTerm in terms)
        {
            var pattern = $"%{searchTerm}%";
            query = query.Where(x =>
                (x.FirstName != null && EF.Functions.Like(EF.Functions.Collate(x.FirstName, "Latin1_General_100_CI_AI"), pattern)) ||
                (x.LastName != null && EF.Functions.Like(EF.Functions.Collate(x.LastName, "Latin1_General_100_CI_AI"), pattern)) ||
                EF.Functions.Like(EF.Functions.Collate(x.DisplayName, "Latin1_General_100_CI_AI"), pattern));
        }

        return await query
            .OrderBy(x => x.DisplayName)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(Player player, CancellationToken cancellationToken) => _db.Players.AddAsync(player, cancellationToken).AsTask();
}
