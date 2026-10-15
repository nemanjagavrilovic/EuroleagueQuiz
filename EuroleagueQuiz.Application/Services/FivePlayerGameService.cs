using System.Collections.Concurrent;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class FivePlayerGameService : IFivePlayerGameService
{
    private const int TotalRounds = 5;
    private const int SecondsPerRound = 24;
    private static readonly ConcurrentDictionary<Guid, GameState> Games = new();
    private readonly IPlayerTeamSeasonRepository _relationships;
    private readonly ITeamRepository _teams;

    public FivePlayerGameService(IPlayerTeamSeasonRepository relationships, ITeamRepository teams)
    {
        _relationships = relationships;
        _teams = teams;
    }

    public async Task<FivePlayerGameRoundDto> StartAsync(CancellationToken cancellationToken)
    {
        var teamIds = await _relationships.GetTeamIdsWithAtLeastPlayersAsync(5, cancellationToken);
        if (teamIds.Count < TotalRounds) throw new InvalidOperationException("Not enough team roster data exists for this game.");

        var selectedIds = teamIds.OrderBy(_ => Random.Shared.Next()).Take(TotalRounds).ToList();
        var teams = new List<TeamDto>();
        foreach (var teamId in selectedIds)
        {
            var team = await _teams.GetByIdAsync(teamId, cancellationToken);
            if (team is not null) teams.Add(TeamDto.FromEntity(team));
        }

        if (teams.Count < TotalRounds) throw new InvalidOperationException("The selected teams could not be loaded.");
        var state = new GameState(Guid.NewGuid(), teams);
        Games[state.Id] = state;
        return ToDto(state);
    }

    public async Task<FivePlayerGameRoundDto> GuessAsync(FivePlayerGameGuessRequest request, CancellationToken cancellationToken)
    {
        if (!Games.TryGetValue(request.RoundId, out var state)) throw new KeyNotFoundException("The game was not found.");
        if (state.GameCompleted) return ToDto(state);

        var round = state.CurrentRound;
        round.LastGuessCorrect = null;

        if (DateTime.UtcNow >= round.DeadlineUtc || request.Skip)
        {
            await CompleteRoundAsync(round, cancellationToken);
            return ToDto(state);
        }

        if (request.PlayerId.HasValue)
        {
            var relationship = await _relationships.FindForPlayerAndTeamAsync(request.PlayerId.Value, round.Team.Id, cancellationToken);
            if (relationship is not null && round.GuessedPlayerIds.Add(relationship.PlayerId))
            {
                round.Players.Add(PlayerDto.FromEntity(relationship.Player));
                round.LastGuessCorrect = true;
                state.Score++;
                if (round.Players.Count >= 5) round.Completed = true;
            }
            else
            {
                round.LastGuessCorrect = false;
            }
        }

        return ToDto(state);
    }

    public Task<FivePlayerGameRoundDto> NextAsync(Guid roundId, CancellationToken cancellationToken)
    {
        if (!Games.TryGetValue(roundId, out var state)) throw new KeyNotFoundException("The game was not found.");
        if (!state.CurrentRound.Completed) throw new InvalidOperationException("Finish the current round first.");
        if (!state.GameCompleted) state.StartNextRound();
        return Task.FromResult(ToDto(state));
    }

    private async Task CompleteRoundAsync(RoundState round, CancellationToken cancellationToken)
    {
        if (round.Completed) return;
        var relationships = await _relationships.GetForTeamAsync(round.Team.Id, cancellationToken);
        foreach (var relationship in relationships.GroupBy(x => x.PlayerId).Select(x => x.OrderByDescending(y => y.Season.StartYear).First()))
        {
            if (!round.GuessedPlayerIds.Add(relationship.PlayerId)) continue;
            round.Players.Add(PlayerDto.FromEntity(relationship.Player));
        }
        round.Completed = true;
    }

    private static FivePlayerGameRoundDto ToDto(GameState state)
    {
        var round = state.CurrentRound;
        var remaining = round.Completed ? 0 : Math.Max(0, (int)Math.Ceiling((round.DeadlineUtc - DateTime.UtcNow).TotalSeconds));
        return new FivePlayerGameRoundDto(state.Id, state.RoundNumber, TotalRounds, SecondsPerRound, remaining, round.Team,
            round.Players, round.Completed, null, state.GameCompleted, state.Score, round.LastGuessCorrect);
    }

    private sealed class GameState(Guid id, IReadOnlyList<TeamDto> teams)
    {
        public Guid Id { get; } = id;
        public IReadOnlyList<TeamDto> Teams { get; } = teams;
        public int RoundNumber { get; private set; } = 1;
        public int Score { get; set; }
        public RoundState CurrentRound { get; private set; } = new(teams[0]);
        public bool GameCompleted => CurrentRound.Completed && RoundNumber == TotalRounds;
        public void StartNextRound() { RoundNumber++; CurrentRound = new RoundState(Teams[RoundNumber - 1]); }
    }

    private sealed class RoundState(TeamDto team)
    {
        public TeamDto Team { get; } = team;
        public DateTime DeadlineUtc { get; } = DateTime.UtcNow.AddSeconds(SecondsPerRound);
        public List<PlayerDto> Players { get; } = [];
        public HashSet<long> GuessedPlayerIds { get; } = [];
        public bool Completed { get; set; }
        public bool? LastGuessCorrect { get; set; }
    }
}
