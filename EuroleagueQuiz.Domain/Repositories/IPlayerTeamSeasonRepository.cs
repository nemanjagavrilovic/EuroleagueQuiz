using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Domain.Repositories;

public interface IPlayerTeamSeasonRepository
{
    Task<IReadOnlyList<PlayerTeamSeason>> GetForPlayerAsync(long playerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerTeamSeason>> GetAllAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<long>> GetTeamIdsWithAtLeastPlayersAsync(int minimumPlayers, CancellationToken cancellationToken);
    Task<PlayerTeamSeason?> FindForPlayerAndTeamAsync(long playerId, long teamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerTeamSeason>> GetForTeamAsync(long teamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerTeamSeason>> GetPlayersForTeamsAsync(long firstTeamId, long secondTeamId, CancellationToken cancellationToken);
    Task<PlayerTeamSeason?> FindAsync(long playerId, long teamId, long seasonId, CancellationToken cancellationToken);
    Task AddAsync(PlayerTeamSeason relationship, CancellationToken cancellationToken);
}
