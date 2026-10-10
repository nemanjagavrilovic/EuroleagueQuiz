using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Application.DTOs;

public sealed record TeamDto(
    long Id,
    string ExternalId,
    string Name,
    string? ShortName,
    string? Country,
    string? CountryCode,
    string? ImageUrl,
    string? CountryFlagUrl)
{
    public static TeamDto FromEntity(Team team) =>
        new(team.Id, team.ExternalId, team.Name, team.ShortName, team.Country, team.CountryCode, team.ImageUrl, FlagUrl(team.CountryCode));

    private static string? FlagUrl(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode) ? null : $"https://flagcdn.com/w80/{countryCode.ToLowerInvariant()}.png";
}
