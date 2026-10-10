using System.Collections.Concurrent;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class CareerGamesService : ICareerGamesService
{
    private readonly IPlayerTeamSeasonRepository _relationships;
    private static readonly SemaphoreSlim CatalogLock = new(1, 1);
    private static IReadOnlyList<PlayerCareer>? Catalog;
    private static readonly ConcurrentDictionary<Guid, MissingClubState> MissingRounds = new();
    private static readonly ConcurrentDictionary<Guid, CareerOrderState> OrderRounds = new();
    private static readonly ConcurrentDictionary<Guid, MysteryState> MysteryRounds = new();

    public CareerGamesService(IPlayerTeamSeasonRepository relationships) => _relationships = relationships;

    public async Task<MissingClubRoundDto> StartMissingClubAsync(CancellationToken cancellationToken)
    {
        var candidates = (await GetCatalogAsync(cancellationToken)).Where(x => x.Teams.Count >= 4).ToList();
        EnsureCandidates(candidates);
        var career = candidates[Random.Shared.Next(candidates.Count)];
        var selected = career.Teams.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();
        var missing = selected[3].Team;
        var options = candidates.SelectMany(x => x.Teams)
            .Where(x => x.Team.Id != missing.Id && selected.All(s => s.Team.Id != x.Team.Id))
            .DistinctBy(x => x.Team.Id)
            .OrderBy(_ => Random.Shared.Next())
            .Take(3).Select(x => x.Team).Append(missing).OrderBy(_ => Random.Shared.Next()).ToList();
        var state = new MissingClubState(Guid.NewGuid(), career.Player, selected.Take(3).Select(x => x.Team).ToList(), options, missing);
        MissingRounds[state.Id] = state;
        return ToDto(state);
    }

    public Task<MissingClubRoundDto> GuessMissingClubAsync(MissingClubGuessRequest request, CancellationToken cancellationToken)
    {
        if (!MissingRounds.TryGetValue(request.RoundId, out var state)) throw new KeyNotFoundException("The round was not found.");
        if (!state.Completed)
        {
            state.Completed = true;
            state.Correct = !request.Skip && request.TeamId == state.CorrectTeam.Id;
        }
        return Task.FromResult(ToDto(state));
    }

    public async Task<CareerOrderRoundDto> StartCareerOrderAsync(CancellationToken cancellationToken)
    {
        var candidates = (await GetCatalogAsync(cancellationToken)).Where(x => x.Teams.Count >= 4).ToList();
        EnsureCandidates(candidates);
        var career = candidates[Random.Shared.Next(candidates.Count)];
        var selected = career.Teams.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();
        var correct = selected.OrderBy(x => x.StartYear).ThenBy(x => x.EndYear).ToList();
        var state = new CareerOrderState(Guid.NewGuid(), career.Player, selected, correct);
        OrderRounds[state.Id] = state;
        return ToDto(state);
    }

    public Task<CareerOrderRoundDto> GuessCareerOrderAsync(CareerOrderGuessRequest request, CancellationToken cancellationToken)
    {
        if (!OrderRounds.TryGetValue(request.RoundId, out var state)) throw new KeyNotFoundException("The round was not found.");
        if (!state.Completed)
        {
            state.Completed = true;
            state.Correct = request.TeamIds.SequenceEqual(state.CorrectOrder.Select(x => x.Team.Id));
        }
        return Task.FromResult(ToDto(state));
    }

    public async Task<MysteryPlayerRoundDto> StartMysteryPlayerAsync(CancellationToken cancellationToken)
    {
        var candidates = (await GetCatalogAsync(cancellationToken)).Where(x => x.Teams.Count >= 3).ToList();
        EnsureCandidates(candidates);
        var career = candidates[Random.Shared.Next(candidates.Count)];
        var first = career.Teams.OrderBy(x => x.StartYear).First();
        var last = career.Teams.OrderByDescending(x => x.EndYear).First();
        var clues = new[]
        {
            $"Played for {career.Teams.Count} EuroLeague clubs in the database.",
            $"First club in this record: {first.Team.Name}.",
            $"Nationality: {career.Player.Nationality ?? "Unknown"}.",
            $"Career record starts in {career.Teams.Min(x => x.StartYear)}.",
            $"Most recent club in this record: {last.Team.Name}."
        };
        var state = new MysteryState(Guid.NewGuid(), career.Player, clues);
        MysteryRounds[state.Id] = state;
        return ToDto(state);
    }

    public Task<MysteryPlayerRoundDto> GuessMysteryPlayerAsync(MysteryPlayerGuessRequest request, CancellationToken cancellationToken)
    {
        if (!MysteryRounds.TryGetValue(request.RoundId, out var state)) throw new KeyNotFoundException("The round was not found.");
        if (!state.Completed)
        {
            if (!request.Skip && request.PlayerId == state.Player.Id)
            {
                state.Completed = true;
                state.Correct = true;
            }
            else if (state.RevealedClueCount < state.Clues.Count)
            {
                state.RevealedClueCount++;
            }
            else
            {
                state.Completed = true;
                state.Correct = false;
            }
        }
        return Task.FromResult(ToDto(state));
    }

    private async Task<IReadOnlyList<PlayerCareer>> GetCatalogAsync(CancellationToken cancellationToken)
    {
        if (Catalog is not null) return Catalog;
        await CatalogLock.WaitAsync(cancellationToken);
        try
        {
            if (Catalog is not null) return Catalog;
            var relationships = await _relationships.GetAllAsync(cancellationToken);
            Catalog = relationships.GroupBy(x => x.PlayerId).Select(group => new PlayerCareer(
                PlayerDto.FromEntity(group.First().Player),
                group.GroupBy(x => x.TeamId).Select(team => new TeamCareer(
                    TeamDto.FromEntity(team.First().Team),
                    team.Min(x => x.Season.StartYear),
                    team.Max(x => x.Season.EndYear))).ToList())).ToList();
            return Catalog;
        }
        finally { CatalogLock.Release(); }
    }

    private static void EnsureCandidates<T>(IReadOnlyList<T> candidates)
    {
        if (candidates.Count == 0) throw new InvalidOperationException("Not enough player history exists for this game.");
    }

    private static MissingClubRoundDto ToDto(MissingClubState x) => new(x.Id, x.Player, x.KnownTeams, x.Options, x.Completed, x.Correct, x.Completed ? x.CorrectTeam : null);
    private static CareerOrderRoundDto ToDto(CareerOrderState x) => new(x.Id, x.Player, x.Clubs.Select(y => new CareerOrderClubDto(y.Team.Id, y.Team.Name, y.Team.ImageUrl)).ToList(), x.Completed, x.Correct, x.Completed ? x.CorrectOrder.Select(y => new CareerOrderClubDto(y.Team.Id, y.Team.Name, y.Team.ImageUrl)).ToList() : null);
    private static MysteryPlayerRoundDto ToDto(MysteryState x) => new(x.Id, x.Clues.Take(x.RevealedClueCount).ToList(), x.RevealedClueCount, x.Clues.Count, x.Completed, x.Correct, x.Completed ? x.Player : null);

    private sealed record PlayerCareer(PlayerDto Player, IReadOnlyList<TeamCareer> Teams);
    private sealed record TeamCareer(TeamDto Team, int StartYear, int EndYear);
    private sealed class MissingClubState(Guid id, PlayerDto player, IReadOnlyList<TeamDto> knownTeams, IReadOnlyList<TeamDto> options, TeamDto correctTeam)
    {
        public Guid Id { get; } = id; public PlayerDto Player { get; } = player; public IReadOnlyList<TeamDto> KnownTeams { get; } = knownTeams; public IReadOnlyList<TeamDto> Options { get; } = options; public TeamDto CorrectTeam { get; } = correctTeam; public bool Completed { get; set; } public bool? Correct { get; set; }
    }
    private sealed class CareerOrderState(Guid id, PlayerDto player, IReadOnlyList<TeamCareer> clubs, IReadOnlyList<TeamCareer> correctOrder)
    {
        public Guid Id { get; } = id; public PlayerDto Player { get; } = player; public IReadOnlyList<TeamCareer> Clubs { get; } = clubs; public IReadOnlyList<TeamCareer> CorrectOrder { get; } = correctOrder; public bool Completed { get; set; } public bool? Correct { get; set; }
    }
    private sealed class MysteryState(Guid id, PlayerDto player, IReadOnlyList<string> clues)
    {
        public Guid Id { get; } = id; public PlayerDto Player { get; } = player; public IReadOnlyList<string> Clues { get; } = clues; public int RevealedClueCount { get; set; } = 1; public bool Completed { get; set; } public bool? Correct { get; set; }
    }
}
