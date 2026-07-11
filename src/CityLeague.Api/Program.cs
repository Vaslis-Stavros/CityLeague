using System.Text;
using System.Text.Json.Serialization;
using CityLeague.Api.Auth;
using CityLeague.Api.Common;
using CityLeague.Api.Hubs;
using CityLeague.Api.Services;
using CityLeague.Infrastructure;
using CityLeague.Infrastructure.Auth;
using CityLeague.Infrastructure.Data;
using CityLeague.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
var authMode = builder.Configuration.GetSection(AuthOptions.SectionName)["Mode"] ?? "Dev";
if (string.Equals(authMode, "B2C", StringComparison.OrdinalIgnoreCase))
    builder.Services.AddSingleton<IExternalIdentityValidator, B2CIdentityValidator>();
else
    builder.Services.AddSingleton<IExternalIdentityValidator, DevIdentityValidator>();

// Ensure local avatars are written under the served web root.
builder.Services.PostConfigure<AvatarStorageOptions>(o =>
{
    if (string.Equals(o.Provider, "Azure", StringComparison.OrdinalIgnoreCase)) return;
    o.LocalRootPath ??= Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "uploads");
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddScoped<ApiMapper>();
builder.Services.AddScoped<UserProvisioningService>();
builder.Services.AddScoped<LocalAuthService>();
builder.Services.AddScoped<EventService>();
builder.Services.AddScoped<LeagueService>();

builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.SetIsOriginAllowed(_ => true).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CityLeagueDbContext>();
    if (DependencyInjection.UsesSqlServer(app.Configuration))
        await db.Database.MigrateAsync();
    else
        await db.Database.EnsureCreatedAsync();
    await DbSeeder.EnsureSeededAsync(db);
}

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<EventHub>("/hubs/events");
app.MapGet("/", () => Results.Ok(new { service = "CityLeague API", status = "ok" }));

app.Run();

/// <summary>Exposed for integration tests via WebApplicationFactory.</summary>
public partial class Program;
