namespace EuroleagueQuiz.Domain.Entities;

public sealed class Season
{
    private Season() { }

    public Season(string externalId, string name, int startYear, int endYear, string competitionCode = "E")
    {
        ExternalId = externalId;
        Name = name;
        StartYear = startYear;
        EndYear = endYear;
        CompetitionCode = competitionCode;
    }

    public long Id { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public int StartYear { get; private set; }
    public int EndYear { get; private set; }
    public string CompetitionCode { get; private set; } = "E";
    public string? Alias { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? ActivationDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    public ICollection<PlayerTeamSeason> PlayerSeasons { get; private set; } = new List<PlayerTeamSeason>();

    public void Update(string name, int startYear, int endYear, string? alias = null, DateTime? startDate = null, DateTime? activationDate = null, DateTime? endDate = null)
    {
        Name = name;
        StartYear = startYear;
        EndYear = endYear;
        Alias = alias;
        StartDate = startDate;
        ActivationDate = activationDate;
        EndDate = endDate;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
