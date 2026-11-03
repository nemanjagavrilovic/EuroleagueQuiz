using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/career-games/career-order/daily")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DailyCareerOrderController(IDailyCareerOrderService service) : ControllerBase
{
    [HttpPost("start")]
    public Task<IActionResult> Start(CancellationToken ct) => Execute(id => service.StartAsync(id, ct));
    [HttpPost("draft")]
    public Task<IActionResult> Draft(DailyCareerOrderRequest request, CancellationToken ct) => Execute(id => service.SaveDraftAsync(id, request, ct));
    [HttpPost("submit")]
    public Task<IActionResult> Submit(DailyCareerOrderRequest request, CancellationToken ct) => Execute(id => service.SubmitAsync(id, request, ct));

    private async Task<IActionResult> Execute(Func<Guid, Task<DailyCareerOrderDto>> action)
    {
        if (!Guid.TryParse(Request.Headers["X-Guest-Id"].ToString(), out var id) || id == Guid.Empty)
            return BadRequest(new { message = "A valid X-Guest-Id header is required." });
        try { return Ok(await action(id)); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
    }
}
