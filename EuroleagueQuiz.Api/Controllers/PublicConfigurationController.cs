using EuroleagueQuiz.Api.Security;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/config/public")]
public sealed class PublicConfigurationController(
    IOptions<TurnstileOptions> turnstile,
    IDailyGridService grids,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) => Ok(new
    {
        turnstileSiteKey = turnstile.Value.SiteKey,
        googleMeasurementId = configuration["Analytics:GoogleMeasurementId"] ?? string.Empty,
        metaPixelId = configuration["Analytics:MetaPixelId"] ?? string.Empty,
        gridScoringNotice = await grids.GetScoringNoticeAsync(cancellationToken)
    });
}
