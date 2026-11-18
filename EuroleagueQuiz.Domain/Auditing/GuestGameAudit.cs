namespace EuroleagueQuiz.Domain.Auditing;

public sealed record GuestGameAuditDefinition(string Game, string Name, string StartedAction, string CompletedAction);

public static class GuestGameAudit
{
    public const string ActorType = "Guest";
    public const string EntityType = "GuestGameAttempt";

    public static IReadOnlyList<GuestGameAuditDefinition> Games { get; } = Array.AsReadOnly(
        GameAuditCatalog.Games.Select(x => new GuestGameAuditDefinition(x.Game, x.Name,
            GameAuditCatalog.Action(x.Game, "guest", false), GameAuditCatalog.Action(x.Game, "guest", true))).ToArray());

    // A stable key is available before EF assigns a new grid attempt's numeric ID.
    // Putting the browser UUID first also allows database-side distinct guest counts.
    public static string AttemptKey(Guid guestId, string game, DateOnly date, long challengeId) =>
        FormattableString.Invariant($"{guestId:D}:{game}:{date:yyyy-MM-dd}:{challengeId}");

    public static Guid? BrowserId(string? attemptKey) =>
        attemptKey is { Length: >= 37 } && attemptKey[36] == ':'
        && Guid.TryParse(attemptKey[..36], out var id) && id != Guid.Empty ? id : null;
}
