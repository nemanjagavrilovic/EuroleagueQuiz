namespace EuroleagueQuiz.Domain.Entities;

public sealed class PlayerTeamSeason
{
    private PlayerTeamSeason() { }

    public PlayerTeamSeason(Player player, Team team, Season season, string source)
    {
        Player = player;
        Team = team;
        Season = season;
        Source = source;
    }

    public long Id { get; private set; }
    public long PlayerId { get; private set; }
    public long TeamId { get; private set; }
    public long SeasonId { get; private set; }
    public int? GamesPlayed { get; private set; }
    public string? ProviderExternalId { get; private set; }
    public bool Active { get; private set; }
    public DateTime? StartDate { get; private set; }
    public DateTime? EndDate { get; private set; }
    public string? JerseyNumber { get; private set; }
    public int? Position { get; private set; }
    public string? PositionName { get; private set; }
    public string Source { get; private set; } = null!;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    public Player Player { get; private set; } = null!;
    public Team Team { get; private set; } = null!;
    public Season Season { get; private set; } = null!;

    public void SetRosterData(string? providerExternalId, bool active, DateTime? startDate, DateTime? endDate, string? jerseyNumber, int? position, string? positionName)
    {
        ProviderExternalId = providerExternalId;
        Active = active;
        StartDate = startDate;
        EndDate = endDate;
        JerseyNumber = jerseyNumber;
        Position = position;
        PositionName = positionName;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
