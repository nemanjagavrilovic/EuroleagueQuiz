using System.Security.Claims;
using EuroleagueQuiz.Api.Administration;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUsersController(AdminUserService users) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool includeDeleted = true,
        CancellationToken ct = default) =>
        Ok(await users.GetPageAsync(search, page, pageSize, includeDeleted, ct));

    [HttpDelete("{id:guid}")]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ExecuteAsync(() => users.SoftDeleteAsync(CurrentUserId(), id, ct));

    [HttpPost("{id:guid}/password-reset")]
    public Task<IActionResult> SendPasswordReset(Guid id, CancellationToken ct) =>
        ExecuteAsync(() => users.SendPasswordEmailAsync(id, ct));

    [HttpPost("{id:guid}/resend-confirmation")]
    public Task<IActionResult> ResendConfirmation(Guid id, CancellationToken ct) =>
        ExecuteAsync(() => users.ResendConfirmationAsync(id, ct));

    private async Task<IActionResult> ExecuteAsync(Func<Task<string>> action)
    {
        try
        {
            return Ok(new { message = await action() });
        }
        catch (AdminUserActionException exception)
        {
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
        catch (AccountEmailDeliveryException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The email could not be sent. Please try again shortly." });
        }
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new UnauthorizedAccessException("The administrator identity is invalid.");
}
