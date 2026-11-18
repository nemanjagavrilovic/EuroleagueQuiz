using System.Security.Claims;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Domain.Auditing;

namespace EuroleagueQuiz.Api.Auditing;

public sealed class PracticeGameAuditor(PracticeAuditSessions sessions, IAuditLogWriter audit)
{
    public async Task StartAsync(HttpContext context, string game, Guid roundId, GridDefinitionDto? grid = null)
    {
        var owner = UserId(context);
        var session = sessions.GetOrAdd(game, roundId, owner, grid);
        if (session.OwnerId != owner) throw new UnauthorizedAccessException("This practice round belongs to another player.");
        if (!owner.HasValue) return;
        await session.Gate.WaitAsync();
        try
        {
            if (session.Started) return;
            await WriteAsync(game, roundId, session, false);
            session.Started = true;
        }
        finally { session.Gate.Release(); }
    }

    public bool CanUse(HttpContext context, string game, Guid roundId) =>
        sessions.Find(game, roundId)?.OwnerId is not Guid owner || UserId(context) == owner;

    public async Task CompleteAsync(HttpContext context, string game, Guid roundId, bool completed,
        bool? correct = null, bool revealed = false, int? correctPlayers = null)
    {
        if (!completed || sessions.Find(game, roundId) is not { } session) return;
        if (session.OwnerId is not Guid owner || UserId(context) != owner) return;
        await session.Gate.WaitAsync();
        try
        {
            if (session.Completed) return;
            await WriteAsync(game, roundId, session, true, correct, revealed, correctPlayers);
            session.Completed = true;
        }
        finally { session.Gate.Release(); }
    }

    public void ValidateGrid(HttpContext context, Guid sessionId,
        IReadOnlyList<long> rows, IReadOnlyList<long> columns)
    {
        var session = GridSession(context, sessionId);
        if (!rows.SequenceEqual(session.Grid!.RowTeams.Select(x => x.Id))
            || !columns.SequenceEqual(session.Grid.ColumnTeams.Select(x => x.Id)))
            throw new ArgumentException("The practice grid does not match this session.");
    }

    public async Task RememberGridMatchAsync(HttpContext context, Guid sessionId, GridPlayerMatchResponse match)
    {
        var session = GridSession(context, sessionId);
        await session.Gate.WaitAsync();
        try { if (!session.Completed && match.AvailableCells.Count > 0) session.Matches[match.Player.Id] = match.AvailableCells.ToArray(); }
        finally { session.Gate.Release(); }
    }

    public async Task CompleteGridAsync(HttpContext context, Guid sessionId, IReadOnlyList<PracticeGridAnswer> answers)
    {
        var session = GridSession(context, sessionId);
        await session.Gate.WaitAsync();
        try
        {
            if (session.Completed) return;
            if (answers is null || answers.Count != 9 || answers.Any(x => x is null)
                || answers.Select(x => (x.Row, x.Column)).Distinct().Count() != 9
                || answers.Any(x => x.Row is < 0 or > 2 || x.Column is < 0 or > 2
                    || !session.Matches.TryGetValue(x.PlayerId, out var cells)
                    || !cells.Any(cell => cell.Row == x.Row && cell.Column == x.Column)))
                throw new ArgumentException("Finish all nine cells with validated players first.");
            foreach (var reused in answers.GroupBy(x => x.PlayerId).Where(x => x.Count() > 1))
                if (reused.Any(answer => session.Matches[answer.PlayerId]
                    .Single(cell => cell.Row == answer.Row && cell.Column == answer.Column).PlayerCount != 1))
                    throw new ArgumentException("A player can only fill multiple cells when each is their sole-player cell.");
            await WriteAsync("grid", sessionId, session, true, true);
            session.Completed = true;
        }
        finally { session.Gate.Release(); }
    }

    private PracticeAuditSession GridSession(HttpContext context, Guid id)
    {
        var session = sessions.Find("grid", id);
        if (session?.Grid is null) throw new KeyNotFoundException("The practice audit session has expired.");
        if (session.OwnerId is not Guid owner || UserId(context) != owner)
            throw new UnauthorizedAccessException("This practice grid belongs to another player.");
        return session;
    }

    private Task WriteAsync(string game, Guid roundId, PracticeAuditSession session, bool completed,
        bool? correct = null, bool revealed = false, int? correctPlayers = null) =>
        audit.WriteAsync(GameAuditCatalog.Action(game, "practice", completed), completed ? AuditResults.Success : AuditResults.Started,
            "PracticeGameSession", roundId.ToString(),
            new { game, mode = "practice", roundId, difficulty = session.Grid?.Difficulty, completed,
                correct, revealed, correctPlayers, leaderboardEligible = false }, session.OwnerId, "User");

    private static Guid? UserId(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
        && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) && id != Guid.Empty ? id : null;
}

public sealed record PracticeGridAnswer(int Row, int Column, long PlayerId);
public sealed record CompletePracticeGridRequest(IReadOnlyList<PracticeGridAnswer> Answers);
