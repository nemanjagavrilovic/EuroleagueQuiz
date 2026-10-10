namespace EuroleagueQuiz.Domain.Entities;

public sealed class Player
{
    private Player() { }

    public Player(string externalId, string displayName)
    {
        ExternalId = externalId;
        DisplayName = displayName;
    }

    public long Id { get; private set; }
    public string ExternalId { get; private set; } = null!;
    public string? ProviderExternalId { get; private set; }
    public string? FirstName { get; private set; }
    public string? LastName { get; private set; }
    public string DisplayName { get; private set; } = null!;
    public string? Nationality { get; private set; }
    public string? NationalityCode { get; private set; }
    public string? ImageUrl { get; private set; }
    public DateOnly? DateOfBirth { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; private set; } = DateTime.UtcNow;

    public ICollection<PlayerTeamSeason> TeamSeasons { get; private set; } = new List<PlayerTeamSeason>();

    public void Update(string displayName, string? firstName, string? lastName, string? nationality, string? nationalityCode, DateOnly? dateOfBirth, string? providerExternalId = null, string? imageUrl = null)
    {
        DisplayName = displayName;
        FirstName = firstName;
        LastName = lastName;
        Nationality = nationality;
        NationalityCode = nationalityCode;
        DateOfBirth = dateOfBirth;
        ProviderExternalId = providerExternalId;
        ImageUrl = imageUrl;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
