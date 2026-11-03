using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Application.DTOs;

public sealed record DailyAttemptIdentity(Guid GuestId, Guid? UserId, bool Ranked)
{
    public static implicit operator DailyAttemptIdentity(Guid guestId) => new(guestId, null, false);

    public Guid AttemptId => Ranked && UserId is not null ? UserId.Value : GuestId;
    public bool EligibleForNewAttempt => Ranked && UserId is not null;

    public void Initialize(DailyGridAttempt attempt)
    {
        attempt.GuestId = AttemptId;
        attempt.UserId = UserId;
        attempt.LeaderboardEligible = EligibleForNewAttempt;
    }

    public void Initialize(DailyGameAttempt attempt)
    {
        attempt.GuestId = AttemptId;
        attempt.UserId = UserId;
        attempt.LeaderboardEligible = EligibleForNewAttempt;
    }
}
