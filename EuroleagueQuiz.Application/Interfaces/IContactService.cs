using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IContactService
{
    Task SendAsync(ContactRequest request, CancellationToken cancellationToken);
}
