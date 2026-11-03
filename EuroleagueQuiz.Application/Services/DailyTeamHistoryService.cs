using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class DailyTeamHistoryService(IDailyGameRepository repository, ITeamHistoryGameService games, IPlayerService players, TimeProvider clock) : IDailyTeamHistoryService
{
    public const string Game = "team-history";

    public Task<DailyTeamHistoryDto> StartAsync(Guid guestId, CancellationToken ct) => ExecuteAsync(guestId, null, ct);
    public Task<DailyTeamHistoryDto> GuessAsync(Guid guestId, DailyTeamHistoryGuessRequest request, CancellationToken ct) => ExecuteAsync(guestId, request, ct);

    private Task<DailyTeamHistoryDto> ExecuteAsync(Guid guestId, DailyTeamHistoryGuessRequest? request, CancellationToken ct)
    {
        if (guestId == Guid.Empty) throw new ArgumentException("A guest identifier is required.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return repository.WithLockAsync($"daily-attempt:{Game}:{date:yyyy-MM-dd}:{guestId}", async () =>
        {
            var attempt = await repository.FindAttemptAsync(Game, guestId, date, ct);
            if (attempt is null)
            {
                if (request is not null) throw new ArgumentException("Start today's Team History challenge first.");
                var challenge = await repository.GetOrCreateChallengeAsync(Game, date, async () =>
                {
                    var definition = await games.CreateChallengeAsync(ct);
                    if (definition.Teams.Count != 4 || definition.Teams.Select(x => x.Id).Distinct().Count() != 4)
                        throw new InvalidOperationException("Team History needs four distinct club clues.");
                    return JsonSerializer.Serialize(definition);
                }, ct);
                attempt = new() { Game = Game, GuestId = guestId, Date = date, Challenge = challenge, ChallengeId = challenge.Id, StateJson = JsonSerializer.Serialize(new AttemptState()) };
                repository.Add(attempt);
            }
            var definition = JsonSerializer.Deserialize<TeamHistoryChallengeDto>(attempt.Challenge.DefinitionJson)!;
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
                    state.Attempts++;
                    if (!request.Skip && request.PlayerId == definition.Player.Id)
                    {
                        state.Completed = true; state.Correct = true;
                        state.Score = Points(state.RevealedCount);
                        state.Feedback = "You found the connection.";
                    }
                    else if (state.RevealedCount < 4)
                    {
                        state.RevealedCount++;
                        state.Feedback = request.Skip ? "Another club was revealed." : "Not this player — another club was revealed.";
                    }
                    else
                    {
                        state.Completed = true; state.Correct = false; state.Score = 0;
                        state.Feedback = "The player is revealed. Come back for tomorrow's challenge.";
                    }
                }
            }
            attempt.StateJson = JsonSerializer.Serialize(state);
            attempt.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveAsync(ct);
            var round = new TeamHistoryRoundDto(attempt.Id, definition.Teams.Take(state.RevealedCount).ToList(), state.RevealedCount, 4,
                state.Completed, state.Correct, state.Attempts, state.Completed ? definition.Player : null);
            return new DailyTeamHistoryDto(attempt.ChallengeId, date, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), round,
                state.Score, state.Completed ? 0 : Points(state.RevealedCount), false, state.UsedPlayerIds.ToList(), state.Feedback);
        }, ct);
    }

    private static int Points(int revealedCount) => (5 - revealedCount) * 25;

    public sealed class AttemptState
    {
        public int RevealedCount { get; set; } = 1;
        public int Attempts { get; set; }
        public bool Completed { get; set; }
        public bool? Correct { get; set; }
        public int Score { get; set; }
        public HashSet<long> UsedPlayerIds { get; set; } = [];
        public HashSet<Guid> Commands { get; set; } = [];
        public string? Feedback { get; set; }
    }
}
