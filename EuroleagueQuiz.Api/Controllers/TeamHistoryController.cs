using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/team-history")]
public sealed class TeamHistoryController : ControllerBase
{
    private readonly ITeamHistoryGameService _service;

    public TeamHistoryController(ITeamHistoryGameService service) => _service = service;

    [HttpPost("start")]
    public async Task<IActionResult> Start(CancellationToken cancellationToken) => Ok(await _service.StartAsync(cancellationToken));

    [HttpPost("guess")]
    public async Task<IActionResult> Guess([FromBody] TeamHistoryGuessRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.GuessAsync(request, cancellationToken));
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
