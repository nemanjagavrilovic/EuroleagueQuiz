using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface ISeasonService
{
    Task<IReadOnlyList<SeasonDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<SeasonDto?> GetByIdAsync(long id, CancellationToken cancellationToken);
}
