using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/import")]
public sealed class ImportController : ControllerBase
{
    private readonly IDataImportService _importService;

    public ImportController(IDataImportService importService) => _importService = importService;

    [HttpPost("seasons/{seasonExternalId}")]
    public async Task<IActionResult> ImportSeason(string seasonExternalId, CancellationToken cancellationToken)
    {
        var result = await _importService.ImportSeasonAsync(seasonExternalId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("seasons")]
    public async Task<IActionResult> ImportAllSeasons(CancellationToken cancellationToken) =>
        Ok(await _importService.ImportAllAvailableSeasonsAsync(cancellationToken));
}
