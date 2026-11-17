using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Services;

public static class MysteryPlayerClues
{
    public static IReadOnlyList<string> Create(PlayerDto player, IReadOnlyList<TeamDto> orderedClubs)
    {
        var clubs = orderedClubs.DistinctBy(x => x.Id).Take(3).ToArray();
        if (clubs.Length != 3)
            throw new ArgumentException("Mystery Player needs three distinct club clues.", nameof(orderedClubs));

        var displayName = player.DisplayName.Trim();
        if (displayName.Length == 0)
            throw new ArgumentException("Mystery Player needs a player name.", nameof(player));

        var nameParts = displayName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstName = string.IsNullOrWhiteSpace(player.FirstName) ? nameParts[0] : player.FirstName.Trim();
        var lastName = player.LastName?.Trim();
        if (string.IsNullOrWhiteSpace(lastName))
        {
            lastName = displayName.StartsWith(firstName + " ", StringComparison.OrdinalIgnoreCase)
                ? displayName[firstName.Length..].Trim()
                : nameParts.ElementAtOrDefault(1) ?? "";
        }

        var openingClue = $"Played for {clubs[0].Name}.";
        var nationality = player.Nationality?.Trim();
        if (!string.IsNullOrWhiteSpace(nationality) &&
            !nationality.Equals("Unknown", StringComparison.OrdinalIgnoreCase) &&
            !nationality.Equals("N/A", StringComparison.OrdinalIgnoreCase) && nationality != "-")
            openingClue = $"Nationality: {nationality}. {openingClue}";

        var initialHint = lastName.Length > 0
            ? $"Last name starts with {char.ToUpperInvariant(lastName[0])}."
            : $"The player's name starts with {char.ToUpperInvariant(firstName[0])}.";
        var firstNameHint = lastName.Length > 0
            ? $"First name: {firstName}."
            : $"The name has {displayName.Count(char.IsLetter)} letters.";
        return
        [
            openingClue,
            $"Also represented {clubs[1].Name}.",
            $"Also represented {clubs[2].Name}.",
            initialHint,
            firstNameHint
        ];
    }
}
