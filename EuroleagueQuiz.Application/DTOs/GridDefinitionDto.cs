namespace EuroleagueQuiz.Application.DTOs;

public sealed record GridDefinitionDto(
    IReadOnlyList<TeamDto> RowTeams,
    IReadOnlyList<TeamDto> ColumnTeams,
    string Difficulty);
