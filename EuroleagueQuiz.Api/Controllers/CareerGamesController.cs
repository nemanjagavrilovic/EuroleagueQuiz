using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Auditing;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/career-games")]
public sealed class CareerGamesController(ICareerGamesService service, PracticeGameAuditor audit) : ControllerBase
{
    [HttpPost("missing-club/start")]
    public Task<IActionResult> StartMissingClub(CancellationToken ct) => Execute(async () =>
    {
        var round = await service.StartMissingClubAsync(ct);
        await audit.StartAsync(HttpContext, "missing-club", round.RoundId);
        return round;
    });

    [HttpPost("missing-club/guess")]
    public Task<IActionResult> GuessMissingClub(MissingClubGuessRequest request, CancellationToken ct)
    {
        if (!audit.CanUse(HttpContext, "missing-club", request.RoundId)) return Task.FromResult<IActionResult>(Forbid());
        return Execute(async () =>
        {
            var round = await service.GuessMissingClubAsync(request, ct);
            await audit.CompleteAsync(HttpContext, "missing-club", round.RoundId, round.Completed, round.Correct, round.Correct == false);
            return round;
        });
    }

    [HttpPost("career-order/start")]
    public Task<IActionResult> StartCareerOrder(CancellationToken ct) => Execute(async () =>
    {
        var round = await service.StartCareerOrderAsync(ct);
        await audit.StartAsync(HttpContext, "career-order", round.RoundId);
        return round;
    });

    [HttpPost("career-order/guess")]
    public Task<IActionResult> GuessCareerOrder(CareerOrderGuessRequest request, CancellationToken ct)
    {
        if (!audit.CanUse(HttpContext, "career-order", request.RoundId)) return Task.FromResult<IActionResult>(Forbid());
        return Execute(async () =>
        {
            var round = await service.GuessCareerOrderAsync(request, ct);
            await audit.CompleteAsync(HttpContext, "career-order", round.RoundId, round.Completed, round.Correct, round.Correct == false);
            return round;
        });
    }

    [HttpPost("mystery-player/start")]
    public Task<IActionResult> StartMysteryPlayer(CancellationToken ct) => Execute(async () =>
    {
        var round = await service.StartMysteryPlayerAsync(ct);
        await audit.StartAsync(HttpContext, "mystery-player", round.RoundId);
        return round;
    });

    [HttpPost("mystery-player/guess")]
    public Task<IActionResult> GuessMysteryPlayer(MysteryPlayerGuessRequest request, CancellationToken ct)
    {
        if (!audit.CanUse(HttpContext, "mystery-player", request.RoundId)) return Task.FromResult<IActionResult>(Forbid());
        return Execute(async () =>
        {
            var round = await service.GuessMysteryPlayerAsync(request, ct);
            await audit.CompleteAsync(HttpContext, "mystery-player", round.RoundId, round.Completed, round.Correct, round.Correct == false);
            return round;
        });
    }

    private static async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return new OkObjectResult(await action()); }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(ex.Message); }
        catch (ArgumentException ex) { return new BadRequestObjectResult(ex.Message); }
    }
}
