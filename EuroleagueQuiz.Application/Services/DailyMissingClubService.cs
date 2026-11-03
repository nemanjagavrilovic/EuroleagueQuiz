using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class DailyMissingClubService(IDailyGameRepository repository, ICareerGamesService games, TimeProvider clock) : IDailyMissingClubService
{
    public const string Game = "missing-club";

    public Task<DailyMissingClubDto> StartAsync(DailyAttemptIdentity identity, CancellationToken ct) => ExecuteAsync(identity, null, ct);
    public Task<DailyMissingClubDto> GuessAsync(DailyAttemptIdentity identity, DailyMissingClubGuessRequest request, CancellationToken ct) => ExecuteAsync(identity, request, ct);

    private Task<DailyMissingClubDto> ExecuteAsync(DailyAttemptIdentity identity, DailyMissingClubGuessRequest? request, CancellationToken ct)
    {
        if (identity.GuestId == Guid.Empty) throw new ArgumentException("A guest identifier is required.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return repository.WithLockAsync($"daily-attempt:{Game}:{date:yyyy-MM-dd}:{identity.AttemptId}", async () =>
        {
            var attempt = await repository.FindOrClaimAsync(Game, identity, date, ct);
            if (attempt is null)
            {
                if (request is not null) throw new ArgumentException("Start today's Missing Club challenge first.");
                var challenge = await repository.GetOrCreateChallengeAsync(Game, date, async () =>
                {
                    var definition = await games.CreateMissingClubChallengeAsync(ct);
                    if (definition.KnownTeams.Count != 3 || definition.KnownTeams.Select(x => x.Id).Distinct().Count() != 3 ||
                        definition.Options.Count != 4 || definition.Options.Select(x => x.Id).Distinct().Count() != 4 ||
                        !definition.Options.Any(x => x.Id == definition.CorrectTeam.Id) ||
                        definition.Options.Any(x => definition.KnownTeams.Any(t => t.Id == x.Id)))
                        throw new InvalidOperationException("Missing Club needs three distinct clues and four valid options.");
                    return JsonSerializer.Serialize(definition);
                }, ct);
                attempt = new() { Game = Game, Date = date, Challenge = challenge, ChallengeId = challenge.Id, StateJson = JsonSerializer.Serialize(new AttemptState()) };
                identity.Initialize(attempt);
                repository.Add(attempt);
            }
            var definition = JsonSerializer.Deserialize<MissingClubChallengeDto>(attempt.Challenge.DefinitionJson)!;
            var state = JsonSerializer.Deserialize<AttemptState>(attempt.StateJson)!;
            if (request is not null)
            {
                if (request.ChallengeId != attempt.ChallengeId) throw new ArgumentException("This challenge has changed. Refresh to load today's game.");
                if (request.CommandId == Guid.Empty) throw new ArgumentException("A command ID is required.");
                if (!state.Completed)
                {
                    if (!request.Skip && (request.TeamId is null || !definition.Options.Any(x => x.Id == request.TeamId)))
                        throw new ArgumentException("Choose one of the offered clubs.");
                    state.Completed = true;
                    state.CommandId = request.CommandId;
                    state.SelectedTeamId = request.Skip ? null : request.TeamId;
                    state.Correct = !request.Skip && request.TeamId == definition.CorrectTeam.Id;
                    state.Score = state.Correct ? 100 : 0;
                    state.Feedback = state.Correct ? "You found the missing connection." : request.Skip ? "The missing club is revealed." : "That wasn't the missing club. The answer is now revealed.";
                }
            }
            attempt.StateJson = JsonSerializer.Serialize(state);
            attempt.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveAsync(ct);
            var round = new MissingClubRoundDto(attempt.Id, definition.Player, definition.KnownTeams, definition.Options,
                state.Completed, state.Completed ? state.Correct : null, state.Completed ? definition.CorrectTeam : null);
            return new DailyMissingClubDto(attempt.ChallengeId, date, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), round,
                state.Score, attempt.LeaderboardEligible, state.SelectedTeamId, state.Feedback);
        }, ct);
    }

    public sealed class AttemptState
    {
        public bool Completed { get; set; }
        public bool Correct { get; set; }
        public int Score { get; set; }
        public long? SelectedTeamId { get; set; }
        public Guid? CommandId { get; set; }
        public string? Feedback { get; set; }
    }
}
