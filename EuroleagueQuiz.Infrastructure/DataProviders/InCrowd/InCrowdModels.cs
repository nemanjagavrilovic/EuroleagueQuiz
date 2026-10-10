namespace EuroleagueQuiz.Infrastructure.DataProviders.InCrowd;

internal sealed record InCrowdSeason(string ExternalId, string Name, int StartYear, int EndYear);
internal sealed record InCrowdTeam(string ExternalId, string Name, string? ShortName, string? Country);
internal sealed record InCrowdPlayer(string ExternalId, string DisplayName, string? FirstName, string? LastName, string? Nationality, DateOnly? DateOfBirth);
