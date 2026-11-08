using System.Security.Claims;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/leaderboard")]
public sealed class LeaderboardController(ILeaderboardService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string period = "today", [FromQuery] string game = "all",
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        Guid? userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsed) ? parsed : null;
        try
        {
            return Ok(await service.GetAsync(period, game, page, pageSize, userId, ct));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }
}
