using EuroleagueQuiz.Application.DTOs;
using Microsoft.Extensions.Caching.Memory;

namespace EuroleagueQuiz.Api.Auditing;

public sealed class PracticeAuditSessions : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object gate = new();

    internal PracticeAuditSession GetOrAdd(string game, Guid roundId, Guid? ownerId, GridDefinitionDto? grid = null)
    {
        lock (gate)
        {
            if (Find(game, roundId) is { } existing) return existing;
            var session = new PracticeAuditSession(ownerId, grid);
            cache.Set((game, roundId), session, new MemoryCacheEntryOptions
                { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) });
            return session;
        }
    }

    internal PracticeAuditSession? Find(string game, Guid roundId) =>
        cache.TryGetValue((game, roundId), out PracticeAuditSession? session) ? session : null;
    public void Dispose() => cache.Dispose();
}

internal sealed class PracticeAuditSession(Guid? ownerId, GridDefinitionDto? grid)
{
    internal Guid? OwnerId { get; } = ownerId;
    internal GridDefinitionDto? Grid { get; } = grid;
    internal SemaphoreSlim Gate { get; } = new(1, 1);
    internal bool Started { get; set; }
    internal bool Completed { get; set; }
    internal Dictionary<long, IReadOnlyList<GridCellDto>> Matches { get; } = [];
}
