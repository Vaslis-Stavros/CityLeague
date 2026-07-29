using CityLeague.Core.Abstractions;
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

    /// <summary>Demo password used only when the <c>vaslis</c> account is created by the seeder.</summary>
    public const string VaslisDemoPassword = "vaslis123";

    public static async Task EnsureSeededAsync(
        CityLeagueDbContext db,
        IPasswordHasher? passwords = null,
        CancellationToken ct = default)
    {
        await EnsureSportsAsync(db, ct);
        await EnsureFootballFormatsAsync(db, ct);
        await EnsureVaslIsDemoContactsAsync(db, passwords, ct);
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

    /// <summary>
    /// Ensures the local account <c>vaslis</c> has 10 accepted dummy contacts for invites/testing.
    /// </summary>
    private static async Task EnsureVaslIsDemoContactsAsync(
        CityLeagueDbContext db, IPasswordHasher? passwords, CancellationToken ct)
    {
        const string ownerHandle = "vaslis";
        var owner = await db.Users.FirstOrDefaultAsync(u => u.UniqueHandle == ownerHandle, ct);
        if (owner is null)
        {
            owner = new User
            {
                B2CObjectId = $"local:{ownerHandle}",
                Email = "vaslis@cityleague.local",
                DisplayName = "VaslIs",
                UniqueHandle = ownerHandle,
                PasswordHash = passwords?.HashPassword(VaslisDemoPassword),
            };
            db.Users.Add(owner);
            await db.SaveChangesAsync(ct);
        }

        var dummies = new (string Handle, string Name)[]
        {
            ("alex_k", "Alex K"),
            ("jordan_lee", "Jordan Lee"),
            ("samira", "Samira N"),
            ("marco_r", "Marco R"),
            ("nina_p", "Nina P"),
            ("owen_b", "Owen B"),
            ("priya", "Priya S"),
            ("theo_m", "Theo M"),
            ("luna_c", "Luna C"),
            ("kai_w", "Kai W"),
        };

        foreach (var (handle, name) in dummies)
        {
            var contactUser = await db.Users.FirstOrDefaultAsync(u => u.UniqueHandle == handle, ct);
            if (contactUser is null)
            {
                contactUser = new User
                {
                    B2CObjectId = $"demo:{handle}",
                    Email = $"{handle}@cityleague.demo",
                    DisplayName = name,
                    UniqueHandle = handle,
                };
                db.Users.Add(contactUser);
                await db.SaveChangesAsync(ct);
            }

            await EnsureAcceptedEdgeAsync(db, owner.Id, contactUser.Id, ct);
            await EnsureAcceptedEdgeAsync(db, contactUser.Id, owner.Id, ct);
        }
    }

    private static async Task EnsureAcceptedEdgeAsync(
        CityLeagueDbContext db, Guid ownerId, Guid contactId, CancellationToken ct)
    {
        var edge = await db.Contacts.FirstOrDefaultAsync(
            c => c.OwnerUserId == ownerId && c.ContactUserId == contactId, ct);
        if (edge is null)
        {
            db.Contacts.Add(new Contact
            {
                OwnerUserId = ownerId,
                ContactUserId = contactId,
                Status = ContactStatus.Accepted,
            });
        }
        else if (edge.Status != ContactStatus.Accepted)
        {
            edge.Status = ContactStatus.Accepted;
        }
    }
}
