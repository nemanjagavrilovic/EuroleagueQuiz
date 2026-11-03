using System.Text.Json;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

public sealed class DailyGridService(IDailyGridRepository repository, IGridService grids, IPlayerService players, TimeProvider clock) : IDailyGridService
{
    public Task<DailyGridAttemptDto> StartAsync(DailyAttemptIdentity identity, GridDifficulty difficulty, CancellationToken ct) =>
        ExecuteAsync(identity, difficulty, null, ct);

    public Task<DailyGridAttemptDto> GuessAsync(DailyAttemptIdentity identity, DailyGridGuessRequest request, CancellationToken ct) =>
        ExecuteAsync(identity, null, async (state, definition) =>
        {
            if (request.CommandId == Guid.Empty) throw new ArgumentException("A command ID is required.");
            if (state.Completed || state.Commands.Contains(request.CommandId)) return;
            if (state.PendingPlayer is not null) throw new ArgumentException("Choose a highlighted cell before guessing another player.");
            if (state.UsedPlayerIds.Contains(request.PlayerId)) throw new ArgumentException("This player has already been guessed.");
            var player = definition.Candidates.SelectMany(x => x).FirstOrDefault(x => x.Id == request.PlayerId)
                ?? await players.GetByIdAsync(request.PlayerId, ct)
                ?? throw new ArgumentException("Player not found.");
            state.Commands.Add(request.CommandId);
            state.UsedPlayerIds.Add(player.Id);
            state.GuessesUsed++;
            state.PendingCells = Enumerable.Range(0, 9)
                .Where(i => state.Answers.All(x => x.Row * 3 + x.Column != i) && definition.Candidates[i].Any(x => x.Id == player.Id))
                .Select(i => new GridCellDto(i / 3, i % 3, definition.Grid.RowTeams[i / 3].Id, definition.Grid.ColumnTeams[i % 3].Id, definition.Candidates[i].Count)).ToList();
            if (state.PendingCells.Count == 0)
            {
                state.Feedback = "This player does not match any empty grid cell.";
                FinishIfNeeded(state, definition);
            }
            else if (state.PendingCells.Count == 1 || state.PendingCells.Any(x => x.PlayerCount == 1))
            {
                foreach (var cell in state.PendingCells) state.Answers.Add(new(cell.Row, cell.Column, player, false));
                state.PendingCells.Clear();
                state.Feedback = "Correct connection!";
                FinishIfNeeded(state, definition);
            }
            else
            {
                state.PendingPlayer = player;
                state.Feedback = "Choose a highlighted square to place this player.";
            }
        }, ct, request.ChallengeId);

    public Task<DailyGridAttemptDto> PlaceAsync(DailyAttemptIdentity identity, DailyGridPlaceRequest request, CancellationToken ct) =>
        ExecuteAsync(identity, null, (state, definition) =>
        {
            if (state.Completed || state.PendingPlayer is null) return Task.CompletedTask;
            var cell = state.PendingCells.FirstOrDefault(x => x.Row == request.Row && x.Column == request.Column)
                ?? throw new ArgumentException("Choose one of the highlighted empty cells.");
            state.Answers.Add(new(cell.Row, cell.Column, state.PendingPlayer, false));
            state.PendingPlayer = null;
            state.PendingCells.Clear();
            state.Feedback = "Correct connection!";
            FinishIfNeeded(state, definition);
            return Task.CompletedTask;
        }, ct, request.ChallengeId);

    public Task<DailyGridAttemptDto> RevealAsync(DailyAttemptIdentity identity, DailyGridRevealRequest request, CancellationToken ct) =>
        ExecuteAsync(identity, null, (state, definition) =>
        {
            if (!state.Completed) { state.Revealed = true; Complete(state, definition); state.Feedback = "Grid revealed. Your earned points are kept."; }
            return Task.CompletedTask;
        }, ct, request.ChallengeId);

    private async Task<DailyGridAttemptDto> ExecuteAsync(DailyAttemptIdentity identity, GridDifficulty? difficulty, Func<AttemptState, Definition, Task>? action, CancellationToken ct, long? challengeId = null)
    {
        if (identity.GuestId == Guid.Empty) throw new ArgumentException("A guest identifier is required.");
        var date = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        return await repository.WithLockAsync($"grid-attempt:{date:yyyy-MM-dd}:{identity.AttemptId}", async () =>
        {
            var attempt = await repository.FindOrClaimAsync(identity, date, ct);
            if (attempt is null)
            {
                if (difficulty is null) throw new ArgumentException("Start today's grid first.");
                if (!Enum.IsDefined(difficulty.Value)) throw new ArgumentException("Unknown difficulty.");
                var challenge = await repository.GetOrCreateChallengeAsync(date, difficulty.ToString()!, async () =>
                {
                    var grid = await grids.CreateGridAsync(ct, difficulty: difficulty.Value);
                    var candidates = new List<List<PlayerDto>>();
                    for (var row = 0; row < 3; row++)
                    for (var col = 0; col < 3; col++)
                        candidates.Add((await players.GetCommonPlayersAsync(grid.RowTeams[row].Id, grid.ColumnTeams[col].Id, ct)).DistinctBy(x => x.Id).ToList());
                    if (candidates.Any(x => x.Count == 0)) throw new InvalidOperationException("The daily grid has an empty intersection.");
                    return JsonSerializer.Serialize(new Definition(grid, candidates));
                }, ct);
                attempt = new DailyGridAttempt { Date = date, Challenge = challenge, ChallengeId = challenge.Id, StateJson = JsonSerializer.Serialize(new AttemptState()) };
                identity.Initialize(attempt);
                repository.Add(attempt);
            }
            if (challengeId is not null && challengeId != attempt.ChallengeId) throw new ArgumentException("This challenge has changed. Refresh to load today's grid.");
            var definition = JsonSerializer.Deserialize<Definition>(attempt.Challenge.DefinitionJson)!;
            var state = JsonSerializer.Deserialize<AttemptState>(attempt.StateJson)!;
            // Resume attempts ended by the old guess cap; explicit reveals stay final.
            if (state.Completed && !state.Revealed && state.Answers.Any(x => x.Revealed))
            {
                state.Answers.RemoveAll(x => x.Revealed);
                state.Completed = false;
                state.Feedback = "Keep going — you have unlimited guesses.";
            }
            if (action is not null) await action(state, definition);
            attempt.StateJson = JsonSerializer.Serialize(state);
            attempt.UpdatedAtUtc = clock.GetUtcNow().UtcDateTime;
            await repository.SaveAsync(ct);
            var correct = state.Answers.Count(x => !x.Revealed);
            return new DailyGridAttemptDto(attempt.ChallengeId, date, date.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc), definition.Grid,
                correct * 10 + (correct == 9 ? 10 : 0), state.GuessesUsed, state.Completed, state.Revealed, attempt.LeaderboardEligible,
                state.Answers, state.PendingPlayer, state.PendingCells, state.UsedPlayerIds.ToList(), state.Feedback);
        }, ct);
    }

    private static void FinishIfNeeded(AttemptState state, Definition definition)
    {
        if (state.Answers.Count == 9) Complete(state, definition);
    }

    private static void Complete(AttemptState state, Definition definition)
    {
        state.Completed = true;
        state.PendingPlayer = null;
        state.PendingCells.Clear();
        // Prefer distinct remaining answers. Singleton intersections retain the game's
        // existing exception, where the same player may be necessary in several cells.
        var cells = Enumerable.Range(0, 9).Where(i => state.Answers.All(x => x.Row * 3 + x.Column != i)).ToList();
        var used = state.Answers.Select(x => x.Player.Id).ToHashSet();
        var assignments = new Dictionary<int, PlayerDto>();
        bool Assign(int cell, HashSet<long> visited)
        {
            foreach (var candidate in definition.Candidates[cell].OrderBy(x => x.Id))
            {
                if (used.Contains(candidate.Id) || !visited.Add(candidate.Id)) continue;
                var previous = assignments.FirstOrDefault(x => x.Value.Id == candidate.Id);
                if (previous.Value is null || Assign(previous.Key, visited)) { assignments[cell] = candidate; return true; }
            }
            return false;
        }
        foreach (var cell in cells.OrderBy(i => definition.Candidates[i].Count)) Assign(cell, new HashSet<long>());
        foreach (var cell in cells)
        {
            var player = assignments.GetValueOrDefault(cell) ?? definition.Candidates[cell].OrderBy(x => x.Id).First();
            state.Answers.Add(new(cell / 3, cell % 3, player, true));
        }
    }

    public sealed record Definition(GridDefinitionDto Grid, List<List<PlayerDto>> Candidates);
    public sealed class AttemptState
    {
        public List<DailyGridAnswerDto> Answers { get; set; } = [];
        public List<GridCellDto> PendingCells { get; set; } = [];
        public PlayerDto? PendingPlayer { get; set; }
        public HashSet<long> UsedPlayerIds { get; set; } = [];
        public HashSet<Guid> Commands { get; set; } = [];
        public int GuessesUsed { get; set; }
        public bool Completed { get; set; }
        public bool Revealed { get; set; }
        public string? Feedback { get; set; }
    }
}
