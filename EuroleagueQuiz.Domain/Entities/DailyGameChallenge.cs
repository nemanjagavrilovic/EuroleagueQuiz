namespace EuroleagueQuiz.Domain.Entities;

public sealed class DailyGameChallenge
{
    public long Id { get; set; }
    public string Game { get; set; } = "";
    public DateOnly Date { get; set; }
    public string DefinitionJson { get; set; } = "";
    public int ScoringVersion { get; set; } = 1;
}

public sealed class DailyGameAttempt
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GuestId { get; set; }
    public Guid? UserId { get; set; }
    public bool LeaderboardEligible { get; set; }
    public int Score { get; set; }
    public bool Completed { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Game { get; set; } = "";
    public DateOnly Date { get; set; }
    public long ChallengeId { get; set; }
    public DailyGameChallenge Challenge { get; set; } = null!;
    public string StateJson { get; set; } = "";
    public DateTime UpdatedAtUtc { get; set; }
}
