namespace EuroleagueQuiz.Application.DTOs;

public sealed record TeamHistoryChallengeDto(PlayerDto Player, IReadOnlyList<TeamDto> Teams);
public sealed record DailyTeamHistoryGuessRequest(long ChallengeId, Guid CommandId, long? PlayerId, bool Skip = false);
public sealed record DailyTeamHistoryDto(
    long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, TeamHistoryRoundDto Round,
    int Score, int AvailablePoints, bool LeaderboardEligible, IReadOnlyList<long> UsedPlayerIds, string? Feedback);
