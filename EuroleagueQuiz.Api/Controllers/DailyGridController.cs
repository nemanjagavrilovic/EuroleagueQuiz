using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/grid/daily")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyGridController(IDailyGridService service) : ControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start([FromQuery] GridDifficulty difficulty = GridDifficulty.Normal, CancellationToken cancellationToken = default) =>
        Execute(id => service.StartAsync(id, difficulty, cancellationToken));

    [HttpPost("guess")]
    public Task<IActionResult> Guess(DailyGridGuessRequest request, CancellationToken cancellationToken) =>
        Execute(id => service.GuessAsync(id, request, cancellationToken));

    [HttpPost("place")]
    public Task<IActionResult> Place(DailyGridPlaceRequest request, CancellationToken cancellationToken) =>
        Execute(id => service.PlaceAsync(id, request, cancellationToken));

    [HttpPost("reveal")]
    public Task<IActionResult> Reveal(DailyGridRevealRequest request, CancellationToken cancellationToken) => Execute(id => service.RevealAsync(id, request, cancellationToken));

    private async Task<IActionResult> Execute(Func<Guid, Task<DailyGridAttemptDto>> action)
    {
        if (!Guid.TryParse(Request.Headers["X-Guest-Id"].ToString(), out var guestId) || guestId == Guid.Empty)
            return BadRequest(new { message = "A valid X-Guest-Id header is required." });
        try { return Ok(await action(guestId)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
