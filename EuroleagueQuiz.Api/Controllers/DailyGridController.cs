using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Authentication;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/grid/daily")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyGridController(IDailyGridService service) : ControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start([FromQuery] GridDifficulty difficulty = GridDifficulty.Normal, [FromQuery] bool ranked = false, CancellationToken cancellationToken = default) =>
        Execute(ranked, id => service.StartAsync(id, difficulty, cancellationToken));

    [HttpPost("guess")]
    public Task<IActionResult> Guess(DailyGridGuessRequest request, [FromQuery] bool ranked, CancellationToken cancellationToken) =>
        Execute(ranked, id => service.GuessAsync(id, request, cancellationToken));

    [HttpPost("place")]
    public Task<IActionResult> Place(DailyGridPlaceRequest request, [FromQuery] bool ranked, CancellationToken cancellationToken) =>
        Execute(ranked, id => service.PlaceAsync(id, request, cancellationToken));

    [HttpPost("reveal")]
    public Task<IActionResult> Reveal(DailyGridRevealRequest request, [FromQuery] bool ranked, CancellationToken cancellationToken) => Execute(ranked, id => service.RevealAsync(id, request, cancellationToken));

    private async Task<IActionResult> Execute(bool ranked, Func<DailyAttemptIdentity, Task<DailyGridAttemptDto>> action)
    {
        if (!DailyIdentityResolver.TryResolve(HttpContext, ranked, out var identity, out var error)) return error!;
        try { return Ok(await action(identity)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
