using EuroleagueQuiz.Application;
using EuroleagueQuiz.Api.Authentication;
using EuroleagueQuiz.Api.Administration;
using EuroleagueQuiz.Api.Auditing;
using EuroleagueQuiz.Api.RateLimiting;
using EuroleagueQuiz.Api.Security;
using EuroleagueQuiz.Infrastructure;
using EuroleagueQuiz.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
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
builder.Services.AddScoped<AdminAuditLogService>();
builder.Services.AddScoped<AuditLogWriter>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddOptions<TurnstileOptions>()
    .Bind(builder.Configuration.GetSection(TurnstileOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.SiteKey), "Turnstile:SiteKey is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.SecretKey), "Turnstile:SecretKey is required.")
    .ValidateOnStart();
if (builder.Environment.IsDevelopment())
    builder.Services.PostConfigure<TurnstileOptions>(options =>
    {
        if (!string.IsNullOrWhiteSpace(options.SiteKey) && !string.IsNullOrWhiteSpace(options.SecretKey)) return;
        options.SiteKey = "1x00000000000000000000AA";
        options.SecretKey = "1x0000000000000000000000000000000AA";
        options.AllowedHostnames = ["example.com"];
        options.EnforceAction = false;
    });
builder.Services.AddHttpClient<TurnstileValidator>(client =>
{
    client.BaseAddress = new Uri("https://challenges.cloudflare.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

var googleOptions = builder.Configuration.GetSection(GoogleOptions.SectionName).Get<GoogleOptions>() ?? new GoogleOptions();
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
var clientBaseUrl = (builder.Configuration["Client:BaseUrl"] ?? "http://localhost:4200").TrimEnd('/');
if (!string.IsNullOrWhiteSpace(googleOptions.ClientId))
    authentication.AddJwtBearer(AuthenticationSchemes.GoogleMobile, options =>
    {
        options.MapInboundClaims = false;
        options.Authority = "https://accounts.google.com";
        options.Audience = googleOptions.ClientId;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidateAudience = true,
            ValidAudience = googleOptions.ClientId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
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
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.ContentType = "application/problem+json";
        int? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            response.Headers["Retry-After"] = retryAfterSeconds.Value.ToString();
        }

        await response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.com/429",
            title = "Too many requests",
            status = StatusCodes.Status429TooManyRequests,
            detail = "Please wait before trying this action again.",
            message = "Too many requests. Please wait before trying again.",
            retryAfterSeconds
        }, cancellationToken);
    };

    AddIpPolicy(options, RateLimitPolicies.Login, "Login", 10, TimeSpan.FromMinutes(1));
    AddIpPolicy(options, RateLimitPolicies.Registration, "Registration", 5, TimeSpan.FromHours(1));
    AddUserOrIpPolicy(options, RateLimitPolicies.AccountEmail, "AccountEmail", 3, TimeSpan.FromHours(1));
    AddIpPolicy(options, RateLimitPolicies.Contact, "Contact", 5, TimeSpan.FromMinutes(10));
    AddUserOrIpPolicy(options, RateLimitPolicies.AdminAction, "AdminAction", 20, TimeSpan.FromMinutes(1));
    AddUserOrIpPolicy(options, RateLimitPolicies.Import, "Import", 2, TimeSpan.FromHours(1));
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
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
await IdentitySeeder.SeedAsync(app.Services);
app.Run();

void AddIpPolicy(RateLimiterOptions options, string policyName, string configurationName,
    int defaultPermitLimit, TimeSpan defaultWindow) =>
    options.AddPolicy(policyName, context => RateLimitPartition.GetFixedWindowLimiter(
        $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}",
        _ => FixedWindow(configurationName, defaultPermitLimit, defaultWindow)));

void AddUserOrIpPolicy(RateLimiterOptions options, string policyName, string configurationName,
    int defaultPermitLimit, TimeSpan defaultWindow) =>
    options.AddPolicy(policyName, context =>
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var partitionKey = !string.IsNullOrWhiteSpace(userId)
            ? $"user:{userId}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey,
            _ => FixedWindow(configurationName, defaultPermitLimit, defaultWindow));
    });

FixedWindowRateLimiterOptions FixedWindow(string name, int defaultPermitLimit, TimeSpan defaultWindow) => new()
{
    PermitLimit = builder.Configuration.GetValue<int?>($"RateLimiting:{name}:PermitLimit") ?? defaultPermitLimit,
    Window = TimeSpan.FromSeconds(
        builder.Configuration.GetValue<int?>($"RateLimiting:{name}:WindowSeconds") ?? (int)defaultWindow.TotalSeconds),
    QueueLimit = 0,
    AutoReplenishment = true
};
