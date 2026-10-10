using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/grid")]
public sealed class GridController : ControllerBase
{
    private readonly IGridService _service;

    public GridController(IGridService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] GridDifficulty difficulty = GridDifficulty.Normal, CancellationToken cancellationToken = default) =>
        Ok(await _service.CreateGridAsync(cancellationToken, difficulty: difficulty));

    [HttpPost("change")]
    public async Task<IActionResult> Change([FromQuery] GridDifficulty difficulty = GridDifficulty.Normal, CancellationToken cancellationToken = default) =>
        Ok(await _service.CreateGridAsync(cancellationToken, forceNew: true, difficulty: difficulty));

    [HttpPost("reveal")]
    public async Task<IActionResult> Reveal([FromBody] GridRevealRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _service.RevealAsync(request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
