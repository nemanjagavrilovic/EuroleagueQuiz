using System.Collections.Concurrent;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class TeamHistoryGameService : ITeamHistoryGameService
{
    private readonly IPlayerTeamSeasonRepository _relationships;
    private static readonly ConcurrentDictionary<Guid, RoundState> Rounds = new();
    private static readonly SemaphoreSlim CandidateLock = new(1, 1);
    private static IReadOnlyList<Candidate>? CandidateCache;

    public TeamHistoryGameService(IPlayerTeamSeasonRepository relationships) => _relationships = relationships;

    public async Task<TeamHistoryRoundDto> StartAsync(CancellationToken cancellationToken)
    {
        var candidates = await GetCandidatesAsync(cancellationToken);

        if (candidates.Count == 0)
            throw new InvalidOperationException("At least one player with four historical clubs is required.");

        var selected = candidates[Random.Shared.Next(candidates.Count)];
        var teams = selected.Teams.OrderBy(_ => Random.Shared.Next()).Take(4).ToList();
        var round = new RoundState(Guid.NewGuid(), selected.Player.Id, selected.Player, teams);
        Rounds[round.Id] = round;
        return ToDto(round);
    }

    private async Task<IReadOnlyList<Candidate>> GetCandidatesAsync(CancellationToken cancellationToken)
    {
        if (CandidateCache is not null)
            return CandidateCache;

        await CandidateLock.WaitAsync(cancellationToken);
        try
        {
            if (CandidateCache is not null)
                return CandidateCache;

            var relationships = await _relationships.GetAllAsync(cancellationToken);
            CandidateCache = relationships
                .GroupBy(x => x.PlayerId)
                .Select(group => new Candidate(
                    group.Key,
                    PlayerDto.FromEntity(group.First().Player),
                    group.GroupBy(x => x.TeamId).Select(team => TeamDto.FromEntity(team.First().Team)).ToList()))
                .Where(x => x.Teams.Count >= 4)
                .ToList();

            return CandidateCache;
        }
        finally
        {
            CandidateLock.Release();
        }
    }

    public Task<TeamHistoryRoundDto> GuessAsync(TeamHistoryGuessRequest request, CancellationToken cancellationToken)
    {
        if (!Rounds.TryGetValue(request.RoundId, out var round))
            throw new KeyNotFoundException("The game round was not found or has expired.");
        if (round.Completed) return Task.FromResult(ToDto(round));
        if (!request.Skip && request.PlayerId is null)
            throw new ArgumentException("Select a player or skip the round.");

        round.Attempts++;
        if (!request.Skip && request.PlayerId == round.PlayerId)
        {
            round.Completed = true;
            round.Correct = true;
        }
        else if (round.RevealedCount < round.Teams.Count)
        {
            round.RevealedCount++;
        }
        else
        {
            round.Completed = true;
            round.Correct = false;
        }

        return Task.FromResult(ToDto(round));
    }

    private static TeamHistoryRoundDto ToDto(RoundState round) =>
        new(round.Id, round.Teams.Take(round.RevealedCount).ToList(), round.RevealedCount, round.Teams.Count, round.Completed, round.Correct, round.Attempts, round.Completed ? round.Player : null);

    private sealed record Candidate(long PlayerId, PlayerDto Player, IReadOnlyList<TeamDto> Teams);

    private sealed class RoundState
    {
        public RoundState(Guid id, long playerId, PlayerDto player, IReadOnlyList<TeamDto> teams)
        {
            Id = id;
            PlayerId = playerId;
            Player = player;
            Teams = teams;
        }

        public Guid Id { get; }
        public long PlayerId { get; }
        public PlayerDto Player { get; }
        public IReadOnlyList<TeamDto> Teams { get; }
        public int RevealedCount { get; set; } = 1;
        public int Attempts { get; set; }
        public bool Completed { get; set; }
        public bool? Correct { get; set; }
    }
}
