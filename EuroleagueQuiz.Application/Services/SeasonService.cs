using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class SeasonService : ISeasonService
{
    private readonly ISeasonRepository _seasons;

    public SeasonService(ISeasonRepository seasons) => _seasons = seasons;

    public async Task<IReadOnlyList<SeasonDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var seasons = await _seasons.GetAllAsync(cancellationToken);
        return seasons.Select(SeasonDto.FromEntity).ToList();
    }

    public async Task<SeasonDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var season = await _seasons.GetByIdAsync(id, cancellationToken);
        return season is null ? null : SeasonDto.FromEntity(season);
    }
}
