using EuroleagueQuiz.Application;
using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Api.Administration;
using EuroleagueQuiz.Infrastructure;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var dataProtectionPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(dataProtectionPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(dataProtectionPath)).SetApplicationName("PickAndRoll");
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<ClientOptions>(builder.Configuration.GetSection("Client"));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection(GoogleOptions.SectionName));
builder.Services.Configure<IdentityBootstrapOptions>(builder.Configuration.GetSection("Identity"));

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (string.IsNullOrWhiteSpace(jwtOptions.SigningKey))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Authentication:Jwt:SigningKey must be configured in production.");
    jwtOptions.SigningKey = "Development-only-PickAndRoll-signing-key-change-me";
}
if (Encoding.UTF8.GetByteCount(jwtOptions.SigningKey) < 32)
    throw new InvalidOperationException("Authentication:Jwt:SigningKey must contain at least 32 bytes.");
builder.Services.Configure<JwtOptions>(options =>
{
    options.Issuer = jwtOptions.Issuer;
    options.Audience = jwtOptions.Audience;
    options.SigningKey = jwtOptions.SigningKey;
    options.AccessTokenMinutes = jwtOptions.AccessTokenMinutes;
    options.RefreshTokenDays = jwtOptions.RefreshTokenDays;
});
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<PasswordSetupService>();
builder.Services.AddScoped<AdminUserService>();

var authentication = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwtOptions.Issuer,
        ValidateAudience = true, ValidAudience = jwtOptions.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
        ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = System.Security.Claims.ClaimTypes.Name,
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            {
                context.Fail("The user identity is invalid.");
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<EuroleagueDbContext>();
            var active = await db.Users.AsNoTracking()
                .AnyAsync(user => user.Id == userId && !user.IsDeleted && !user.MustChangePassword, context.HttpContext.RequestAborted);
            if (!active) context.Fail("The account is no longer active.");
        }
    };
})
.AddCookie("External", options =>
{
    options.Cookie.Name = "pickandroll.external";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(10);
});
var googleOptions = builder.Configuration.GetSection(GoogleOptions.SectionName).Get<GoogleOptions>() ?? new GoogleOptions();
var clientBaseUrl = (builder.Configuration["Client:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
if (!string.IsNullOrWhiteSpace(googleOptions.ClientId) && !string.IsNullOrWhiteSpace(googleOptions.ClientSecret))
    authentication.AddGoogle("Google", options =>
    {
        options.SignInScheme = "External";
        options.ClientId = googleOptions.ClientId;
        options.ClientSecret = googleOptions.ClientSecret;
        options.CallbackPath = "/api/signin-google";
        options.Events.OnRemoteFailure = context =>
        {
            context.HandleResponse();
            var cancelled = string.Equals(context.Request.Query["error"], "access_denied", StringComparison.OrdinalIgnoreCase);
            var error = cancelled ? "google-cancelled" : "google";
            context.Response.Redirect($"{clientBaseUrl}/login?error={error}");
            return Task.CompletedTask;
        };
    });
builder.Services.AddAuthorization();
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
    options.AddPolicy("auth", _ => RateLimitPartition.GetFixedWindowLimiter("auth", _ => new FixedWindowRateLimiterOptions
    {
        PermitLimit = 30,
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

    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

var app = builder.Build();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors("Angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
await IdentitySeeder.SeedAsync(app.Services);
app.Run();
