using CityLeague.Core.Entities;
using CityLeague.Core.Enums;
using CityLeague.Core.Formations;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Infrastructure.Data;

/// <summary>Seeds the reference data (sports and football formats). Idempotent.</summary>
public static class DbSeeder
{
    public const int FootballSportId = 1;
    public const int PadelSportId = 2;
    public const int TennisSportId = 3;
    public const int BasketballSportId = 4;
    public const int OtherSportId = 5;

    public static async Task EnsureSeededAsync(CityLeagueDbContext db, CancellationToken ct = default)
    {
        await EnsureSportsAsync(db, ct);
        await EnsureFootballFormatsAsync(db, ct);
        await db.SaveChangesAsync(ct);
    }

    private static async Task EnsureSportsAsync(CityLeagueDbContext db, CancellationToken ct)
    {
        var existing = await db.Sports.Select(s => s.Id).ToListAsync(ct);

        var sports = new[]
        {
            new Sport { Id = FootballSportId, Key = "football", Name = "Football", Availability = SportAvailability.Enabled, SortOrder = 1 },
            new Sport { Id = PadelSportId, Key = "padel", Name = "Padel", Availability = SportAvailability.ComingSoon, SortOrder = 2 },
            new Sport { Id = TennisSportId, Key = "tennis", Name = "Tennis", Availability = SportAvailability.ComingSoon, SortOrder = 3 },
            new Sport { Id = BasketballSportId, Key = "basketball", Name = "Basketball", Availability = SportAvailability.ComingSoon, SortOrder = 4 },
            new Sport { Id = OtherSportId, Key = "other", Name = "Other", Availability = SportAvailability.ComingSoon, SortOrder = 5 },
        };

        foreach (var sport in sports.Where(s => !existing.Contains(s.Id)))
            db.Sports.Add(sport);
    }

    private static async Task EnsureFootballFormatsAsync(CityLeagueDbContext db, CancellationToken ct)
    {
        var existingKeys = await db.EventFormats
            .Where(f => f.SportId == FootballSportId)
            .Select(f => f.Key)
            .ToListAsync(ct);

        var id = 1;
        for (var perSide = 5; perSide <= 11; perSide++, id++)
        {
            var key = FormationProvider.FormatKey(perSide);
            if (existingKeys.Contains(key))
                continue;

            db.EventFormats.Add(new EventFormat
            {
                Id = id,
                SportId = FootballSportId,
                Key = key,
                Name = $"{perSide} vs {perSide}",
                PlayersPerSide = perSide,
                FormationTemplateId = key,
            });
        }
    }
}
