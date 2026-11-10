using System.Security.Claims;
using System.Text.Json;
using EuroleagueQuiz.Domain.Entities;
using EuroleagueQuiz.Infrastructure.Identity;
using EuroleagueQuiz.Infrastructure.Persistence;

namespace EuroleagueQuiz.Api.Auditing;

public sealed class AuditLogWriter(
    IHttpContextAccessor httpContextAccessor,
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<AuditLogWriter> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task WriteAsync(
        string action,
        string result,
        string? entityType = null,
        string? entityId = null,
        object? details = null,
        Guid? actorUserId = null,
        string? actorType = null,
        CancellationToken cancellationToken = default)
    {
        var context = httpContextAccessor.HttpContext;
        actorUserId ??= Guid.TryParse(context?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
            ? currentUserId
            : null;
        actorType ??= context?.User.IsInRole(AppRoles.Admin) == true
            ? "Admin"
            : actorUserId.HasValue ? "User" : "Anonymous";

        string? detailsJson = null;
        if (details is not null)
            detailsJson = JsonSerializer.Serialize(details, JsonOptions);

        var entry = new AuditLog(
            timeProvider.GetUtcNow().UtcDateTime,
            actorUserId,
            actorType,
            action,
            result,
            entityType,
            entityId,
            context?.TraceIdentifier,
            detailsJson);

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<EuroleagueDbContext>();
            db.AuditLogs.Add(entry);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Auditing must not turn an already-completed account or admin action
            // into an HTTP failure. The technical logger still makes the loss visible.
            logger.LogError(exception, "Audit event {AuditAction} could not be persisted.", action);
        }
    }
}
