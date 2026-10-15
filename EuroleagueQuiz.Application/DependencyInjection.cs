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
        services.AddScoped<ITeamHistoryGameService, TeamHistoryGameService>();
        services.AddScoped<ICareerGamesService, CareerGamesService>();
        services.AddScoped<IFivePlayerGameService, FivePlayerGameService>();
        return services;
    }
}
