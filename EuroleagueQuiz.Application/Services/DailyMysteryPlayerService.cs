using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class DailyMysteryPlayerService(IDailyGameRepository repository, ICareerGamesService games, IPlayerService players, TimeProvider clock) : IDailyMysteryPlayerService
{
    public const string Game = "mystery-player";
    public Task<DailyMysteryPlayerDto> StartAsync(DailyAttemptIdentity identity, CancellationToken ct) => ExecuteAsync(identity, null, ct);
    public Task<DailyMysteryPlayerDto> GuessAsync(DailyAttemptIdentity identity, DailyMysteryPlayerGuessRequest request, CancellationToken ct) => ExecuteAsync(identity, request, ct);

    private Task<DailyMysteryPlayerDto> ExecuteAsync(DailyAttemptIdentity identity, DailyMysteryPlayerGuessRequest? request, CancellationToken ct)
    {
        if (identity.GuestId == Guid.Empty) throw new ArgumentException("A guest identifier is required.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return repository.WithLockAsync($"daily-attempt:{Game}:{date:yyyy-MM-dd}:{identity.AttemptId}", async () =>
        {
            var attempt = await repository.FindOrClaimAsync(Game, identity, date, ct);
            if (attempt is null)
            {
                if (request is not null) throw new ArgumentException("Start today's Mystery Player challenge first.");
                var challenge = await repository.GetOrCreateChallengeAsync(Game, date, async () =>
                {
                    var definition = await games.CreateMysteryPlayerChallengeAsync(ct);
                    if (definition.Clues.Count != 5 || definition.Clues.Any(string.IsNullOrWhiteSpace) || definition.Clues.Distinct().Count() != 5)
                        throw new InvalidOperationException("Mystery Player needs five distinct clues.");
                    return JsonSerializer.Serialize(definition);
                }, ct);
                attempt = new() { Game = Game, Date = date, Challenge = challenge, ChallengeId = challenge.Id,
                    StateJson = JsonSerializer.Serialize(new AttemptState()) };
                identity.Initialize(attempt);
                repository.Add(attempt);
            }
            var definition = JsonSerializer.Deserialize<MysteryPlayerChallengeDto>(attempt.Challenge.DefinitionJson)!;
            var state = JsonSerializer.Deserialize<AttemptState>(attempt.StateJson)!;
            if (request is not null)
            {
                if (request.ChallengeId != attempt.ChallengeId) throw new ArgumentException("This challenge has changed. Refresh to load today's game.");
                if (request.CommandId == Guid.Empty) throw new ArgumentException("A command ID is required.");
                if (!state.Completed && !state.Commands.Contains(request.CommandId))
                {
                    if (!request.Skip)
                    {
                        if (request.PlayerId is null) throw new ArgumentException("Select a player or reveal another clue.");
                        if (state.UsedPlayerIds.Contains(request.PlayerId.Value)) throw new ArgumentException("This player has already been guessed.");
                        if (request.PlayerId != definition.Player.Id && await players.GetByIdAsync(request.PlayerId.Value, ct) is null)
                            throw new ArgumentException("Player not found.");
                        state.UsedPlayerIds.Add(request.PlayerId.Value);
                    }
                    state.Commands.Add(request.CommandId);
                    if (!request.Skip && request.PlayerId == definition.Player.Id)
                    {
                        state.Completed = true; state.Correct = true; state.Score = Points(state.RevealedCount);
                        state.Feedback = "You solved the mystery.";
                    }
                    else if (state.RevealedCount < 5)
                    {
                        state.RevealedCount++;
                        state.Feedback = request.Skip ? "Another clue was revealed." : "Not this player — another clue was revealed.";
                    }
                    else
                    {
                        state.Completed = true; state.Correct = false; state.Score = 0;
                        state.Feedback = "The mystery player is revealed. Come back for tomorrow's challenge.";
                    }
                }
            }
            var now = clock.GetUtcNow().UtcDateTime;
            attempt.StateJson = JsonSerializer.Serialize(state);
            attempt.Score = state.Score;
            attempt.Completed = state.Completed;
            if (state.Completed && attempt.CompletedAtUtc is null) attempt.CompletedAtUtc = now;
            attempt.UpdatedAtUtc = now;
            await repository.SaveAsync(ct);
            var round = new MysteryPlayerRoundDto(attempt.Id, definition.Clues.Take(state.RevealedCount).ToList(), state.RevealedCount, 5,
                state.Completed, state.Correct, state.Completed ? definition.Player : null);
            return new DailyMysteryPlayerDto(attempt.ChallengeId, date, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), round,
                state.Score, state.Completed ? 0 : Points(state.RevealedCount), attempt.LeaderboardEligible, state.UsedPlayerIds.ToList(), state.Feedback);
        }, ct);
    }

    private static int Points(int revealedCount) => (6 - revealedCount) * 20;
    public sealed class AttemptState
    {
        public int RevealedCount { get; set; } = 1;
        public bool Completed { get; set; }
        public bool? Correct { get; set; }
        public int Score { get; set; }
        public HashSet<long> UsedPlayerIds { get; set; } = [];
        public HashSet<Guid> Commands { get; set; } = [];
        public string? Feedback { get; set; }
    }
}
