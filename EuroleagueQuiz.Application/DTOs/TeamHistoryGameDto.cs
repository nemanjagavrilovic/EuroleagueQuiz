namespace EuroleagueQuiz.Application.DTOs;

public sealed record TeamHistoryRoundDto(
    Guid RoundId,
    IReadOnlyList<TeamDto> RevealedTeams,
    int RevealedTeamCount,
    int TotalTeams,
    bool Completed,
    bool? Correct,
    int Attempts,
    PlayerDto? CorrectPlayer);

public sealed record TeamHistoryGuessRequest(
    Guid RoundId,
    long? PlayerId,
    bool Skip = false);
