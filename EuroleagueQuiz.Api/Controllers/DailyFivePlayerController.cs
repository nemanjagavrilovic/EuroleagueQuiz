using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Authentication;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/five-player/daily")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyFivePlayerController(IDailyFivePlayerService service) : ControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start([FromQuery] bool ranked, CancellationToken ct) => Execute(ranked, id => service.StartAsync(id, ct));
    [HttpPost("guess")]
    public Task<IActionResult> Guess(DailyFivePlayerRequest request, [FromQuery] bool ranked, CancellationToken ct) => Execute(ranked, id => service.GuessAsync(id, request, ct));
    [HttpPost("next")]
    public Task<IActionResult> Next(DailyFivePlayerRequest request, [FromQuery] bool ranked, CancellationToken ct) => Execute(ranked, id => service.NextAsync(id, request, ct));
    private async Task<IActionResult> Execute(bool ranked, Func<DailyAttemptIdentity, Task<DailyFivePlayerDto>> action)
    {
        if (!DailyIdentityResolver.TryResolve(HttpContext, ranked, out var identity, out var error)) return error!;
        try { return Ok(await action(identity)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
