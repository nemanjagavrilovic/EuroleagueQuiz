using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/career-games")]
public sealed class CareerGamesController : ControllerBase
{
    private readonly ICareerGamesService _service;
    public CareerGamesController(ICareerGamesService service) => _service = service;

    [HttpPost("missing-club/start")] public async Task<IActionResult> StartMissingClub(CancellationToken ct) => Ok(await _service.StartMissingClubAsync(ct));
    [HttpPost("missing-club/guess")] public async Task<IActionResult> GuessMissingClub(MissingClubGuessRequest request, CancellationToken ct) => await Execute(() => _service.GuessMissingClubAsync(request, ct));
    [HttpPost("career-order/start")] public async Task<IActionResult> StartCareerOrder(CancellationToken ct) => Ok(await _service.StartCareerOrderAsync(ct));
    [HttpPost("career-order/guess")] public async Task<IActionResult> GuessCareerOrder(CareerOrderGuessRequest request, CancellationToken ct) => await Execute(() => _service.GuessCareerOrderAsync(request, ct));
    [HttpPost("mystery-player/start")] public async Task<IActionResult> StartMysteryPlayer(CancellationToken ct) => Ok(await _service.StartMysteryPlayerAsync(ct));
    [HttpPost("mystery-player/guess")] public async Task<IActionResult> GuessMysteryPlayer(MysteryPlayerGuessRequest request, CancellationToken ct) => await Execute(() => _service.GuessMysteryPlayerAsync(request, ct));

    private static async Task<IActionResult> Execute<T>(Func<Task<T>> action)
    {
        try { return new OkObjectResult(await action()); }
        catch (KeyNotFoundException ex) { return new NotFoundObjectResult(ex.Message); }
    }
}
