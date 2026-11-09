using System.ComponentModel.DataAnnotations;
using EuroleagueQuiz.Application.DTOs;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using EuroleagueQuiz.Infrastructure.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace EuroleagueQuiz.Tests;

public sealed class ContactServiceTests
{
    [Fact]
    public async Task SendsToFixedInboxWithVisitorAsReplyToAndMessageDetails()
    {
        var sender = new FakeSender();
        var service = new ContactService(sender);
        await service.SendAsync(ValidRequest(), CancellationToken.None);

        var email = Assert.Single(sender.Messages);
        Assert.Equal("noreply@pickandroll.io", email.To);
        Assert.Equal("fan@example.com", email.ReplyTo);
        Assert.Equal("Basketball Fan", email.ReplyToName);
        Assert.Equal("[Pick & Roll Contact] Quiz issue", email.Subject);
        Assert.Contains("Please check this player.", email.Body);
        Assert.Equal("Quiz issue", email.SubmittedSubject);
        Assert.Equal("Please check this player.", email.SubmittedMessage);
    }

    [Fact]
    public async Task HoneypotDoesNotSendEmail()
    {
        var sender = new FakeSender();
        var request = ValidRequest();
        request.Website = "https://spam.example";
        await new ContactService(sender).SendAsync(request, CancellationToken.None);
        Assert.Empty(sender.Messages);
    }

    [Theory]
    [InlineData("subject")]
    [InlineData("email")]
    [InlineData("name")]
    public async Task RejectsHeaderNewlinesBeforeSending(string field)
    {
        var sender = new FakeSender();
        var request = ValidRequest();
        if (field == "subject") request.Subject = "Question\r\nBcc: unwanted@example.com";
        if (field == "email") request.Email = "fan@example.com\r\nBcc: unwanted@example.com";
        if (field == "name") request.Name = "Fan\r\nBcc: unwanted@example.com";

        await Assert.ThrowsAsync<ValidationException>(() => new ContactService(sender).SendAsync(request, CancellationToken.None));
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task RejectsWhitespaceMessageAfterTrimming()
    {
        var sender = new FakeSender();
        var request = ValidRequest();
        request.Message = new string(' ', 20);
        await Assert.ThrowsAsync<ValidationException>(() => new ContactService(sender).SendAsync(request, CancellationToken.None));
        Assert.Empty(sender.Messages);
    }

    [Fact]
    public async Task MissingCredentialsFailsWithoutAttemptingDelivery()
    {
        var sender = new SmtpContactEmailSender(Options.Create(new SmtpOptions()), NullLogger<SmtpContactEmailSender>.Instance);
        await Assert.ThrowsAsync<ContactEmailDeliveryException>(() => sender.SendAsync(
            new ContactEmail("noreply@pickandroll.io", "fan@example.com", "Fan", "Subject", "Message", "Subject", "Message"), CancellationToken.None));
    }

    [Fact]
    public async Task DeliveryFailureIsNotReportedAsSuccess()
    {
        var sender = new FakeSender { Fail = true };
        await Assert.ThrowsAsync<ContactEmailDeliveryException>(() => new ContactService(sender).SendAsync(ValidRequest(), CancellationToken.None));
    }

    private static ContactRequest ValidRequest() => new()
    {
        Name = " Basketball Fan ", Email = " fan@example.com ", Subject = " Quiz issue ", Message = " Please check this player. "
    };

    private sealed class FakeSender : IContactEmailSender
    {
        public List<ContactEmail> Messages { get; } = new();
        public bool Fail { get; init; }
        public Task SendAsync(ContactEmail email, CancellationToken cancellationToken)
        {
            if (Fail) throw new ContactEmailDeliveryException();
            Messages.Add(email);
            return Task.CompletedTask;
        }
    }
}
