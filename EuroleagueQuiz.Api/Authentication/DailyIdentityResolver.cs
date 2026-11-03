using System.Security.Claims;
using EuroleagueQuiz.Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace EuroleagueQuiz.Api.Authentication;

public static class DailyIdentityResolver
{
    public static bool TryResolve(HttpContext context, bool ranked, out DailyAttemptIdentity identity, out IActionResult? error)
    {
        identity = null!;
        error = null;
        if (!Guid.TryParse(context.Request.Headers["X-Guest-Id"].ToString(), out var guestId) || guestId == Guid.Empty)
        {
            error = new BadRequestObjectResult(new { message = "A valid X-Guest-Id header is required." });
            return false;
        }
        if (!ranked)
        {
            identity = new DailyAttemptIdentity(guestId, null, false);
            return true;
        }
        if (context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId) || userId == Guid.Empty)
        {
            error = new UnauthorizedObjectResult(new { message = "Sign in to play Ranked mode." });
            return false;
        }
        identity = new DailyAttemptIdentity(guestId, userId, true);
        return true;
    }
}
