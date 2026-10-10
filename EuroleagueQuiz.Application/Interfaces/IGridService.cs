using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IGridService
{
    Task<GridDefinitionDto> CreateGridAsync(CancellationToken cancellationToken, bool forceNew = false, GridDifficulty difficulty = GridDifficulty.Normal);
    Task<GridRevealResponse> RevealAsync(GridRevealRequest request, CancellationToken cancellationToken);
}
