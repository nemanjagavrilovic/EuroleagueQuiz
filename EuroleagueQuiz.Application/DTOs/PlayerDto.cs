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
    public string DisplayName { get; init; } = FormatDisplayName(DisplayName, FirstName, LastName);

    public static PlayerDto FromEntity(Player player) =>
        new(player.Id, player.ExternalId, player.DisplayName, player.FirstName, player.LastName, player.Nationality, player.NationalityCode, player.ImageUrl, FlagUrl(player.NationalityCode));

    private static string FormatDisplayName(string displayName, string? firstName, string? lastName)
    {
        var value = displayName.Trim();
        var comma = value.IndexOf(',');
        if (comma > 0 && comma < value.Length - 1)
        {
            var familyName = value[..comma].Trim();
            var givenName = value[(comma + 1)..].Trim();
            if (givenName.Length > 0 && familyName.Length > 0) return $"{givenName} {familyName}";
        }

        if (value.Length > 0) return value;
        return string.Join(' ', new[] { firstName?.Trim(), lastName?.Trim() }.Where(part => !string.IsNullOrWhiteSpace(part))!);
    }

    private static string? FlagUrl(string? countryCode) =>
        string.IsNullOrWhiteSpace(countryCode) ? null : $"https://flagcdn.com/w80/{countryCode.ToLowerInvariant()}.png";
}
