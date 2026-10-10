using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Application.DTOs;

public sealed record PlayerDto(
    long Id,
    string ExternalId,
    string DisplayName,
    string? FirstName,
    string? LastName,
    string? Nationality,
    string? NationalityCode,
    string? ImageUrl,
    string? CountryFlagUrl)
{
    public static PlayerDto FromEntity(Player player) =>
        new(player.Id, player.ExternalId, player.DisplayName, player.FirstName, player.LastName, player.Nationality, player.NationalityCode, player.ImageUrl, FlagUrl(player.NationalityCode));

    private static string? FlagUrl(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode) ? null : $"https://flagcdn.com/w80/{countryCode.ToLowerInvariant()}.png";
}
