using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Tests;

public sealed class CareerOrderRulesTests
{
    [Fact]
    public void All_permutations_score_only_true_directed_neighboring_moves()
    {
        long[] correct = [1, 2, 3, 4];
        foreach (var a in correct)
        foreach (var b in correct.Where(x => x != a))
        foreach (var c in correct.Where(x => x != a && x != b))
        {
            var submitted = new[] { a, b, c, correct.Single(x => x != a && x != b && x != c) };
            var expected = Enumerable.Range(0, 3).Count(i => submitted[i + 1] == submitted[i] + 1);
            Assert.Equal(expected, CareerOrderRules.CountCorrectTransfers(submitted, correct));
        }
    }

    [Fact]
    public void Questions_are_contiguous_windows_without_skipping_recorded_clubs()
    {
        var windows = CareerOrderRules.FindConsecutiveClubWindows(History(1, 2, 3, 4, 5).Reverse());
        Assert.Equal(2, windows.Count);
        Assert.Equal(new long[] { 1, 2, 3, 4 }, windows[0].Select(x => x.Id));
        Assert.Equal(new long[] { 2, 3, 4, 5 }, windows[1].Select(x => x.Id));
    }

    [Fact]
    public void Consecutive_seasons_at_one_club_form_one_spell_but_return_visits_remain()
    {
        var windows = CareerOrderRules.FindConsecutiveClubWindows(History(1, 1, 2, 1, 3, 4, 4));
        var window = Assert.Single(windows);
        Assert.Equal(new long[] { 2, 1, 3, 4 }, window.Select(x => x.Id));
        Assert.Empty(CareerOrderRules.FindConsecutiveClubWindows(History(1, 2, 1, 3)));
    }

    [Fact]
    public void Ambiguous_same_season_moves_break_the_sequence_instead_of_inventing_transfers()
    {
        var history = History(1, 2, 3, 4, 5, 6, 7, 8).ToList();
        history.Add(Row(99, 2016));
        var windows = CareerOrderRules.FindConsecutiveClubWindows(history);
        var window = Assert.Single(windows);
        Assert.Equal(new long[] { 5, 6, 7, 8 }, window.Select(x => x.Id));
    }

    [Fact]
    public void Duplicate_roster_records_are_not_transfers_and_other_competitions_are_excluded()
    {
        var history = History(1, 2, 3, 4).ToList();
        history.Add(Row(2, 2014));
        history.Add(Row(99, 2014, "U"));
        Assert.Equal(new long[] { 1, 2, 3, 4 }, Assert.Single(
            CareerOrderRules.FindConsecutiveClubWindows(history)).Select(x => x.Id));
    }

    [Fact]
    public void Fewer_than_four_known_club_spells_cannot_make_a_question()
    {
        Assert.Empty(CareerOrderRules.FindConsecutiveClubWindows(History(1, 1, 2, 3)));
    }

    [Fact]
    public void Return_visits_cannot_offer_two_valid_orders_for_the_same_four_choices()
    {
        var window = Assert.Single(CareerOrderRules.FindConsecutiveClubWindows(History(1, 2, 3, 4, 1, 5)));
        Assert.Equal(new long[] { 3, 4, 1, 5 }, window.Select(x => x.Id));
        Assert.Empty(CareerOrderRules.FindConsecutiveClubWindows(History(1, 2, 3, 4, 1)));
    }

    private static IEnumerable<PlayerTeamSeason> History(params long[] clubs) =>
        clubs.Select((club, index) => Row(club, 2013 + index));

    private static PlayerTeamSeason Row(long clubId, int year, string competition = "E")
    {
        var team = new Team(clubId.ToString(), $"Club {clubId}");
        typeof(Team).GetProperty(nameof(Team.Id))!.SetValue(team, clubId);
        return new(new Player("42", "Test Player"), team, new Season($"{competition}{year}", $"{year}-{year + 1}", year, year + 1, competition), "test");
    }
}
