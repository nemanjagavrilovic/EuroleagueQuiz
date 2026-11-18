using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Auditing;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/team-history")]
public sealed class TeamHistoryController : ControllerBase
{
    private readonly ITeamHistoryGameService _service;

    private readonly PracticeGameAuditor _audit;
    public TeamHistoryController(ITeamHistoryGameService service, PracticeGameAuditor audit) { _service = service; _audit = audit; }

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken)
    {
        var round = await _service.StartAsync(cancellationToken);
        await _audit.StartAsync(HttpContext, "team-history", round.RoundId);
        return Ok(round);
    }

    [HttpPost("guess")]
    public async Task<IActionResult> Guess([FromBody] TeamHistoryGuessRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!_audit.CanUse(HttpContext, "team-history", request.RoundId)) return Forbid();
            var round = await _service.GuessAsync(request, cancellationToken);
            await _audit.CompleteAsync(HttpContext, "team-history", round.RoundId, round.Completed, round.Correct, round.Correct == false);
            return Ok(round);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
