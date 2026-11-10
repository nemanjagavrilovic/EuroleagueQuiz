using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Api.Auditing;
using EuroleagueQuiz.Api.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/import")]
[EnableRateLimiting(RateLimitPolicies.Import)]
public sealed class ImportController(IDataImportService importService, AuditLogWriter audit) : ControllerBase
{
    [HttpPost("seasons/{seasonExternalId}")]
    public async Task<IActionResult> ImportSeason(string seasonExternalId, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(AuditActions.SeasonImportStarted, AuditResults.Started, "Season", seasonExternalId,
            cancellationToken: cancellationToken);
        try
        {
            var result = await importService.ImportSeasonAsync(seasonExternalId, cancellationToken);
            await audit.WriteAsync(AuditActions.SeasonImportCompleted, AuditResults.Success, "Season", seasonExternalId,
                new { result.TeamsRead, result.PlayersRead, result.RelationshipsCreated }, cancellationToken: cancellationToken);
            return Ok(result);
        }
        catch (Exception exception)
        {
            await audit.WriteAsync(AuditActions.SeasonImportFailed, AuditResults.Failed, "Season", seasonExternalId,
                new { reason = exception.GetType().Name }, cancellationToken: cancellationToken);
            throw;
        }
    }

    [HttpPost("seasons")]
    public async Task<IActionResult> ImportAllSeasons(CancellationToken cancellationToken)
    {
        await audit.WriteAsync(AuditActions.AllSeasonsImportStarted, AuditResults.Started,
            cancellationToken: cancellationToken);
        try
        {
            var results = await importService.ImportAllAvailableSeasonsAsync(cancellationToken);
            await audit.WriteAsync(AuditActions.AllSeasonsImportCompleted, AuditResults.Success,
                details: new
                {
                    seasons = results.Count,
                    teamsRead = results.Sum(result => result.TeamsRead),
                    playersRead = results.Sum(result => result.PlayersRead),
                    relationshipsCreated = results.Sum(result => result.RelationshipsCreated)
                }, cancellationToken: cancellationToken);
            return Ok(results);
        }
        catch (Exception exception)
        {
            await audit.WriteAsync(AuditActions.AllSeasonsImportFailed, AuditResults.Failed,
                details: new { reason = exception.GetType().Name }, cancellationToken: cancellationToken);
            throw;
        }
    }
}
