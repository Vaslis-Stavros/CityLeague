using CityLeague.Core.Abstractions;
using CityLeague.Core.Formations;
using CityLeague.Infrastructure.Auth;
using CityLeague.Infrastructure.Data;
using CityLeague.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CityLeague.Infrastructure;

public static class DependencyInjection
{
    /// <summary>True when the app is configured to use SQL Server (otherwise SQLite for local dev).</summary>
    public static bool UsesSqlServer(IConfiguration config)
        => !string.IsNullOrWhiteSpace(config.GetConnectionString("SqlServer"));

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<CityLeagueDbContext>(options =>
        {
            var sqlServer = config.GetConnectionString("SqlServer");
            if (!string.IsNullOrWhiteSpace(sqlServer))
            {
                options.UseSqlServer(sqlServer, sql => sql.EnableRetryOnFailure());
            }
            else
            {
                var sqlite = config.GetConnectionString("Sqlite") ?? "Data Source=CityLeague.db";
                options.UseSqlite(sqlite);
            }
        });

        services.AddSingleton<IFormationProvider, FormationProvider>();

        services.Configure<JwtOptions>(config.GetSection(JwtOptions.SectionName));
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();

        services.Configure<AvatarStorageOptions>(config.GetSection(AvatarStorageOptions.SectionName));
        var provider = config.GetSection(AvatarStorageOptions.SectionName)["Provider"];
        if (string.Equals(provider, "Azure", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IAvatarStorage, AzureBlobAvatarStorage>();
        else
            services.AddSingleton<IAvatarStorage, LocalAvatarStorage>();

        return services;
    }
}
