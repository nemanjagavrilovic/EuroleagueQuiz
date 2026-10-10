using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IPlayerService
{
    Task<IReadOnlyList<PlayerDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<PlayerDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerDto>> GetCommonPlayersAsync(long firstTeamId, long secondTeamId, CancellationToken cancellationToken);
    Task<IReadOnlyList<PlayerDto>> SearchAsync(string term, CancellationToken cancellationToken);
    Task<GridPlayerMatchResponse?> GetGridPlayerMatchAsync(GridPlayerMatchRequest request, CancellationToken cancellationToken);
}
