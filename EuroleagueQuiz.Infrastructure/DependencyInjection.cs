using EuroleagueQuiz.Domain.Repositories;
using EuroleagueQuiz.Application.Interfaces;
using EuroleagueQuiz.Infrastructure.Persistence;
using EuroleagueQuiz.Infrastructure.Repositories;
using EuroleagueQuiz.Infrastructure.DataProviders.InCrowd;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using EuroleagueQuiz.Infrastructure.Email;
using EuroleagueQuiz.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace EuroleagueQuiz.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<EuroleagueDbContext>(options =>
            options.UseNpgsql(connectionString, npgsqlOptions =>
                npgsqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorCodesToAdd: null)));

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddSignInManager()
            .AddEntityFrameworkStores<EuroleagueDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<ITeamRepository, TeamRepository>();
        services.AddScoped<ISeasonRepository, SeasonRepository>();
        services.AddScoped<IPlayerTeamSeasonRepository, PlayerTeamSeasonRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IDailyGridRepository, DailyGridRepository>();
        services.AddScoped<IDailyGameRepository, DailyGameRepository>();
        services.AddHttpClient<IBasketballDataProvider, InCrowdEuroleagueDataProvider>(client =>
        {
            client.BaseAddress = new Uri("https://feeds.incrowdsports.com/provider/euroleague-feeds/v2/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EuroleagueQuiz/1.0");
        });
        services.AddScoped<IDataImportService, InCrowdImportService>();
        services.AddOptions<ResendOptions>().Bind(configuration.GetSection("Resend"));
        services.AddHttpClient<ResendEmailClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });
        services.AddScoped<IContactEmailSender, ResendContactEmailSender>();
        services.AddScoped<IAccountEmailSender, ResendAccountEmailSender>();

        return services;
    }
}
