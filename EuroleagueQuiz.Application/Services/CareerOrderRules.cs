using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Application.Services;

public static class CareerOrderRules
{
    public const int CurrentVersion = 2;

    public static int CountCorrectTransfers(IReadOnlyList<long> submitted, IReadOnlyList<long> correctOrder)
    {
        var transfers = correctOrder.Zip(correctOrder.Skip(1)).ToHashSet();
        return submitted.Zip(submitted.Skip(1)).Count(transfers.Contains);
    }

    public static IReadOnlyList<IReadOnlyList<CareerOrderClubDto>> FindConsecutiveClubWindows(IEnumerable<PlayerTeamSeason> history)
    {
        var windows = new List<IReadOnlyList<CareerOrderClubDto>>();
        var segment = new List<CareerOrderClubDto>();
        foreach (var season in history.Where(x => string.Equals(x.Season.CompetitionCode, "E", StringComparison.OrdinalIgnoreCase))
                     .GroupBy(x => x.Season.StartYear).OrderBy(x => x.Key))
        {
            var clubs = season.Select(x => x.Team).DistinctBy(x => x.Id).ToList();
            // The feed does not reliably order transfers within a season. Start
            // a new segment rather than inventing a move across an ambiguous year.
            if (clubs.Count != 1)
            {
                segment.Clear();
                continue;
            }
            var club = clubs[0];
            if (segment.Count > 0 && segment[^1].Id == club.Id) continue;
            segment.Add(new(club.Id, club.Name, club.ImageUrl));
            if (segment.Count < 4) continue;
            var window = segment.TakeLast(4).ToList();
            // Club IDs identify the choices in both clients. Return visits stay
            // in the chronology, but duplicated choices cannot form a question.
            if (window.Select(x => x.Id).Distinct().Count() == 4) windows.Add(window);
        }
        // The question exposes player + club IDs, not stint dates. If the same
        // four clubs appear in different sequences, the answer would be ambiguous.
        return windows.GroupBy(window => string.Join(",", window.Select(x => x.Id).Order()))
            .Select(group => group.DistinctBy(window => string.Join(",", window.Select(x => x.Id))).ToList())
            .Where(group => group.Count == 1).Select(group => group[0]).ToList();
    }
}
