namespace EuroleagueQuiz.Application.Interfaces;

public interface IDataImportService
{
    Task<ImportResult> ImportSeasonAsync(string seasonExternalId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ImportResult>> ImportAllAvailableSeasonsAsync(CancellationToken cancellationToken);
}

public sealed record ImportResult(string SeasonExternalId, int TeamsRead, int PlayersRead, int RelationshipsCreated);
