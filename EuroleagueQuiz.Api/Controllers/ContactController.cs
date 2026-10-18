using System.ComponentModel.DataAnnotations;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EuroleagueQuiz.Api.Controllers;

[ApiController]
[Route("api/contact")]
[EnableRateLimiting("contact")]
public sealed class ContactController(IContactService service) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(32768)]
    public async Task<IActionResult> Send(ContactRequest request, CancellationToken cancellationToken)
    {
        try
        {
            await service.SendAsync(request, cancellationToken);
            return Ok(new { message = "Your message has been sent. Thank you for getting in touch." });
        }
        catch (ValidationException)
        {
            return Problem(statusCode: 400, title: "Please check your contact details and message.");
        }
        catch (ContactEmailDeliveryException)
        {
            return Problem(statusCode: 503, title: "Email is temporarily unavailable. Please try again later.");
        }
    }
}
