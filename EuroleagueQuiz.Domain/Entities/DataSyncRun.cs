namespace EuroleagueQuiz.Domain.Entities;

public sealed class DataSyncRun
{
    private DataSyncRun() { }

    public DataSyncRun(string provider, long? seasonId)
    {
        Provider = provider;
        SeasonId = seasonId;
        StartedAtUtc = DateTime.UtcNow;
        Status = "Running";
    }

    public long Id { get; private set; }
    public string Provider { get; private set; } = null!;
    public long? SeasonId { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? FinishedAtUtc { get; private set; }
    public string Status { get; private set; } = null!;
    public int RecordsRead { get; private set; }
    public int RecordsCreated { get; private set; }
    public int RecordsUpdated { get; private set; }
    public string? ErrorMessage { get; private set; }

    public void Complete(int recordsRead, int recordsCreated, int recordsUpdated)
    {
        RecordsRead = recordsRead;
        RecordsCreated = recordsCreated;
        RecordsUpdated = recordsUpdated;
        Status = "Completed";
        FinishedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string errorMessage)
    {
        Status = "Failed";
        ErrorMessage = errorMessage;
        FinishedAtUtc = DateTime.UtcNow;
    }
}
