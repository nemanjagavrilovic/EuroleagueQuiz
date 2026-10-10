using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Persistence;
using EuroleagueQuiz.Infrastructure.Repositories;
using EuroleagueQuiz.Infrastructure.DataProviders.InCrowd;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EuroleagueQuiz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("SqlServer")
            ?? throw new InvalidOperationException("ConnectionStrings:SqlServer is not configured.");

        services.AddDbContext<EuroleagueDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IPlayerTeamSeasonRepository, PlayerTeamSeasonRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddHttpClient<IBasketballDataProvider, InCrowdEuroleagueDataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://feeds.incrowdsports.com/provider/euroleague-feeds/v2/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EuroleagueQuiz/1.0");
        });
        services.AddScoped<IDataImportService, InCrowdImportService>();

        return services;
    }
}
