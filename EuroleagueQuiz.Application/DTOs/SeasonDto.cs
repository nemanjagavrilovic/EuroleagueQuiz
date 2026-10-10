using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Application.DTOs;

public sealed record SeasonDto(
    long Id,
    string ExternalId,
    string Name,
    int StartYear,
    int EndYear)
{
    public static SeasonDto FromEntity(Season season) =>
        new(season.Id, season.ExternalId, season.Name, season.StartYear, season.EndYear);
}
