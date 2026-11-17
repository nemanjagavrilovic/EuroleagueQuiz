using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Tests;

public sealed class MysteryPlayerCluesTests
{
    private static readonly TeamDto[] Clubs =
    [
        new(1, "1", "Familiar Club", null, null, null, null, null),
        new(2, "2", "Second Club", null, null, null, null, null),
        new(3, "3", "Third Club", null, null, null, null, null)
    ];

    [Fact]
    public void Nationality_and_three_clubs_lead_to_a_name_hint_without_giving_the_full_answer()
    {
        var player = new PlayerDto(1, "1", "Alex Petrović", "Alex", "Petrović", "Serbia", null, null, null);

        var clues = MysteryPlayerClues.Create(player, Clubs);

        Assert.Equal(5, clues.Distinct().Count());
        Assert.Contains("Serbia", clues[0]);
        Assert.All(Clubs, club => Assert.Contains(clues, clue => clue.Contains(club.Name)));
        Assert.Contains("Alex", clues[4]);
        Assert.Contains("Last name starts with P", clues[3]);
        Assert.DoesNotContain(clues, clue => clue.Contains(player.DisplayName));
        Assert.DoesNotContain(clues, clue => clue.Contains("database") || clue.Contains("record starts"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Unknown")]
    [InlineData("N/A")]
    [InlineData("-")]
    public void Missing_nationality_uses_club_and_name_hints_instead_of_an_unknown_clue(string? nationality)
    {
        var player = new PlayerDto(1, "1", "Alex Petrović", "Alex", "Petrović", nationality, null, null, null);

        var clues = MysteryPlayerClues.Create(player, Clubs);

        Assert.Equal(5, clues.Distinct().Count());
        Assert.Contains(Clubs[0].Name, clues[0]);
        Assert.Contains("First name: Alex", clues[4]);
        Assert.Contains("Last name starts with P", clues[3]);
        Assert.DoesNotContain(clues, clue => clue.StartsWith("Nationality:"));
    }

    [Theory]
    [InlineData("DE COLO, NANDO", null, "NANDO", "D")]
    [InlineData("Juan Carlos Navarro", "Juan Carlos", "Juan Carlos", "N")]
    [InlineData("Łukasz Żak", null, "Łukasz", "Ż")]
    public void Legacy_display_names_and_compound_given_names_produce_correct_name_hints(
        string displayName, string? firstName, string expectedFirstName, string expectedInitial)
    {
        var player = new PlayerDto(1, "1", displayName, firstName, null, "Spain", null, null, null);

        var clues = MysteryPlayerClues.Create(player, Clubs);

        Assert.Contains($"First name: {expectedFirstName}.", clues[4]);
        Assert.Contains($"Last name starts with {expectedInitial}.", clues[3]);
    }

    [Fact]
    public void A_single_word_name_still_has_five_clues_without_disclosing_the_answer()
    {
        var player = new PlayerDto(1, "1", "NENE", null, null, null, null, null, null);

        var clues = MysteryPlayerClues.Create(player, Clubs);

        Assert.Equal(5, clues.Distinct().Count());
        Assert.DoesNotContain(clues, clue => clue.Contains(player.DisplayName));
    }

    [Fact]
    public async Task Practice_and_daily_generator_prioritize_familiar_clubs_and_reveal_one_clue_at_a_time()
    {
        var player = new Player("1", "Alex Petrović");
        player.Update("Alex Petrović", "Alex", "Petrović", "Serbia", "RS", null);
        var rare = new Team("1", "Rare Club");
        var familiar = new Team("2", "Familiar Club");
        var second = new Team("3", "Second Club");
        var third = new Team("4", "Third Club");
        var relationships = new[]
        {
            Roster(player, 1, rare, 1, 2013),
            Roster(player, 1, familiar, 2, 2014),
            Roster(player, 1, second, 3, 2015),
            Roster(player, 1, third, 4, 2016),
            Roster(new Player("2", "Other Player"), 2, familiar, 2, 2014),
            Roster(new Player("3", "Third Player"), 3, familiar, 2, 2014),
            Roster(new Player("3", "Third Player"), 3, second, 3, 2015),
            Roster(new Player("4", "Fourth Player"), 4, third, 4, 2016)
        };
        var service = new CareerGamesService(new FakeRelationships(relationships));

        var definition = await service.CreateMysteryPlayerChallengeAsync(default);
        Assert.Equal(5, definition.Clues.Distinct().Count());
        Assert.Contains("Familiar Club", definition.Clues[0]);
        Assert.DoesNotContain(definition.Clues, clue => clue.Contains("Rare Club"));
        var round = await service.StartMysteryPlayerAsync(default);
        Assert.Contains("Serbia", Assert.Single(round.RevealedClues));
        Assert.Null(round.CorrectPlayer);
        var next = await service.GuessMysteryPlayerAsync(new(round.RoundId, null, true), default);
        Assert.Equal(2, next.RevealedClueCount);
        Assert.Equal(definition.Clues.Take(2), next.RevealedClues);
        Assert.Null(next.CorrectPlayer);
    }

    private static PlayerTeamSeason Roster(Player player, long playerId, Team team, long teamId, int year)
    {
        typeof(Player).GetProperty(nameof(Player.Id))!.SetValue(player, playerId);
        typeof(Team).GetProperty(nameof(Team.Id))!.SetValue(team, teamId);
        var relationship = new PlayerTeamSeason(player, team, new Season($"E{year}", year.ToString(), year, year + 1), "test");
        typeof(PlayerTeamSeason).GetProperty(nameof(PlayerTeamSeason.PlayerId))!.SetValue(relationship, playerId);
        typeof(PlayerTeamSeason).GetProperty(nameof(PlayerTeamSeason.TeamId))!.SetValue(relationship, teamId);
        typeof(PlayerTeamSeason).GetProperty(nameof(PlayerTeamSeason.SeasonId))!.SetValue(relationship, (long)year);
        return relationship;
    }

    private sealed class FakeRelationships(IReadOnlyList<PlayerTeamSeason> relationships) : IPlayerTeamSeasonRepository
    {
        public Task<IReadOnlyList<PlayerTeamSeason>> GetAllAsync(CancellationToken ct) => Task.FromResult(relationships);
        public Task<IReadOnlyList<PlayerTeamSeason>> GetForPlayerAsync(long playerId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<long>> GetTeamIdsWithAtLeastPlayersAsync(int minimumPlayers, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerTeamSeason?> FindForPlayerAndTeamAsync(long playerId, long teamId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerTeamSeason>> GetForTeamAsync(long teamId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerTeamSeason>> GetPlayersForTeamsAsync(long first, long second, CancellationToken ct) => throw new NotSupportedException();
        public Task<PlayerTeamSeason?> FindAsync(long playerId, long teamId, long seasonId, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(PlayerTeamSeason relationship, CancellationToken ct) => throw new NotSupportedException();
    }
}
