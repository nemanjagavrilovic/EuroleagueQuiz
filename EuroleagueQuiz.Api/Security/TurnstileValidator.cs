using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Api.Security;

public sealed class TurnstileValidator(
    HttpClient client,
    IOptions<TurnstileOptions> options,
    ILogger<TurnstileValidator> logger)
{
    public async Task<bool> ValidateAsync(string? token, string expectedAction, string? remoteIp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048) return false;

        try
        {
            using var response = await client.PostAsJsonAsync("turnstile/v0/siteverify", new
            {
                secret = options.Value.SecretKey,
                response = token,
                remoteip = remoteIp
            }, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Turnstile Siteverify returned HTTP {StatusCode}.", (int)response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileResponse>(cancellationToken: cancellationToken);
            if (result is null || !result.Success)
            {
                logger.LogInformation("Turnstile rejected a contact request. Errors: {Errors}",
                    result?.ErrorCodes is { Length: > 0 } errors ? string.Join(",", errors) : "none");
                return false;
            }

            if (options.Value.EnforceAction &&
                !string.Equals(result.Action, expectedAction, StringComparison.Ordinal))
            {
                logger.LogWarning("Turnstile action mismatch. Expected {ExpectedAction}, received {ActualAction}.",
                    expectedAction, result.Action ?? "none");
                return false;
            }

            if (options.Value.AllowedHostnames.Length > 0 &&
                !options.Value.AllowedHostnames.Contains(result.Hostname, StringComparer.OrdinalIgnoreCase))
            {
                logger.LogWarning("Turnstile hostname {Hostname} is not allowed.", result.Hostname ?? "none");
                return false;
            }

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogError(exception, "Turnstile validation could not be completed.");
            return false;
        }
    }

    private sealed class TurnstileResponse
    {
        [JsonPropertyName("success")] public bool Success { get; init; }
        [JsonPropertyName("hostname")] public string? Hostname { get; init; }
        [JsonPropertyName("action")] public string? Action { get; init; }
        [JsonPropertyName("error-codes")] public string[] ErrorCodes { get; init; } = [];
    }
}
