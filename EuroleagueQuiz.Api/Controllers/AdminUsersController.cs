using System.Security.Claims;
using EuroleagueQuiz.Api.Administration;
using EuroleagueQuiz.Api.Auditing;
using EuroleagueQuiz.Api.RateLimiting;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = AppRoles.Admin)]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AdminUsersController(AdminUserService users, AuditLogWriter audit) : ControllerBase
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
    [EnableRateLimiting(RateLimitPolicies.AdminAction)]
    public Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        ExecuteAsync(id, AuditActions.UserSoftDeleted, () => users.SoftDeleteAsync(CurrentUserId(), id, ct), ct);

    [HttpPost("{id:guid}/password-reset")]
    [EnableRateLimiting(RateLimitPolicies.AdminAction)]
    public Task<IActionResult> SendPasswordReset(Guid id, CancellationToken ct) =>
        ExecuteAsync(id, AuditActions.TemporaryPasswordSent, () => users.SendPasswordEmailAsync(id, ct), ct);

    [HttpPost("{id:guid}/resend-confirmation")]
    [EnableRateLimiting(RateLimitPolicies.AdminAction)]
    public Task<IActionResult> ResendConfirmation(Guid id, CancellationToken ct) =>
        ExecuteAsync(id, AuditActions.ConfirmationResent, () => users.ResendConfirmationAsync(id, ct), ct);

    private async Task<IActionResult> ExecuteAsync(
        Guid targetUserId,
        string auditAction,
        Func<Task<string>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            var message = await action();
            await audit.WriteAsync(auditAction, AuditResults.Success, "User", targetUserId.ToString(),
                cancellationToken: cancellationToken);
            return Ok(new { message });
        }
        catch (AdminUserActionException exception)
        {
            await audit.WriteAsync(auditAction, AuditResults.Denied, "User", targetUserId.ToString(),
                new { statusCode = exception.StatusCode, reason = exception.Message }, cancellationToken: cancellationToken);
            return StatusCode(exception.StatusCode, new { message = exception.Message });
        }
        catch (AccountEmailDeliveryException)
        {
            await audit.WriteAsync(auditAction, AuditResults.Failed, "User", targetUserId.ToString(),
                new { reason = "EmailDeliveryFailed" }, cancellationToken: cancellationToken);
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The email could not be sent. Please try again shortly." });
        }
        catch (Exception exception)
        {
            await audit.WriteAsync(auditAction, AuditResults.Failed, "User", targetUserId.ToString(),
                new { reason = exception.GetType().Name }, cancellationToken: cancellationToken);
            throw;
        }
    }

    private Guid CurrentUserId() => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
        ? id
        : throw new UnauthorizedAccessException("The administrator identity is invalid.");
}
