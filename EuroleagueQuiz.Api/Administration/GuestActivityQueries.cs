using EuroleagueQuiz.Domain.Auditing;
using EuroleagueQuiz.Domain.Entities;

namespace EuroleagueQuiz.Api.Administration;

internal sealed record GuestActivityAggregate(string Key, int UniqueGuests, int GamesPlayed, int CompletedGames);

internal static class GuestActivityQueries
{
    internal static IQueryable<AuditLog> Tracked(IQueryable<AuditLog> logs)
    {
        var actions = GuestGameAudit.Games.SelectMany(x => new[] { x.StartedAction, x.CompletedAction }).ToArray();
        return logs.Where(entry => entry.ActorType == GuestGameAudit.ActorType
            && entry.EntityType == GuestGameAudit.EntityType && entry.EntityId != null
            && entry.EntityId.Length >= 37 && actions.Contains(entry.Action));
    }

    internal static IQueryable<string> UniqueGuestIds(IQueryable<AuditLog> activity) =>
        activity.Select(x => x.EntityId!.Substring(0, 36)).Distinct();

    internal static IQueryable<GuestActivityAggregate> ByGame(IQueryable<AuditLog> activity)
    {
        var completedActions = GuestGameAudit.Games.Select(x => x.CompletedAction).ToArray();
        return activity.GroupBy(x => x.Action.Replace("GuestStarted", "").Replace("GuestCompleted", ""))
            .Select(group => new GuestActivityAggregate(
                group.Key,
                group.Select(x => x.EntityId!.Substring(0, 36)).Distinct().Count(),
                group.Select(x => x.EntityId).Distinct().Count(),
                group.Where(x => completedActions.Contains(x.Action)).Select(x => x.EntityId).Distinct().Count()));
    }
}
