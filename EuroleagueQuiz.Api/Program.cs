using EuroleagueQuiz.Application;
using EuroleagueQuiz.Infrastructure;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Shared delivery limit works behind Nginx without trusting forwarded IP headers.
    options.AddPolicy("contact", _ => RateLimitPartition.GetFixedWindowLimiter("contact", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 10,
        Window = TimeSpan.FromMinutes(1),
        QueueLimit = 0,
        AutoReplenishment = true
    }));
});
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options => options.AddPolicy("Angular", policy =>
{
    if (builder.Environment.IsDevelopment() && allowedOrigins.Length == 0)
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin();
        return;
    }

    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod();
}));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Angular");
app.UseRateLimiter();
app.MapControllers();
app.Run();
