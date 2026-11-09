using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class ResendEmailClient(HttpClient httpClient, IOptions<ResendOptions> options, ILogger<ResendEmailClient> logger)
{
    public async Task SendAsync(string to, string subject, string text, string? replyTo, CancellationToken cancellationToken, string? html = null)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.FromAddress))
            throw new ResendEmailException("Resend API settings are incomplete.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        request.Content = JsonContent.Create(new ResendEmailRequest(
            $"{settings.FromName} <{settings.FromAddress}>", [to], subject, text, html,
            string.IsNullOrWhiteSpace(replyTo) ? null : [replyTo]));

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return;
            logger.LogError("Resend rejected an email request with HTTP {StatusCode}.", (int)response.StatusCode);
            throw new ResendEmailException($"Resend returned HTTP {(int)response.StatusCode}.");
        }
        catch (ResendEmailException) { throw; }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError("Resend email request failed ({FailureType}): {FailureReason}",
                exception.GetType().Name, exception.GetBaseException().Message);
            throw new ResendEmailException("Resend could not be reached.");
        }
    }

    private sealed record ResendEmailRequest(
        [property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("text")] string Text,
        [property: JsonPropertyName("html"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Html,
        [property: JsonPropertyName("reply_to"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? ReplyTo);
}

public sealed class ResendEmailException(string message) : Exception(message);
