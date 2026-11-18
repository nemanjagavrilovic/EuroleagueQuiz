namespace EuroleagueQuiz.Application.DTOs;

public sealed record GridRevealRequest(
    IReadOnlyList<long> RowTeamIds,
    IReadOnlyList<long> ColumnTeamIds,
    Guid? PracticeSessionId = null);

public sealed record GridRevealCellDto(
    int Row,
    int Column,
    PlayerDto? Player,
    int PlayerCount);

public sealed record GridRevealResponse(
    IReadOnlyList<GridRevealCellDto> Cells);
