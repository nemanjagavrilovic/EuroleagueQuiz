using EuroleagueQuiz.Application.DTOs;

namespace EuroleagueQuiz.Application.Interfaces;

public interface IDailyCareerOrderService
{
    Task<DailyCareerOrderDto> StartAsync(DailyAttemptIdentity identity, CancellationToken cancellationToken);
    Task<DailyCareerOrderDto> SaveDraftAsync(DailyAttemptIdentity identity, DailyCareerOrderRequest request, CancellationToken cancellationToken);
    Task<DailyCareerOrderDto> SubmitAsync(DailyAttemptIdentity identity, DailyCareerOrderRequest request, CancellationToken cancellationToken);
}
