using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class GridService : IGridService
{
    private readonly ITeamRepository _teams;
    private readonly IPlayerTeamSeasonRepository _relationships;

    public GridService(ITeamRepository teams, IPlayerTeamSeasonRepository relationships)
    {
        _teams = teams;
        _relationships = relationships;
    }

    public async Task<GridDefinitionDto> CreateGridAsync(
        CancellationToken cancellationToken,
        bool forceNew = false,
        GridDifficulty difficulty = GridDifficulty.Normal)
    {
        var teams = (await _teams.GetAllAsync(cancellationToken)).ToList();
        var relationships = await _relationships.GetAllAsync(cancellationToken);
        var playersByTeam = relationships
            .GroupBy(x => x.TeamId)
            .ToDictionary(x => x.Key, x => x.Select(relationship => relationship.PlayerId).ToHashSet());

        var playerProfiles = relationships
            .GroupBy(x => x.PlayerId)
            .ToDictionary(
                x => x.Key,
                x => (Teams: x.Select(relationship => relationship.TeamId).Distinct().Count(),
                      Seasons: x.Select(relationship => relationship.SeasonId).Distinct().Count()));

        var candidates = teams.Where(team => playersByTeam.ContainsKey(team.Id)).ToList();
        if (candidates.Count < 6)
            throw new InvalidOperationException("At least six clubs with player data are required to create a grid.");

        // A fresh grid is generated for every request. Daily seeding is intentionally disabled.
        var random = Random.Shared;
        GridCandidate? best = null;

        for (var attempt = 0; attempt < 10000; attempt++)
        {
            var rows = candidates.OrderBy(_ => random.Next()).Take(3).ToList();
            var columns = candidates
                .Where(column => !rows.Contains(column) && rows.All(row => HasCommonPlayer(playersByTeam, row.Id, column.Id)))
                .OrderBy(_ => random.Next())
                .Take(3)
                .ToList();

            if (columns.Count != 3) continue;

            var counts = new List<int>(9);
            var fameScores = new List<double>(9);
            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            {
                var commonPlayers = playersByTeam[rows[row].Id].Intersect(playersByTeam[columns[column].Id]).ToList();
                counts.Add(commonPlayers.Count);
                fameScores.Add(commonPlayers.Count == 0
                    ? 0
                    : commonPlayers.Max(playerId => PlayerVisibilityScore(playerProfiles[playerId])));
            }

            var score = Score(difficulty, counts, fameScores);
            if (best is null || score < best.Score)
                best = new GridCandidate(rows, columns, score);
        }

        if (best is null)
            throw new InvalidOperationException("Could not create a complete grid for this difficulty. Import more player and club data first.");

        return new GridDefinitionDto(
            best.Rows.Select(TeamDto.FromEntity).ToList(),
            best.Columns.Select(TeamDto.FromEntity).ToList(),
            difficulty.ToString());
    }

    public async Task<GridRevealResponse> RevealAsync(GridRevealRequest request, CancellationToken cancellationToken)
    {
        if (request.RowTeamIds.Count != 3 || request.ColumnTeamIds.Count != 3)
            throw new ArgumentException("A grid must contain three row teams and three column teams.");

        var candidateCells = new List<RevealCandidateCell>();
        for (var row = 0; row < 3; row++)
        for (var column = 0; column < 3; column++)
        {
            var relationships = await _relationships.GetPlayersForTeamsAsync(request.RowTeamIds[row], request.ColumnTeamIds[column], cancellationToken);
            var players = relationships.Select(x => x.Player).DistinctBy(x => x.Id).OrderBy(x => x.DisplayName).ToList();
            candidateCells.Add(new RevealCandidateCell(row, column, players));
        }

        var assignments = new Dictionary<int, Player>();
        var playerAssignments = new Dictionary<long, int>();
        foreach (var cell in candidateCells.OrderBy(x => x.Players.Count))
            TryAssignPlayer(cell.Index, candidateCells, assignments, playerAssignments, new HashSet<long>());

        var cells = candidateCells.Select(cell =>
        {
            assignments.TryGetValue(cell.Index, out var player);
            return new GridRevealCellDto(cell.Row, cell.Column, player is null ? null : PlayerDto.FromEntity(player), cell.Players.Count);
        }).ToList();
        return new GridRevealResponse(cells);
    }

    private static bool TryAssignPlayer(
        int cellIndex,
        IReadOnlyList<RevealCandidateCell> cells,
        IDictionary<int, Player> assignments,
        IDictionary<long, int> playerAssignments,
        ISet<long> visitedPlayers)
    {
        var cell = cells[cellIndex];
        foreach (var player in cell.Players)
        {
            if (!visitedPlayers.Add(player.Id)) continue;

            if (!playerAssignments.TryGetValue(player.Id, out var previousCell) ||
                TryAssignPlayer(previousCell, cells, assignments, playerAssignments, visitedPlayers))
            {
                assignments[cellIndex] = player;
                playerAssignments[player.Id] = cellIndex;
                return true;
            }
        }

        return false;
    }

    private static bool HasCommonPlayer(IReadOnlyDictionary<long, HashSet<long>> playersByTeam, long firstTeamId, long secondTeamId) =>
        playersByTeam[firstTeamId].Overlaps(playersByTeam[secondTeamId]);

    private static double PlayerVisibilityScore((int Teams, int Seasons) profile) =>
        profile.Teams * 3 + profile.Seasons;

    private static double Score(GridDifficulty difficulty, IReadOnlyList<int> counts, IReadOnlyList<double> fameScores)
    {
        var average = counts.Average();
        var maximum = counts.Max();
        var uniqueCells = counts.Count(count => count == 1);
        var averageVisibility = fameScores.Average();

        return difficulty switch
        {
            GridDifficulty.Easy => -average * 5 - averageVisibility * 2 + uniqueCells * 20,
            GridDifficulty.Normal => Math.Abs(average - 5) * 3 + Math.Abs(maximum - 10) * 0.25,
            GridDifficulty.Hard => Math.Abs(average - 2.5) * 5 + averageVisibility * 0.5,
            GridDifficulty.Legend => counts.Sum(count => Math.Max(0, count - 1)) * 8 + averageVisibility,
            _ => average
        };
    }

    private sealed record GridCandidate(IReadOnlyList<Domain.Entities.Team> Rows, IReadOnlyList<Domain.Entities.Team> Columns, double Score);
    private sealed record RevealCandidateCell(int Row, int Column, IReadOnlyList<Domain.Entities.Player> Players)
    {
        public int Index => Row * 3 + Column;
    }
}
