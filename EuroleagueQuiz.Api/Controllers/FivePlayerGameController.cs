using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/five-player")]
public sealed class FivePlayerGameController : ControllerBase
{
    private readonly IFivePlayerGameService _service;

    public FivePlayerGameController(IFivePlayerGameService service) => _service = service;

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken) => await Execute(() => _service.StartAsync(cancellationToken));

    [HttpPost("guess")]
    public async Task<IActionResult> Guess(FivePlayerGameGuessRequest request, CancellationToken cancellationToken) => await Execute(() => _service.GuessAsync(request, cancellationToken));

    [HttpPost("next")]
    public async Task<IActionResult> Next([FromQuery] Guid roundId, CancellationToken cancellationToken) => await Execute(() => _service.NextAsync(roundId, cancellationToken));

    private static async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return new OkObjectResult(await action()); }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(ex.Message); }
        catch (ArgumentException ex) { return new BadRequestObjectResult(ex.Message); }
        catch (InvalidOperationException ex) { return new BadRequestObjectResult(ex.Message); }
    }
}
