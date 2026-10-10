using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class TeamService : ITeamService
{
    private readonly ITeamRepository _teams;

    public TeamService(ITeamRepository teams) => _teams = teams;

    public async Task<IReadOnlyList<TeamDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var teams = await _teams.GetAllAsync(cancellationToken);
        return teams.Select(TeamDto.FromEntity).ToList();
    }

    public async Task<TeamDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var team = await _teams.GetByIdAsync(id, cancellationToken);
        return team is null ? null : TeamDto.FromEntity(team);
    }
}
