namespace EuroleagueQuiz.Application.DTOs;

public sealed record CareerOrderChallengeDto(PlayerDto Player, IReadOnlyList<CareerOrderClubDto> Clubs, IReadOnlyList<CareerOrderClubDto> CorrectOrder);
public sealed record DailyCareerOrderRequest(long ChallengeId, Guid CommandId, IReadOnlyList<long>? TeamIds);
public sealed record DailyCareerOrderDto(long ChallengeId, DateOnly Date, DateTime ResetsAtUtc, CareerOrderRoundDto Round,
    int Score, int? CorrectPairs, int TotalPairs, bool LeaderboardEligible);
