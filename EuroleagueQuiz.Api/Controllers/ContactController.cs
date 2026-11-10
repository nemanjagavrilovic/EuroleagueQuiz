using System.ComponentModel.DataAnnotations;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Api.Auditing;
using EuroleagueQuiz.Api.RateLimiting;
using EuroleagueQuiz.Api.Security;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/contact")]
[EnableRateLimiting(RateLimitPolicies.Contact)]
public sealed class ContactController(IContactService service, TurnstileValidator turnstile, AuditLogWriter audit) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> Send(ContactRequest request, CancellationToken cancellationToken)
    {
        var verified = await turnstile.ValidateAsync(request.TurnstileToken, "contact",
            HttpContext.Connection.RemoteIpAddress?.ToString(), cancellationToken);
        if (!verified)
        {
            await audit.WriteAsync(AuditActions.ContactVerificationFailed, AuditResults.Denied,
                details: new { reason = "TurnstileRejected" }, cancellationToken: cancellationToken);
            return BadRequest(new { message = "Verification failed or expired. Please try again." });
        }

        try
        {
            await service.SendAsync(request, cancellationToken);
            await audit.WriteAsync(AuditActions.ContactMessageSent, AuditResults.Success,
                cancellationToken: cancellationToken);
            return Ok(new { message = "Your message has been sent. Thank you for getting in touch." });
        }
        catch (ValidationException)
        {
            await audit.WriteAsync(AuditActions.ContactValidationFailed, AuditResults.Failed,
                details: new { reason = "InvalidContactRequest" }, cancellationToken: cancellationToken);
            return Problem(statusCode: 400, title: "Please check your contact details and message.");
        }
        catch (ContactEmailDeliveryException)
        {
            await audit.WriteAsync(AuditActions.ContactDeliveryFailed, AuditResults.Failed,
                details: new { reason = "EmailDeliveryFailed" }, cancellationToken: cancellationToken);
            return Problem(statusCode: 503, title: "Email is temporarily unavailable. Please try again later.");
        }
    }
}
