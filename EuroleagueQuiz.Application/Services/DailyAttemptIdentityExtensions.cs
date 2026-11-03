using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Domain.Repositories;

namespace EuroleagueQuiz.Application.Services;

internal static class DailyAttemptIdentityExtensions
{
    public static async Task<DailyGridAttempt?> FindOrClaimAsync(this IDailyGridRepository repository, DailyAttemptIdentity identity, DateOnly date, CancellationToken ct)
    {
        var attempt = await repository.FindAttemptAsync(identity.AttemptId, date, ct);
        if (attempt is not null || !identity.EligibleForNewAttempt || identity.GuestId == identity.AttemptId) return attempt;
        attempt = await repository.FindAttemptAsync(identity.GuestId, date, ct);
        if (attempt is null) return null;
        attempt.GuestId = identity.AttemptId;
        attempt.UserId = identity.UserId;
        // A challenge first viewed as a guest never becomes ranked retroactively.
        attempt.LeaderboardEligible = false;
        return attempt;
    }

    public static async Task<DailyGameAttempt?> FindOrClaimAsync(this IDailyGameRepository repository, string game, DailyAttemptIdentity identity, DateOnly date, CancellationToken ct)
    {
        var attempt = await repository.FindAttemptAsync(game, identity.AttemptId, date, ct);
        if (attempt is not null || !identity.EligibleForNewAttempt || identity.GuestId == identity.AttemptId) return attempt;
        attempt = await repository.FindAttemptAsync(game, identity.GuestId, date, ct);
        if (attempt is null) return null;
        attempt.GuestId = identity.AttemptId;
        attempt.UserId = identity.UserId;
        attempt.LeaderboardEligible = false;
        return attempt;
    }
}
