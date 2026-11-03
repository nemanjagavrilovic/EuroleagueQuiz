using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyCareerOrderService
{
    Task<DailyCareerOrderDto> StartAsync(Guid guestId, CancellationToken cancellationToken);
    Task<DailyCareerOrderDto> SaveDraftAsync(Guid guestId, DailyCareerOrderRequest request, CancellationToken cancellationToken);
    Task<DailyCareerOrderDto> SubmitAsync(Guid guestId, DailyCareerOrderRequest request, CancellationToken cancellationToken);
}
