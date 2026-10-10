namespace EuroleagueQuiz.Domain.Entities;

public sealed class Team
{
    private Team() { }

    public Team(string externalId, string name)
    {
        ExternalId = externalId;
        Name = name;
    }

    public long Id { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string Name { get; private set; } = null!;
    public string? ShortName { get; private set; }
    public string? TvCode { get; private set; }
    public string? PermanentName { get; private set; }
    public string? PermanentAlias { get; private set; }
    public bool IsVirtual { get; private set; }
    public string? Country { get; private set; }
    public string? CountryCode { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    public ICollection<PlayerTeamSeason> PlayerSeasons { get; private set; } = new List<PlayerTeamSeason>();

    public void Update(string name, string? shortName, string? country, string? countryCode = null, string? tvCode = null, string? permanentName = null, string? permanentAlias = null, bool isVirtual = false, string? imageUrl = null)
    {
        Name = name;
        ShortName = shortName;
        TvCode = tvCode;
        PermanentName = permanentName;
        PermanentAlias = permanentAlias;
        IsVirtual = isVirtual;
        Country = country;
        CountryCode = countryCode;
        ImageUrl = imageUrl;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
