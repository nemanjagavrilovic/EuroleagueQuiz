namespace EuroleagueQuiz.Application.DTOs;

public sealed record GridPlayerMatchRequest(
    long PlayerId,
    IReadOnlyList<long> RowTeamIds,
    IReadOnlyList<long> ColumnTeamIds);

public sealed record GridCellDto(
    int Row,
    int Column,
    long RowTeamId,
    long ColumnTeamId,
    int PlayerCount);

public sealed record GridPlayerMatchResponse(
    PlayerDto Player,
    IReadOnlyList<GridCellDto> AvailableCells);
