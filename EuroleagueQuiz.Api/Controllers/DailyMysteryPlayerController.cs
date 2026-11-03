using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/career-games/mystery-player/daily")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyMysteryPlayerController(IDailyMysteryPlayerService service) : ControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start(CancellationToken ct) => Execute(id => service.StartAsync(id, ct));
    [HttpPost("guess")]
    public Task<IActionResult> Guess(DailyMysteryPlayerGuessRequest request, CancellationToken ct) => Execute(id => service.GuessAsync(id, request, ct));

    private async Task<IActionResult> Execute(Func<Guid, Task<DailyMysteryPlayerDto>> action)
    {
        if (!Guid.TryParse(Request.Headers["X-Guest-Id"].ToString(), out var id) || id == Guid.Empty)
            return BadRequest(new { message = "A valid X-Guest-Id header is required." });
        try { return Ok(await action(id)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
