using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class DailyCareerOrderService(IDailyGameRepository repository, ICareerGamesService games, TimeProvider clock) : IDailyCareerOrderService
{
    public const string Game = "career-order";
    public Task<DailyCareerOrderDto> StartAsync(DailyAttemptIdentity identity, CancellationToken ct) => ExecuteAsync(identity, null, false, ct);
    public Task<DailyCareerOrderDto> SaveDraftAsync(DailyAttemptIdentity identity, DailyCareerOrderRequest request, CancellationToken ct) => ExecuteAsync(identity, request, false, ct);
    public Task<DailyCareerOrderDto> SubmitAsync(DailyAttemptIdentity identity, DailyCareerOrderRequest request, CancellationToken ct) => ExecuteAsync(identity, request, true, ct);

    private Task<DailyCareerOrderDto> ExecuteAsync(DailyAttemptIdentity identity, DailyCareerOrderRequest? request, bool submit, CancellationToken ct)
    {
        if (identity.GuestId == Guid.Empty) throw new ArgumentException("A guest identifier is required.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return repository.WithLockAsync($"daily-attempt:{Game}:{date:yyyy-MM-dd}:{identity.AttemptId}", async () =>
        {
            var attempt = await repository.FindOrClaimAsync(Game, identity, date, ct);
            if (attempt is null)
            {
                if (request is not null) throw new ArgumentException("Start today's Career Order challenge first.");
                var challenge = await repository.GetOrCreateChallengeAsync(Game, date, async () =>
                {
                    var definition = await games.CreateCareerOrderChallengeAsync(ct);
                    if (definition.Clubs.Count != 4 || definition.Clubs.Select(x => x.Id).Distinct().Count() != 4 ||
                        definition.CorrectOrder.Count != 4 || definition.CorrectOrder.Select(x => x.Id).Distinct().Count() != 4 ||
                        !definition.Clubs.Select(x => x.Id).ToHashSet().SetEquals(definition.CorrectOrder.Select(x => x.Id)))
                        throw new InvalidOperationException("Career Order needs four distinct clubs and a matching correct order.");
                    return JsonSerializer.Serialize(definition);
                }, ct);
                var definition = JsonSerializer.Deserialize<CareerOrderChallengeDto>(challenge.DefinitionJson)!;
                attempt = new() { Game = Game, Date = date, Challenge = challenge, ChallengeId = challenge.Id,
                    StateJson = JsonSerializer.Serialize(new AttemptState { TeamIds = definition.Clubs.Select(x => x.Id).ToList() }) };
                identity.Initialize(attempt);
                repository.Add(attempt);
            }
            var puzzle = JsonSerializer.Deserialize<CareerOrderChallengeDto>(attempt.Challenge.DefinitionJson)!;
            var state = JsonSerializer.Deserialize<AttemptState>(attempt.StateJson)!;
            if (request is not null)
            {
                if (request.ChallengeId != attempt.ChallengeId) throw new ArgumentException("This challenge has changed. Refresh to load today's game.");
                if (request.CommandId == Guid.Empty) throw new ArgumentException("A command ID is required.");
                if (!state.Completed && !state.Commands.Contains(request.CommandId))
                {
                    if (request.TeamIds is null || request.TeamIds.Count != 4 || request.TeamIds.Distinct().Count() != 4 ||
                        !request.TeamIds.ToHashSet().SetEquals(puzzle.Clubs.Select(x => x.Id)))
                        throw new ArgumentException("Submit each of the four offered clubs exactly once.");
                    state.TeamIds = request.TeamIds.ToList();
                    state.Commands.Add(request.CommandId);
                    if (submit)
                    {
                        var ranks = puzzle.CorrectOrder.Select((club, index) => (club.Id, index)).ToDictionary(x => x.Id, x => x.index);
                        var correctPairs = 0;
                        for (var i = 0; i < state.TeamIds.Count; i++)
                        for (var j = i + 1; j < state.TeamIds.Count; j++)
                            if (ranks[state.TeamIds[i]] < ranks[state.TeamIds[j]]) correctPairs++;
                        state.CorrectPairs = correctPairs;
                        state.Score = (int)Math.Round(correctPairs * 100m / 6, MidpointRounding.AwayFromZero);
                        state.Completed = true;
                    }
                }
            }
            attempt.StateJson = JsonSerializer.Serialize(state);
            attempt.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveAsync(ct);
            var clubs = puzzle.Clubs.ToDictionary(x => x.Id);
            var round = new CareerOrderRoundDto(attempt.Id, puzzle.Player, state.TeamIds.Select(id => clubs[id]).ToList(),
                state.Completed, state.Completed ? state.Score == 100 : null, state.Completed ? puzzle.CorrectOrder : null);
            return new DailyCareerOrderDto(attempt.ChallengeId, date, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                round, state.Score, state.Completed ? state.CorrectPairs : null, 6, attempt.LeaderboardEligible);
        }, ct);
    }

    public sealed class AttemptState
    {
        public List<long> TeamIds { get; set; } = [];
        public HashSet<Guid> Commands { get; set; } = [];
        public bool Completed { get; set; }
        public int CorrectPairs { get; set; }
        public int Score { get; set; }
    }
}
