namespace EuroleagueQuiz.Domain.Auditing;

public sealed record GameAuditDefinition(string Game, string Name, string ActionPrefix);

public static class GameAuditCatalog
{
    public static IReadOnlyList<GameAuditDefinition> Games { get; } = Array.AsReadOnly<GameAuditDefinition>([
        new("grid", "Player Grid", "Game.Grid"),
        new("team-history", "Team History", "Game.TeamHistory"),
        new("missing-club", "Missing Club", "Game.MissingClub"),
        new("career-order", "Career Order", "Game.CareerOrder"),
        new("mystery-player", "Mystery Player", "Game.MysteryPlayer"),
        new("five-player", "Starting Five", "Game.StartingFive")
    ]);

    public static string Action(string game, string mode, bool completed)
    {
        var prefix = Games.Single(x => x.Game == game).ActionPrefix;
        var suffix = mode switch
        {
            "ranked" => "Ranked", "practice" => "Practice", "daily" => "Daily", "guest" => "Guest",
            _ => throw new ArgumentException("Unknown game audit mode.", nameof(mode))
        };
        return prefix + suffix + (completed ? "Completed" : "Started");
    }
}
