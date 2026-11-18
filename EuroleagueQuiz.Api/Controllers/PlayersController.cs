using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Auditing;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController : ControllerBase
{
    private readonly IPlayerService _service;
    private readonly PracticeGameAuditor _audit;
    public PlayersController(IPlayerService service, PracticeGameAuditor audit) { _service = service; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var player = await _service.GetByIdAsync(id, cancellationToken);
        return player is null ? NotFound() : Ok(player);
    }

    [HttpGet("common")]
    public async Task<IActionResult> GetCommon([FromQuery] long firstTeamId, [FromQuery] long secondTeamId, CancellationToken cancellationToken)
    {
        if (firstTeamId == secondTeamId) return BadRequest("The two teams must be different.");
        return Ok(await _service.GetCommonPlayersAsync(firstTeamId, secondTeamId, cancellationToken));
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string term, CancellationToken cancellationToken) =>
        Ok(await _service.SearchAsync(term, cancellationToken));

    [HttpPost("grid-match")]
    public async Task<IActionResult> GetGridMatch([FromBody] GridPlayerMatchRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.PracticeSessionId is Guid id)
                _audit.ValidateGrid(HttpContext, id, request.RowTeamIds, request.ColumnTeamIds);
            var result = await _service.GetGridPlayerMatchAsync(request, cancellationToken);
            if (result is not null && request.PracticeSessionId is Guid sessionId)
                await _audit.RememberGridMatchAsync(HttpContext, sessionId, result);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
