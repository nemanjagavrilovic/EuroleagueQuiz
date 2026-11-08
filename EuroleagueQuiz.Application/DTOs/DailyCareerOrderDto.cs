namespace EuroleagueQuiz.Application.DTOs;

public sealed record CareerOrderChallengeDto(PlayerDto Player, IReadOnlyList<CareerOrderClubDto> Clubs,
    IReadOnlyList<CareerOrderClubDto> CorrectOrder, int HistoryVersion = 1);
public sealed record DailyCareerOrderRequest(long ChallengeId, Guid CommandId, IReadOnlyList<long>? TeamIds);
public sealed record DailyCareerOrderDto(long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, CareerOrderRoundDto Round,
    int Score, int? CorrectTransfers, int TotalTransfers, bool LeaderboardEligible, int ScoringVersion);
