namespace EuroleagueQuiz.Domain.Entities;

public sealed class DailyGridChallenge
{
    public long Id { get; set; }
    public DateOnly Date { get; set; }
    public string Difficulty { get; set; } = "Normal";
    // Null identifies a challenge created before guest and ranked grids were separated.
    public bool? Ranked { get; set; }
    public string DefinitionJson { get; set; } = "";
    public int ScoringVersion { get; set; } = 1;
}

public sealed class DailyGridAttempt
{
    public long Id { get; set; }
    public Guid GuestId { get; set; }
    public Guid? UserId { get; set; }
    public bool Ranked { get; set; }
    public bool LeaderboardEligible { get; set; }
    public int Score { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateOnly Date { get; set; }
    public long ChallengeId { get; set; }
    public DailyGridChallenge Challenge { get; set; } = null!;
    public string StateJson { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}
