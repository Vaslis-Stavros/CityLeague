using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CityLeague.Infrastructure.Data;

/// <summary>
/// Enables `dotnet ef migrations` against SQL Server (the production target) without a live DB.
/// The connection string is only used for scaffolding the migration, not for connecting.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CityLeagueDbContext>
{
    public CityLeagueDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<CityLeagueDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=CityLeague;Trusted_Connection=True;")
            .Options;
        return new CityLeagueDbContext(options);
    }
}
