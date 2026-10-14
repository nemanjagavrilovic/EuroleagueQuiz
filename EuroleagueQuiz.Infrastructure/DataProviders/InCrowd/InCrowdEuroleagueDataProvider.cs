using System.Globalization;
using System.Text.Json;
using EuroleagueQuiz.Application.Interfaces;

namespace EuroleagueQuiz.Infrastructure.DataProviders.InCrowd;

public sealed class InCrowdEuroleagueDataProvider : IBasketballDataProvider
{
    private const string CompetitionCode = "E";
    private readonly HttpClient _http;

    public InCrowdEuroleagueDataProvider(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<ProviderSeason>> GetSeasonsAsync(CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"competitions/{CompetitionCode}/seasons", cancellationToken);
        return ReadItems(document.RootElement).Select(x =>
        {
            var code = ReadString(x, "code", "Code") ?? string.Empty;
            var startYear = ParseSeasonYear(code);
            return new ProviderSeason(
                code,
                ReadString(x, "name", "Name") ?? code,
                startYear,
                startYear + 1,
                ReadString(x, "alias", "Alias"),
                ReadString(x, "competitionCode", "CompetitionCode") ?? CompetitionCode,
                ParseDateTime(ReadString(x, "startDate", "StartDate")),
                ParseDateTime(ReadString(x, "activationDate", "ActivationDate")),
                ParseDateTime(ReadString(x, "endDate", "EndDate")));
        }).Where(x => x.StartYear > 0).OrderBy(x => x.StartYear).ToList();
    }

    public async Task<IReadOnlyList<ProviderTeam>> GetTeamsAsync(string seasonExternalId, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"competitions/{CompetitionCode}/seasons/{seasonExternalId}/clubs", cancellationToken);
        return ReadItems(document.RootElement).Select(x => new ProviderTeam(
            ReadString(x, "code", "Code", "id", "Id") ?? string.Empty,
            ReadString(x, "name", "Name") ?? string.Empty,
            ReadString(x, "shortName", "ShortName", "abbreviation", "Abbreviation"),
            ReadNestedString(x, "country", "name", "Name") ?? ReadString(x, "country", "Country"),
            ReadNestedString(x, "country", "code", "Code"),
            ReadString(x, "tvCode", "TvCode"),
            ReadString(x, "clubPermanentName", "permanentName", "PermanentName"),
            ReadString(x, "clubPermanentAlias", "permanentAlias", "PermanentAlias"),
            ReadBoolean(x, "isVirtual", "IsVirtual"),
            ReadNestedString(x, "images", "crest", "Crest")
        )).Where(x => !string.IsNullOrWhiteSpace(x.ExternalId) && !string.IsNullOrWhiteSpace(x.Name)).ToList();
    }

    public async Task<IReadOnlyList<ProviderPlayer>> GetTeamPlayersAsync(string seasonExternalId, string teamExternalId, CancellationToken cancellationToken)
    {
        using var document = await GetJsonAsync($"competitions/{CompetitionCode}/seasons/{seasonExternalId}/clubs/{teamExternalId}/people", cancellationToken);
        return ReadItems(document.RootElement).Where(IsPlayer).Select(ToPlayer)
            .Where(x => !string.IsNullOrWhiteSpace(x.ExternalId) && !string.IsNullOrWhiteSpace(x.DisplayName))
            .DistinctBy(x => x.ExternalId).ToList();
    }

    private async Task<JsonDocument> GetJsonAsync(string relativePath, CancellationToken cancellationToken)
    {
        using var response = await _http.GetAsync(relativePath, cancellationToken);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(json);
    }

    private static IEnumerable<JsonElement> ReadItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array) return root.EnumerateArray();
        return root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray() : Enumerable.Empty<JsonElement>();
    }

    private static bool IsPlayer(JsonElement element)
    {
        var type = ReadString(element, "type", "Type");
        return type?.Equals("J", StringComparison.OrdinalIgnoreCase) == true || type?.Contains("player", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static ProviderPlayer ToPlayer(JsonElement element)
    {
        var person = element.TryGetProperty("person", out var nested) ? nested : element;
        var firstName = ReadString(person, "firstName", "FirstName", "passportName", "PassportName");
        var lastName = ReadString(person, "lastName", "LastName", "passportSurname", "PassportSurname");
        var displayName = ReadString(person, "displayName", "DisplayName", "name", "Name") ?? $"{firstName} {lastName}".Trim();
        var personCode = ReadString(person, "code", "Code", "id", "Id");
        var providerExternalId = ReadString(element, "externalId", "ExternalId");
        var externalId = personCode ?? providerExternalId ?? string.Empty;
        var country = person.TryGetProperty("country", out var countryElement) ? countryElement : default;
        return new ProviderPlayer(
            externalId,
            displayName,
            firstName,
            lastName,
            ReadString(country, "name", "Name"),
            ReadString(country, "code", "Code"),
            ParseDate(ReadString(person, "dateOfBirth", "DateOfBirth", "birthDate", "BirthDate")),
            providerExternalId,
            ReadBoolean(element, "active", "Active"),
            ParseDateTime(ReadString(element, "startDate", "StartDate")),
            ParseDateTime(ReadString(element, "endDate", "EndDate")),
            ReadString(element, "dorsal", "Dorsal", "dorsalRaw", "DorsalRaw"),
            ReadInt32(element, "position", "Position"),
            ReadString(element, "positionName", "PositionName"),
            ReadNestedString(person, "images", "headshot", "Headshot", "profile", "Profile", "default", "Default"));
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.String) return value.GetString();
            if (value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False) return value.ToString();
        }
        return null;
    }

    private static string? ReadNestedString(JsonElement element, string property, params string[] names) =>
        element.TryGetProperty(property, out var nested) && nested.ValueKind == JsonValueKind.Object ? ReadString(nested, names) : null;

    private static bool ReadBoolean(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) return value.GetBoolean();
            if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
        }
        return false;
    }

    private static int? ReadInt32(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!element.TryGetProperty(name, out var value)) continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
            if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)) return parsed;
        }
        return null;
    }

    private static int ParseSeasonYear(string code)
    {
        var yearText = new string(code.SkipWhile(c => !char.IsDigit(c)).Take(4).ToArray());
        return int.TryParse(yearText, out var year) ? year : 0;
    }

    private static DateOnly? ParseDate(string? value) =>
        DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null;

    private static DateTime? ParseDateTime(string? value) =>
        DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var date)
            ? DateTime.SpecifyKind(date, DateTimeKind.Utc)
            : null;
}
