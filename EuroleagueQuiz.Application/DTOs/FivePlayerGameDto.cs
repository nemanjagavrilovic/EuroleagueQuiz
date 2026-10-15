namespace EuroleagueQuiz.Application.DTOs;

public sealed record FivePlayerGameRoundDto(
    Guid RoundId,
    int RoundNumber,
    int TotalRounds,
    int SecondsPerRound,
    int SecondsRemaining,
    TeamDto Team,
    IReadOnlyList<PlayerDto> Players,
    bool Completed,
    bool? RoundCorrect,
    bool GameCompleted,
    int Score,
    bool? LastGuessCorrect);

public sealed record FivePlayerGameGuessRequest(Guid RoundId, long? PlayerId, bool Skip = false);
