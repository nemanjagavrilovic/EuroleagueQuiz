namespace EuroleagueQuiz.Application.Interfaces;

public sealed record ContactEmail(
    string To,
    string ReplyTo,
    string ReplyToName,
    string Subject,
    string Body,
    string SubmittedSubject,
    string SubmittedMessage);

public interface IContactEmailSender
{
    Task SendAsync(ContactEmail email, CancellationToken cancellationToken);
}

public sealed class ContactEmailDeliveryException : Exception
{
    public ContactEmailDeliveryException() : base("Contact email could not be sent.") { }
}
