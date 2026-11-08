using EuroleagueQuiz.Application.Interfaces;

namespace EuroleagueQuiz.Infrastructure.Email;

public sealed class ResendAccountEmailSender(ResendEmailClient client) : IAccountEmailSender
{
    public Task SendConfirmationAsync(string email, string displayName, string confirmationUrl, CancellationToken cancellationToken) =>
        SendAsync(email, "Confirm your Pick & Roll account",
            $"Hi {displayName},\n\nConfirm your email address to activate your Pick & Roll account:\n{confirmationUrl}\n\nIf you did not create this account, you can ignore this message.", cancellationToken);

    public Task SendPasswordResetAsync(string email, string displayName, string resetUrl, CancellationToken cancellationToken) =>
        SendAsync(email, "Reset your Pick & Roll password",
            $"Hi {displayName},\n\nUse this link to reset your Pick & Roll password:\n{resetUrl}\n\nIf you did not request a password reset, you can ignore this message.", cancellationToken);

    private async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        try { await client.SendAsync(email, subject, body, null, cancellationToken); }
        catch (ResendEmailException) { throw new AccountEmailDeliveryException(); }
    }

    public Task SendPasswordSetupAsync(string email, string displayName, string setupUrl, CancellationToken cancellationToken) =>
        SendAsync(email, "Create your Pick & Roll password",
            $"Hi {displayName},\n\nUse this one-time link to create a password for your existing Pick & Roll account:\n{setupUrl}\n\nYou can then sign in with your email and password or continue using Google. This does not change your Google password.\n\nIf you did not request this, ignore this message; your account will not change.", cancellationToken);
}
