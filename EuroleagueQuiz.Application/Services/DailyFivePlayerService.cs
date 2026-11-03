using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class DailyFivePlayerService(IDailyGameRepository repository, IFivePlayerGameService games, TimeProvider clock) : IDailyFivePlayerService
{
    public const string Game = "five-player";
    private const int TotalRounds = 5;
    private const int SecondsPerRound = 24;
    public Task<DailyFivePlayerDto> StartAsync(DailyAttemptIdentity identity, CancellationToken ct) => ExecuteAsync(identity, null, false, ct);
    public Task<DailyFivePlayerDto> GuessAsync(DailyAttemptIdentity identity, DailyFivePlayerRequest request, CancellationToken ct) => ExecuteAsync(identity, request, false, ct);
    public Task<DailyFivePlayerDto> NextAsync(DailyAttemptIdentity identity, DailyFivePlayerRequest request, CancellationToken ct) => ExecuteAsync(identity, request, true, ct);

    private Task<DailyFivePlayerDto> ExecuteAsync(DailyAttemptIdentity identity, DailyFivePlayerRequest? request, bool next, CancellationToken ct)
    {
        if (identity.GuestId == Guid.Empty) throw new ArgumentException("A guest identifier is required.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return repository.WithLockAsync($"daily-attempt:{Game}:{date:yyyy-MM-dd}:{identity.AttemptId}", async () =>
        {
            var attempt = await repository.FindOrClaimAsync(Game, identity, date, ct);
            if (attempt is null)
            {
                if (request is not null) throw new ArgumentException("Start today's Starting Five challenge first.");
                var challenge = await repository.GetOrCreateChallengeAsync(Game, date, async () =>
                {
                    var definition = await games.CreateChallengeAsync(ct);
                    if (definition.Clubs.Count != TotalRounds || definition.Clubs.Select(x => x.Team.Id).Distinct().Count() != TotalRounds ||
                        definition.Clubs.Any(x => x.Players.Select(p => p.Id).Distinct().Count() < 5 || x.Players.Count != x.Players.Select(p => p.Id).Distinct().Count()))
                        throw new InvalidOperationException("Starting Five needs five distinct clubs with at least five historical players each.");
                    return JsonSerializer.Serialize(definition);
                }, ct);
                attempt = new() { Game = Game, Date = date, ChallengeId = challenge.Id, Challenge = challenge,
                    StateJson = JsonSerializer.Serialize(new AttemptState { Rounds = [NewRound()] }) };
                identity.Initialize(attempt);
                repository.Add(attempt);
            }
            var definition = JsonSerializer.Deserialize<FivePlayerChallengeDto>(attempt.Challenge.DefinitionJson)!;
            var state = JsonSerializer.Deserialize<AttemptState>(attempt.StateJson)!;
            var round = state.Rounds[^1];
            var club = definition.Clubs[state.Rounds.Count - 1];
            var now = clock.GetUtcNow().UtcDateTime;
            if (!round.Completed && now >= round.DeadlineUtc)
            {
                CompleteRound(round, club);
                state.Feedback = "Time's up. The remaining players are revealed.";
            }
            if (request is not null)
            {
                if (request.ChallengeId != attempt.ChallengeId) throw new ArgumentException("This challenge has changed. Refresh to load today's game.");
                if (request.CommandId == Guid.Empty) throw new ArgumentException("A command ID is required.");
                if (!state.Commands.Contains(request.CommandId))
                {
                    if (request.RoundNumber != state.Rounds.Count) throw new ArgumentException("This round has changed. Refresh to restore your progress.");
                    if (next)
                    {
                        if (!round.Completed) throw new ArgumentException("Finish the current round first.");
                        if (state.Rounds.Count < TotalRounds)
                        {
                            state.Rounds.Add(NewRound());
                            round = state.Rounds[^1]; club = definition.Clubs[state.Rounds.Count - 1];
                            state.Feedback = "Name five players for this club.";
                        }
                    }
                    else if (!round.Completed)
                    {
                        round.LastGuessCorrect = null;
                        if (request.Skip)
                        {
                            CompleteRound(round, club);
                            state.Feedback = "Round revealed. Your earned points are kept.";
                        }
                        else
                        {
                            if (request.PlayerId is null or <= 0) throw new ArgumentException("Select a player or reveal the round.");
                            var player = club.Players.FirstOrDefault(x => x.Id == request.PlayerId);
                            if (player is not null && !round.GuessedPlayerIds.Contains(player.Id))
                            {
                                round.GuessedPlayerIds.Add(player.Id); round.LastGuessCorrect = true;
                                state.Feedback = "Correct player!";
                                if (round.GuessedPlayerIds.Count == 5) round.Completed = true;
                            }
                            else
                            {
                                round.LastGuessCorrect = false;
                                state.Feedback = "This player did not play for this club, or was already guessed.";
                            }
                        }
                    }
                    state.Commands.Add(request.CommandId);
                }
            }
            attempt.StateJson = JsonSerializer.Serialize(state);
            attempt.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveAsync(ct);
            now = clock.GetUtcNow().UtcDateTime;
            var correctGuesses = state.Rounds.Sum(x => x.GuessedPlayerIds.Count);
            var playersById = club.Players.ToDictionary(x => x.Id);
            var remaining = round.Completed ? 0 : Math.Clamp((int)Math.Ceiling((round.DeadlineUtc - now).TotalSeconds), 0, SecondsPerRound);
            var dto = new FivePlayerGameRoundDto(attempt.Id, state.Rounds.Count, TotalRounds, SecondsPerRound, remaining, club.Team,
                round.GuessedPlayerIds.Concat(round.RevealedPlayerIds).Select(id => playersById[id]).ToList(), round.Completed,
                round.Completed ? round.GuessedPlayerIds.Count == 5 : null, round.Completed && state.Rounds.Count == TotalRounds, correctGuesses, round.LastGuessCorrect);
            return new DailyFivePlayerDto(attempt.ChallengeId, date, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), dto,
                correctGuesses * 4, correctGuesses, attempt.LeaderboardEligible, now, round.Completed ? null : round.DeadlineUtc, round.RevealedPlayerIds, state.Feedback);
        }, ct);
    }

    private RoundState NewRound() => new() { DeadlineUtc = clock.GetUtcNow().UtcDateTime.AddSeconds(SecondsPerRound) };
    private static void CompleteRound(RoundState round, FivePlayerClubChallengeDto club)
    {
        round.RevealedPlayerIds = club.Players.Where(x => !round.GuessedPlayerIds.Contains(x.Id)).Take(5 - round.GuessedPlayerIds.Count).Select(x => x.Id).ToList();
        round.LastGuessCorrect = null;
        round.Completed = true;
    }
    public sealed class AttemptState
    {
        public List<RoundState> Rounds { get; set; } = [];
        public HashSet<Guid> Commands { get; set; } = [];
        public string? Feedback { get; set; }
    }
    public sealed class RoundState
    {
        public DateTime DeadlineUtc { get; set; }
        public List<long> GuessedPlayerIds { get; set; } = [];
        public List<long> RevealedPlayerIds { get; set; } = [];
        public bool Completed { get; set; }
        public bool? LastGuessCorrect { get; set; }
    }
}
