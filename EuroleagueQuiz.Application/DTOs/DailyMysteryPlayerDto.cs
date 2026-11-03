namespace EuroleagueQuiz.Application.DTOs;

public sealed record MysteryPlayerChallengeDto(PlayerDto Player, IReadOnlyList<string> Clues);
public sealed record DailyMysteryPlayerGuessRequest(long ChallengeId, Guid CommandId, long? PlayerId, bool Skip = false);
public sealed record DailyMysteryPlayerDto(long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, MysteryPlayerRoundDto Round,
    int Score, int AvailablePoints, bool LeaderboardEligible, IReadOnlyList<long> UsedPlayerIds, string? Feedback);
