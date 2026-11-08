using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EuroleagueQuiz.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<ITeamService, TeamService>();
        services.AddScoped<ISeasonService, SeasonService>();
        services.AddScoped<IGridService, GridService>();
        services.AddScoped<IDailyGridService, DailyGridService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ITeamHistoryGameService, TeamHistoryGameService>();
        services.AddScoped<IDailyTeamHistoryService, DailyTeamHistoryService>();
        services.AddScoped<ICareerGamesService, CareerGamesService>();
        services.AddScoped<IDailyMissingClubService, DailyMissingClubService>();
        services.AddScoped<IDailyCareerOrderService, DailyCareerOrderService>();
        services.AddScoped<IDailyMysteryPlayerService, DailyMysteryPlayerService>();
        services.AddScoped<IFivePlayerGameService, FivePlayerGameService>();
        services.AddScoped<IDailyFivePlayerService, DailyFivePlayerService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ILeaderboardService, LeaderboardService>();
        return services;
    }
}
