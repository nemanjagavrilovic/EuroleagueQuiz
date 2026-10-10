using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/seasons")]
public sealed class SeasonsController : ControllerBase
{
    private readonly ISeasonService _service;
    public SeasonsController(ISeasonService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken) => Ok(await _service.GetAllAsync(cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> GetById(long id, CancellationToken cancellationToken)
    {
        var season = await _service.GetByIdAsync(id, cancellationToken);
        return season is null ? NotFound() : Ok(season);
    }
}
