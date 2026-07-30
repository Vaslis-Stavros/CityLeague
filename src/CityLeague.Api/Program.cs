using System.Text;
using System.Text.Json.Serialization;
using CityLeague.Api.Auth;
using CityLeague.Api.Common;
using CityLeague.Api.Hubs;
using CityLeague.Api.Services;
using CityLeague.Core.Abstractions;
using CityLeague.Infrastructure;
using CityLeague.Infrastructure.Auth;
using CityLeague.Infrastructure.Data;
using CityLeague.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient(SocialIdentityValidator.HttpClientName);
builder.Services.AddSingleton<IOpenIdMetadataProvider, OpenIdMetadataProvider>();
builder.Services.AddSingleton<SocialProviderCatalog>();
builder.Services.AddSingleton<SocialProviderDirectory>();
builder.Services.AddSingleton<IAppleClientSecretFactory, AppleClientSecretFactory>();
builder.Services.AddSingleton<ISocialIdentityValidator, SocialIdentityValidator>();
builder.Services.AddSingleton<B2CIdentityValidator>();
builder.Services.AddSingleton<DevIdentityValidator>();
builder.Services.AddSingleton<IExternalIdentityValidator, CompositeIdentityValidator>();

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

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    // App Service / Container Apps sit behind a trusted load balancer.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CityLeagueDbContext>();
    if (DependencyInjection.UsesSqlServer(app.Configuration))
        await db.Database.MigrateAsync();
    else
    {
        // EnsureCreated is a no-op once the file exists, so also create any tables added later.
        await db.Database.EnsureCreatedAsync();
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS "UserExternalLogins" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_UserExternalLogins" PRIMARY KEY,
                "UserId" TEXT NOT NULL,
                "Provider" TEXT NOT NULL,
                "Subject" TEXT NOT NULL,
                "Email" TEXT NULL,
                "CreatedAt" INTEGER NOT NULL,
                "LastLoginAt" INTEGER NOT NULL,
                CONSTRAINT "FK_UserExternalLogins_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserExternalLogins_Provider_Subject" ON "UserExternalLogins" ("Provider", "Subject");""");
        await db.Database.ExecuteSqlRawAsync(
            """CREATE INDEX IF NOT EXISTS "IX_UserExternalLogins_UserId" ON "UserExternalLogins" ("UserId");""");

        await EnsureSqliteColumnAsync(db, "Leagues", "PlannedMatchCount", "INTEGER NOT NULL DEFAULT 10");
        await EnsureSqliteColumnAsync(db, "Leagues", "StartedAt", "TEXT NULL");
        await EnsureSqliteColumnAsync(db, "LeagueTeams", "LeaderUserId", "TEXT NULL");
        await EnsureSqliteColumnAsync(db, "LeagueTeams", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LeagueParticipants_LeagueId_UserId" ON "LeagueParticipants" ("LeagueId", "UserId");""");
        await db.Database.ExecuteSqlRawAsync(
            """CREATE UNIQUE INDEX IF NOT EXISTS "IX_LeagueEvents_LeagueId_EventId" ON "LeagueEvents" ("LeagueId", "EventId");""");
    }
    await DbSeeder.EnsureSeededAsync(db, app.Services.GetRequiredService<IPasswordHasher>());
}

static async Task EnsureSqliteColumnAsync(DbContext db, string table, string column, string definition)
{
    var connection = db.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
        await connection.OpenAsync();

    await using var cmd = connection.CreateCommand();
    cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
    var exists = false;
    await using (var reader = await cmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
    }

    if (!exists)
        await db.Database.ExecuteSqlRawAsync($"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {definition}");
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseForwardedHeaders();

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
