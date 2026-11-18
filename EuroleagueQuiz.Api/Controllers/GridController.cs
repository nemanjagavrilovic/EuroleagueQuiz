using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using EuroleagueQuiz.Api.Auditing;
using Microsoft.AspNetCore.Authorization;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/grid")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class GridController : ControllerBase
{
    private readonly IGridService _service;

    private readonly PracticeGameAuditor _audit;
    public GridController(IGridService service, PracticeGameAuditor audit) { _service = service; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GridDifficulty difficulty = GridDifficulty.Normal, CancellationToken cancellationToken = default) =>
        Ok(await StartPracticeAsync(difficulty, false, cancellationToken));

    [HttpPost("change")]
    public async Task<IActionResult> Change([FromQuery] GridDifficulty difficulty = GridDifficulty.Normal, CancellationToken cancellationToken = default) =>
        Ok(await StartPracticeAsync(difficulty, true, cancellationToken));

    private async Task<GridDefinitionDto> StartPracticeAsync(GridDifficulty difficulty, bool forceNew, CancellationToken ct)
    {
        var grid = await _service.CreateGridAsync(ct, forceNew: forceNew, difficulty: difficulty);
        if (User.Identity?.IsAuthenticated != true) return grid;
        var id = Guid.NewGuid();
        await _audit.StartAsync(HttpContext, "grid", id, grid);
        return grid with { PracticeSessionId = id };
    }

    [HttpPost("reveal")]
    public async Task<IActionResult> Reveal([FromBody] GridRevealRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.PracticeSessionId is Guid id)
                _audit.ValidateGrid(HttpContext, id, request.RowTeamIds, request.ColumnTeamIds);
            var result = await _service.RevealAsync(request, cancellationToken);
            if (request.PracticeSessionId is Guid sessionId)
                await _audit.CompleteAsync(HttpContext, "grid", sessionId, true, false, true);
            return Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }

    [Authorize]
    [HttpPost("practice/{sessionId:guid}/complete")]
    public async Task<IActionResult> CompletePractice(Guid sessionId, CompletePracticeGridRequest request)
    {
        try { await _audit.CompleteGridAsync(HttpContext, sessionId, request.Answers); return NoContent(); }
        catch (ArgumentException exception) { return BadRequest(new { message = exception.Message }); }
        catch (UnauthorizedAccessException) { return Forbid(); }
        catch (KeyNotFoundException exception) { return NotFound(new { message = exception.Message }); }
    }
}
