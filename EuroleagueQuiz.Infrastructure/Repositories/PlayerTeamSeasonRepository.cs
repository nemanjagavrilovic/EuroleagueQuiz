using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EuroleagueQuiz.Infrastructure.Repositories;

public sealed class PlayerTeamSeasonRepository : IPlayerTeamSeasonRepository
{
    private readonly EuroleagueDbContext _db;
    public PlayerTeamSeasonRepository(EuroleagueDbContext db) => _db = db;

    public async Task<IReadOnlyList<PlayerTeamSeason>> GetForPlayerAsync(long playerId, CancellationToken cancellationToken) =>
        await _db.PlayerTeamSeasons.AsNoTracking().Include(x => x.Team).Include(x => x.Season).Where(x => x.PlayerId == playerId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerTeamSeason>> GetAllAsync(CancellationToken cancellationToken) =>
        await _db.PlayerTeamSeasons.AsNoTracking().Include(x => x.Player).Include(x => x.Team).Include(x => x.Season).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<PlayerTeamSeason>> GetPlayersForTeamsAsync(long firstTeamId, long secondTeamId, CancellationToken cancellationToken)
    {
        var first = _db.PlayerTeamSeasons.Where(x => x.TeamId == firstTeamId).Select(x => x.PlayerId);
        var second = _db.PlayerTeamSeasons.Where(x => x.TeamId == secondTeamId).Select(x => x.PlayerId);

        return await _db.PlayerTeamSeasons.AsNoTracking().Include(x => x.Player)
            .Where(x => first.Contains(x.PlayerId) && second.Contains(x.PlayerId))
            .ToListAsync(cancellationToken);
    }

    public Task<PlayerTeamSeason?> FindAsync(long playerId, long teamId, long seasonId, CancellationToken cancellationToken) =>
        _db.PlayerTeamSeasons.FirstOrDefaultAsync(x => x.PlayerId == playerId && x.TeamId == teamId && x.SeasonId == seasonId, cancellationToken);

    public Task AddAsync(PlayerTeamSeason relationship, CancellationToken cancellationToken) => _db.PlayerTeamSeasons.AddAsync(relationship, cancellationToken).AsTask();
}
