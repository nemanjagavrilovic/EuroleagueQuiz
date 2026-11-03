using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Authentication;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/career-games/career-order/daily")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyCareerOrderController(IDailyCareerOrderService service) : ControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start([FromQuery] bool ranked, CancellationToken ct) => Execute(ranked, id => service.StartAsync(id, ct));
    [HttpPost("draft")]
    public Task<IActionResult> Draft(DailyCareerOrderRequest request, [FromQuery] bool ranked, CancellationToken ct) => Execute(ranked, id => service.SaveDraftAsync(id, request, ct));
    [HttpPost("submit")]
    public Task<IActionResult> Submit(DailyCareerOrderRequest request, [FromQuery] bool ranked, CancellationToken ct) => Execute(ranked, id => service.SubmitAsync(id, request, ct));

    private async Task<IActionResult> Execute(bool ranked, Func<DailyAttemptIdentity, Task<DailyCareerOrderDto>> action)
    {
        if (!DailyIdentityResolver.TryResolve(HttpContext, ranked, out var identity, out var error)) return error!;
        try { return Ok(await action(identity)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
