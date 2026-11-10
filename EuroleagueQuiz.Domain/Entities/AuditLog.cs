namespace EuroleagueQuiz.Domain.Entities;

public sealed class AuditLog
{
    private AuditLog() { }

    public AuditLog(
        DateTime occurredAtUtc,
        Guid? actorUserId,
        string actorType,
        string action,
        string result,
        string? entityType,
        string? entityId,
        string? correlationId,
        string? detailsJson)
    {
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc);
        ActorUserId = actorUserId;
        ActorType = actorType;
        Action = action;
        Result = result;
        EntityType = entityType;
        EntityId = entityId;
        CorrelationId = correlationId;
        DetailsJson = detailsJson;
    }

    public long Id { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string ActorType { get; private set; } = null!;
    public string Action { get; private set; } = null!;
    public string Result { get; private set; } = null!;
    public string? EntityType { get; private set; }
    public string? EntityId { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? DetailsJson { get; private set; }
}
