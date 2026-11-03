namespace EuroleagueQuiz.Application.DTOs;

public sealed record DailyGridGuessRequest(long ChallengeId, long PlayerId, Guid CommandId);
public sealed record DailyGridPlaceRequest(long ChallengeId, int Row, int Column);
public sealed record DailyGridRevealRequest(long ChallengeId);
public sealed record DailyGridAnswerDto(int Row, int Column, PlayerDto Player, bool Revealed);
public sealed record DailyGridAttemptDto(
    long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, GridDefinitionDto Grid,
    int Score, int GuessesUsed, bool Completed, bool Revealed,
    bool LeaderboardEligible, IReadOnlyList<DailyGridAnswerDto> Answers,
    PlayerDto? PendingPlayer, IReadOnlyList<GridCellDto> PendingCells,
    IReadOnlyList<long> UsedPlayerIds, string? Feedback);
