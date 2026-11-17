using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class GridService : IGridService
{
    private readonly ITeamRepository _teams;
    private readonly IPlayerTeamSeasonRepository _relationships;
    private static readonly SemaphoreSlim CatalogLock = new(1, 1);
    private static GridCatalog? Catalog;
    private static readonly TimeSpan CatalogLifetime = TimeSpan.FromMinutes(10);

    public GridService(ITeamRepository teams, IPlayerTeamSeasonRepository relationships)
    {
        _teams = teams;
        _relationships = relationships;
    }

    public async Task<GridDefinitionDto> CreateGridAsync(
        CancellationToken cancellationToken,
        bool forceNew = false,
        GridDifficulty difficulty = GridDifficulty.Normal,
        IReadOnlyCollection<GridDefinitionDto>? excludedGrids = null)
    {
        var catalog = await GetCatalogAsync(cancellationToken);
        if (catalog.Teams.Count < 6)
            throw new InvalidOperationException("At least six clubs with player data are required to create a grid.");

        var excluded = excludedGrids?.Select(grid => LayoutKey(
            grid.RowTeams.Select(team => team.Id), grid.ColumnTeams.Select(team => team.Id))).ToHashSet() ?? [];
        GridCandidate? best = null;
        if (difficulty == GridDifficulty.Normal)
        {
            var establishedClubs = catalog.Teams.Where(team => catalog.ClubProfiles[team.Id].IsFamiliar).ToArray();
            if (establishedClubs.Length >= 6 && establishedClubs.Length < catalog.Teams.Count)
                best = FindGrid(catalog, establishedClubs, difficulty, excluded, cancellationToken);
        }
        best ??= FindGrid(catalog, catalog.Teams, difficulty, excluded, cancellationToken);

        if (best is null)
            throw new InvalidOperationException("Could not create a complete grid for this difficulty. Import more player and club data first.");

        return new GridDefinitionDto(
            best.Rows.Select(TeamDto.FromEntity).ToList(),
            best.Columns.Select(TeamDto.FromEntity).ToList(),
            difficulty.ToString());
    }

    private static GridCandidate? FindGrid(GridCatalog catalog, IReadOnlyList<Team> teams,
        GridDifficulty difficulty, HashSet<string> excluded, CancellationToken cancellationToken)
    {
        var random = Random.Shared;
        GridCandidate? best = null;
        var rowPool = teams.ToArray();
        var columnPool = new List<Team>(teams.Count);
        var counts = new int[9];
        var fameScores = new double[9];
        for (var attempt = 0; attempt < 10_000; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var i = 0; i < 3; i++)
            {
                var other = random.Next(i, rowPool.Length);
                (rowPool[i], rowPool[other]) = (rowPool[other], rowPool[i]);
            }
            var first = rowPool[0];
            var second = rowPool[1];
            var third = rowPool[2];
            columnPool.Clear();
            foreach (var team in teams)
            {
                if (catalog.Pairs.ContainsKey(ClubPair(first.Id, team.Id)) &&
                    catalog.Pairs.ContainsKey(ClubPair(second.Id, team.Id)) &&
                    catalog.Pairs.ContainsKey(ClubPair(third.Id, team.Id)))
                    columnPool.Add(team);
            }
            if (columnPool.Count < 3) continue;

            for (var i = 0; i < 3; i++)
            {
                var other = random.Next(i, columnPool.Count);
                (columnPool[i], columnPool[other]) = (columnPool[other], columnPool[i]);
            }
            var rows = rowPool[..3];
            var columns = columnPool.GetRange(0, 3);
            if (excluded.Count > 0 && excluded.Contains(LayoutKey(rows.Select(x => x.Id), columns.Select(x => x.Id))))
                continue;

            for (var row = 0; row < 3; row++)
            for (var column = 0; column < 3; column++)
            {
                var pair = catalog.Pairs[ClubPair(rows[row].Id, columns[column].Id)];
                counts[row * 3 + column] = pair.PlayerCount;
                fameScores[row * 3 + column] = pair.BestVisibility;
            }

            var score = Score(difficulty, counts, fameScores);
            var unfamiliarClubs = 0;
            if (difficulty == GridDifficulty.Normal)
            {
                var profiles = rows.Concat(columns).Select(x => catalog.ClubProfiles[x.Id]).ToArray();
                unfamiliarClubs = profiles.Count(x => !x.IsFamiliar);
                score -= profiles.Average(x => x.Visibility) * 0.25;
            }
            if (best is null || unfamiliarClubs < best.UnfamiliarClubs ||
                (unfamiliarClubs == best.UnfamiliarClubs && score < best.Score))
                best = new GridCandidate(rows, columns, score, unfamiliarClubs);
        }

        return best;
    }

    private static (long, long) ClubPair(long first, long second) => first < second ? (first, second) : (second, first);

    private static string LayoutKey(IEnumerable<long> rowIds, IEnumerable<long> columnIds)
    {
        var rows = rowIds.Order().ToArray();
        var columns = columnIds.Order().ToArray();
        // Reordering or swapping the axes creates the same nine intersections.
        if (rows[0] > columns[0]) (rows, columns) = (columns, rows);
        return string.Join(",", rows) + "|" + string.Join(",", columns);
    }

    private async Task<GridCatalog> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (Catalog is not null && DateTime.UtcNow - Catalog.CreatedAtUtc < CatalogLifetime)
            return Catalog;

        await CatalogLock.WaitAsync(cancellationToken);
        try
        {
            if (Catalog is not null && DateTime.UtcNow - Catalog.CreatedAtUtc < CatalogLifetime)
                return Catalog;

            var teams = (await _teams.GetAllAsync(cancellationToken)).ToList();
            var relationships = await _relationships.GetAllAsync(cancellationToken);
            var rosters = relationships.GroupBy(x => x.TeamId).ToDictionary(x => x.Key, x => x.ToList());
            var candidates = teams.Where(team => rosters.ContainsKey(team.Id)).ToArray();
            var seasons = relationships.Select(x => x.Season.StartYear).Distinct().OrderDescending().Take(4).ToHashSet();
            var clubProfiles = rosters.ToDictionary(x => x.Key, x =>
            {
                var clubSeasons = x.Value.Select(relationship => relationship.Season.StartYear).Distinct().ToArray();
                return new ClubProfile(clubSeasons.Length, clubSeasons.Count(seasons.Contains));
            });
            var pairs = new Dictionary<(long, long), ClubIntersection>();
            foreach (var career in relationships.GroupBy(x => x.PlayerId))
            {
                var clubs = career.Select(x => x.TeamId).Distinct().Order().ToArray();
                var visibility = clubs.Length * 3 + career.Select(x => x.SeasonId).Distinct().Count();
                for (var first = 0; first < clubs.Length; first++)
                for (var second = first + 1; second < clubs.Length; second++)
                {
                    var key = (clubs[first], clubs[second]);
                    var pair = pairs.GetValueOrDefault(key);
                    pairs[key] = new ClubIntersection(pair.PlayerCount + 1, Math.Max(pair.BestVisibility, visibility));
                }
            }
            Catalog = new GridCatalog(candidates, clubProfiles, pairs, DateTime.UtcNow);
            return Catalog;
        }
        finally
        {
            CatalogLock.Release();
        }
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

    private static double Score(GridDifficulty difficulty, IReadOnlyList<int> counts, IReadOnlyList<double> fameScores)
    {
        var average = counts.Average();
        var uniqueCells = counts.Count(count => count == 1);
        var averageVisibility = fameScores.Average();

        return difficulty switch
        {
            GridDifficulty.Easy => -average * 5 - averageVisibility * 2 + uniqueCells * 20,
            GridDifficulty.Normal => Math.Abs(average - 7) * 2 - averageVisibility * 0.5
                + counts.Sum(count => Math.Max(0, 3 - count)) * 8,
            GridDifficulty.Hard => Math.Abs(average - 2.5) * 5 + averageVisibility * 0.5,
            GridDifficulty.Legend => counts.Sum(count => Math.Max(0, count - 1)) * 8 + averageVisibility,
            _ => average
        };
    }

    private sealed record GridCandidate(IReadOnlyList<Team> Rows, IReadOnlyList<Team> Columns, double Score, int UnfamiliarClubs);
    private readonly record struct ClubProfile(int Seasons, int RecentSeasons)
    {
        public bool IsFamiliar => Seasons >= 5 || RecentSeasons >= 2;
        public int Visibility => Math.Min(Seasons, 10) + RecentSeasons * 2;
    }
    private readonly record struct ClubIntersection(int PlayerCount, int BestVisibility);
    private sealed record GridCatalog(
        IReadOnlyList<Team> Teams,
        IReadOnlyDictionary<long, ClubProfile> ClubProfiles,
        IReadOnlyDictionary<(long, long), ClubIntersection> Pairs,
        DateTime CreatedAtUtc);
    private sealed record RevealCandidateCell(int Row, int Column, IReadOnlyList<Domain.Entities.Player> Players)
    {
        public int Index => Row * 3 + Column;
    }
}
