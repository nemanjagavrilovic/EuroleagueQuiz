namespace EuroleagueQuiz.Application.Interfaces;

public interface IAccountEmailSender
{
    Task SendConfirmationAsync(string email, string displayName, string confirmationUrl, CancellationToken cancellationToken);
    Task SendPasswordResetAsync(string email, string displayName, string resetUrl, CancellationToken cancellationToken);
}

public sealed class AccountEmailDeliveryException : Exception
{
    public AccountEmailDeliveryException() : base("Account email could not be sent.") { }
}
