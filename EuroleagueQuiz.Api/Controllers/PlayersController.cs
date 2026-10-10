using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/players")]
public sealed class PlayersController : ControllerBase
{
    private readonly IPlayerService _service;
    public PlayersController(IPlayerService service) => _service = service;

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
            var result = await _service.GetGridPlayerMatchAsync(request, cancellationToken);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception.Message);
        }
    }
}
