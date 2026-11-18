using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Auditing;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/five-player")]
public sealed class FivePlayerGameController : ControllerBase
{
    private readonly IFivePlayerGameService _service;

    private readonly PracticeGameAuditor _audit;
    public FivePlayerGameController(IFivePlayerGameService service, PracticeGameAuditor audit) { _service = service; _audit = audit; }

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken) => await Execute(async () =>
    {
        var round = await _service.StartAsync(cancellationToken);
        await _audit.StartAsync(HttpContext, "five-player", round.RoundId);
        return round;
    });

    [HttpPost("guess")]
    public async Task<IActionResult> Guess(FivePlayerGameGuessRequest request, CancellationToken cancellationToken)
    {
        if (!_audit.CanUse(HttpContext, "five-player", request.RoundId)) return Forbid();
        return await Execute(async () =>
        {
            var round = await _service.GuessAsync(request, cancellationToken);
            await _audit.CompleteAsync(HttpContext, "five-player", round.RoundId, round.GameCompleted,
                correct: round.Score == round.TotalRounds * 5,
                revealed: round.Score < round.TotalRounds * 5, correctPlayers: round.Score);
            return round;
        });
    }

    [HttpPost("next")]
    public async Task<IActionResult> Next([FromQuery] Guid roundId, CancellationToken cancellationToken)
    {
        if (!_audit.CanUse(HttpContext, "five-player", roundId)) return Forbid();
        return await Execute(async () =>
        {
            var round = await _service.NextAsync(roundId, cancellationToken);
            await _audit.CompleteAsync(HttpContext, "five-player", round.RoundId, round.GameCompleted,
                correct: round.Score == round.TotalRounds * 5,
                revealed: round.Score < round.TotalRounds * 5, correctPlayers: round.Score);
            return round;
        });
    }

    private static async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return new OkObjectResult(await action()); }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(ex.Message); }
        catch (ArgumentException ex) { return new BadRequestObjectResult(ex.Message); }
        catch (InvalidOperationException ex) { return new BadRequestObjectResult(ex.Message); }
    }
}
