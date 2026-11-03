namespace EuroleagueQuiz.Application.DTOs;

public sealed record FivePlayerClubChallengeDto(TeamDto Team, IReadOnlyList<PlayerDto> Players);
public sealed record FivePlayerChallengeDto(IReadOnlyList<FivePlayerClubChallengeDto> Clubs);
public sealed record DailyFivePlayerRequest(long ChallengeId, int RoundNumber, Guid CommandId, long? PlayerId = null, bool Skip = false);
public sealed record DailyFivePlayerDto(long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, FivePlayerGameRoundDto Round,
    int Score, int CorrectGuesses, bool LeaderboardEligible, DateTime ServerNowUtc, DateTime? DeadlineUtc,
    IReadOnlyList<long> RevealedPlayerIds, string? Feedback);
