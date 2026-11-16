using EuroleagueQuiz.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/config/public")]
public sealed class PublicConfigurationController(
    IOptions<TurnstileOptions> turnstile,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
    public IActionResult Get() => Ok(new
    {
        turnstileSiteKey = turnstile.Value.SiteKey,
        googleMeasurementId = configuration["Analytics:GoogleMeasurementId"] ?? string.Empty,
        metaPixelId = configuration["Analytics:MetaPixelId"] ?? string.Empty
    });
}
