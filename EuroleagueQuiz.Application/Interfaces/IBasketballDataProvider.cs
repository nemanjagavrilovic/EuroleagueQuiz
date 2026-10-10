namespace EuroleagueQuiz.Application.Interfaces;

public interface IBasketballDataProvider
{
    Task<IReadOnlyList<ProviderSeason>> GetSeasonsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderTeam>> GetTeamsAsync(string seasonExternalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProviderPlayer>> GetTeamPlayersAsync(string seasonExternalId, string teamExternalId, CancellationToken cancellationToken);
}

public sealed record ProviderSeason(
    string ExternalId,
    string Name,
    int StartYear,
    int EndYear,
    string? Alias,
    string CompetitionCode,
    DateTime? StartDate,
    DateTime? ActivationDate,
    DateTime? EndDate);

public sealed record ProviderTeam(
    string ExternalId,
    string Name,
    string? ShortName,
    string? Country,
    string? CountryCode,
    string? TvCode,
    string? PermanentName,
    string? PermanentAlias,
    bool IsVirtual,
    string? ImageUrl);

public sealed record ProviderPlayer(
    string ExternalId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Nationality,
    string? NationalityCode,
    DateOnly? DateOfBirth,
    string? ProviderExternalId,
    bool Active,
    DateTime? StartDate,
    DateTime? EndDate,
    string? JerseyNumber,
    int? Position,
    string? PositionName,
    string? ImageUrl);
