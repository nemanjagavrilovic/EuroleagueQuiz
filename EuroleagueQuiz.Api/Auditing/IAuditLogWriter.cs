namespace EuroleagueQuiz.Api.Auditing;

public interface IAuditLogWriter
{
    Task WriteAsync(string action, string result, string? entityType = null, string? entityId = null,
        object? details = null, Guid? actorUserId = null, string? actorType = null,
        CancellationToken cancellationToken = default);
}
