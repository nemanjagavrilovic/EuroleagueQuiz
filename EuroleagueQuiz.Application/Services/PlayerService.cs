using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _players;
    private readonly IPlayerTeamSeasonRepository _relationships;

    public PlayerService(IPlayerRepository players, IPlayerTeamSeasonRepository relationships)
    {
        _players = players;
        _relationships = relationships;
    }

    public async Task<IReadOnlyList<PlayerDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        var players = await _players.GetAllAsync(cancellationToken);
        return players.Select(PlayerDto.FromEntity).ToList();
    }

    public async Task<PlayerDto?> GetByIdAsync(long id, CancellationToken cancellationToken)
    {
        var player = await _players.GetByIdAsync(id, cancellationToken);
        return player is null ? null : PlayerDto.FromEntity(player);
    }

    public async Task<IReadOnlyList<PlayerDto>> GetCommonPlayersAsync(long firstTeamId, long secondTeamId, CancellationToken cancellationToken)
    {
        var relationships = await _relationships.GetPlayersForTeamsAsync(firstTeamId, secondTeamId, cancellationToken);
        return relationships
            .Select(x => x.Player)
            .DistinctBy(x => x.Id)
            .Select(PlayerDto.FromEntity)
            .ToList();
    }

    public async Task<IReadOnlyList<PlayerDto>> SearchAsync(string term, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term)) return Array.Empty<PlayerDto>();
        var players = await _players.SearchAsync(term.Trim(), 10, cancellationToken);
        return players.Select(PlayerDto.FromEntity).ToList();
    }

    public async Task<GridPlayerMatchResponse?> GetGridPlayerMatchAsync(GridPlayerMatchRequest request, CancellationToken cancellationToken)
    {
        if (request.RowTeamIds.Count != 3 || request.ColumnTeamIds.Count != 3 ||
            request.RowTeamIds.Concat(request.ColumnTeamIds).Distinct().Count() != 6)
            throw new ArgumentException("A grid must contain three distinct row teams and three distinct column teams.");

        var player = await _players.GetByIdAsync(request.PlayerId, cancellationToken);
        if (player is null) return null;

        var playerRelationships = await _relationships.GetForPlayerAsync(player.Id, cancellationToken);
        var teamIds = playerRelationships.Select(x => x.TeamId).ToHashSet();
        var cells = new List<GridCellDto>();

        for (var row = 0; row < 3; row++)
        for (var column = 0; column < 3; column++)
        {
            if (teamIds.Contains(request.RowTeamIds[row]) && teamIds.Contains(request.ColumnTeamIds[column]))
            {
                var commonPlayers = await _relationships.GetPlayersForTeamsAsync(request.RowTeamIds[row], request.ColumnTeamIds[column], cancellationToken);
                var playerCount = commonPlayers.Select(x => x.PlayerId).Distinct().Count();
                cells.Add(new GridCellDto(row, column, request.RowTeamIds[row], request.ColumnTeamIds[column], playerCount));
            }
        }

        return new GridPlayerMatchResponse(PlayerDto.FromEntity(player), cells);
    }
}
