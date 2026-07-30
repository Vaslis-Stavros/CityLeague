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

    private const string DemoMarkerPrefix = "[demo]";

    public static async Task EnsureSeededAsync(
        CityLeagueDbContext db,
        IPasswordHasher? passwords = null,
        CancellationToken ct = default)
    {
        await EnsureSportsAsync(db, ct);
        await EnsureFootballFormatsAsync(db, ct);
        await EnsureVaslIsDemoContactsAsync(db, passwords, ct);
        await EnsureVaslIsDemoLifecycleAsync(db, ct);
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

        var dummies = new (string Handle, string Name, string AvatarUrl)[]
        {
            ("alex_k", "Alex K", "https://api.dicebear.com/7.x/avataaars/png?seed=alex_k&size=128"),
            ("jordan_lee", "Jordan Lee", "https://api.dicebear.com/7.x/avataaars/png?seed=jordan_lee&size=128"),
            ("samira", "Samira N", "https://api.dicebear.com/7.x/avataaars/png?seed=samira&size=128"),
            ("marco_r", "Marco R", "https://api.dicebear.com/7.x/avataaars/png?seed=marco_r&size=128"),
            ("nina_p", "Nina P", "https://api.dicebear.com/7.x/avataaars/png?seed=nina_p&size=128"),
            ("owen_b", "Owen B", "https://api.dicebear.com/7.x/avataaars/png?seed=owen_b&size=128"),
            ("priya", "Priya S", "https://api.dicebear.com/7.x/avataaars/png?seed=priya&size=128"),
            ("theo_m", "Theo M", "https://api.dicebear.com/7.x/avataaars/png?seed=theo_m&size=128"),
            ("luna_c", "Luna C", "https://api.dicebear.com/7.x/avataaars/png?seed=luna_c&size=128"),
            ("kai_w", "Kai W", "https://api.dicebear.com/7.x/avataaars/png?seed=kai_w&size=128"),
        };

        foreach (var (handle, name, avatarUrl) in dummies)
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
                    AvatarBlobUrl = avatarUrl,
                };
                db.Users.Add(contactUser);
                await db.SaveChangesAsync(ct);
            }
            else if (contactUser.B2CObjectId?.StartsWith("demo:", StringComparison.Ordinal) == true
                     || string.IsNullOrWhiteSpace(contactUser.AvatarBlobUrl)
                     || contactUser.AvatarBlobUrl.Contains("pravatar.cc", StringComparison.OrdinalIgnoreCase)
                     || contactUser.AvatarBlobUrl.Contains("dicebear.com", StringComparison.OrdinalIgnoreCase))
            {
                // Keep demo faces up to date for invite-panel testing.
                contactUser.AvatarBlobUrl = avatarUrl;
            }

            await EnsureAcceptedEdgeAsync(db, owner.Id, contactUser.Id, ct);
            await EnsureAcceptedEdgeAsync(db, contactUser.Id, owner.Id, ct);
        }
    }

    /// <summary>
    /// Seeds sample matches and a finished league for <c>vaslis</c> so Home / History show every lifecycle state.
    /// Idempotent via title prefix <c>[demo]</c>.
    /// </summary>
    private static async Task EnsureVaslIsDemoLifecycleAsync(CityLeagueDbContext db, CancellationToken ct)
    {
        var owner = await db.Users.FirstOrDefaultAsync(u => u.UniqueHandle == "vaslis", ct);
        if (owner is null) return;

        var format = await db.EventFormats.FirstOrDefaultAsync(f => f.Key == FormationProvider.FormatKey(5), ct)
            ?? await db.EventFormats.FirstOrDefaultAsync(f => f.SportId == FootballSportId, ct);
        if (format is null) return;

        var contacts = await db.Users
            .Where(u => u.UniqueHandle != null && u.UniqueHandle != "vaslis" && u.B2CObjectId!.StartsWith("demo:"))
            .OrderBy(u => u.UniqueHandle)
            .Take(10)
            .ToListAsync(ct);
        if (contacts.Count == 0) return;

        var formations = new FormationProvider();
        var now = DateTimeOffset.UtcNow;
        var roster = new[] { owner }.Concat(contacts).Take(10).ToList();

        await EnsureDemoEventAsync(db, formations, owner, format, roster, ct,
            title: $"{DemoMarkerPrefix} Friday night kickabout",
            location: "Central Park Pitch",
            scheduledAt: now.AddDays(2).Date.AddHours(18),
            status: EventStatus.Open,
            fillRoster: false,
            homeScore: null,
            awayScore: null);

        await EnsureDemoEventAsync(db, formations, owner, format, roster, ct,
            title: $"{DemoMarkerPrefix} Incomplete Sunday game",
            location: "Riverside Turf",
            scheduledAt: now.AddDays(-2).Date.AddHours(17),
            status: EventStatus.Incomplete,
            fillRoster: false,
            homeScore: null,
            awayScore: null);

        await EnsureDemoEventAsync(db, formations, owner, format, roster, ct,
            title: $"{DemoMarkerPrefix} Pending result — lock night",
            location: "City Arena",
            scheduledAt: now.AddHours(-6),
            status: EventStatus.Locked,
            fillRoster: true,
            homeScore: null,
            awayScore: null);

        await EnsureDemoEventAsync(db, formations, owner, format, roster, ct,
            title: $"{DemoMarkerPrefix} Derby win",
            location: "North End",
            scheduledAt: now.AddDays(-10).Date.AddHours(19),
            status: EventStatus.Completed,
            fillRoster: true,
            homeScore: 3,
            awayScore: 1);

        await EnsureDemoEventAsync(db, formations, owner, format, roster, ct,
            title: $"{DemoMarkerPrefix} Midweek draw",
            location: "West Field",
            scheduledAt: now.AddDays(-18).Date.AddHours(20),
            status: EventStatus.Completed,
            fillRoster: true,
            homeScore: 2,
            awayScore: 2);

        await EnsureDemoCompletedLeagueAsync(db, formations, owner, format, roster, now, ct);
    }

    private static async Task<Event?> EnsureDemoEventAsync(
        CityLeagueDbContext db,
        FormationProvider formations,
        User owner,
        EventFormat format,
        IReadOnlyList<User> roster,
        CancellationToken ct,
        string title,
        string location,
        DateTimeOffset scheduledAt,
        EventStatus status,
        bool fillRoster,
        int? homeScore,
        int? awayScore)
    {
        var existing = await db.Events.FirstOrDefaultAsync(e => e.OwnerUserId == owner.Id && e.Title == title, ct);
        if (existing is not null)
            return existing;

        var template = formations.GetTemplate(format.FormationTemplateId);
        var ev = new Event
        {
            OwnerUserId = owner.Id,
            SportId = format.SportId,
            EventFormatId = format.Id,
            Title = title,
            ScheduledAt = scheduledAt,
            Location = location,
            Status = status,
            CreatedAt = scheduledAt.AddDays(-3),
        };
        db.Events.Add(ev);

        foreach (var user in roster.Take(Math.Min(roster.Count, Math.Max(4, template.Slots.Count))))
        {
            db.EventParticipants.Add(new EventParticipant
            {
                EventId = ev.Id,
                UserId = user.Id,
                InvitedByUserId = user.Id == owner.Id ? null : owner.Id,
                CanInvite = true,
            });
        }

        var claimUsers = fillRoster
            ? roster.Take(template.Slots.Count).ToList()
            : roster.Take(Math.Min(4, template.Slots.Count)).ToList();

        for (var i = 0; i < template.Slots.Count; i++)
        {
            var slot = template.Slots[i];
            Guid? userId = null;
            DateTimeOffset? claimedAt = null;
            if (i < claimUsers.Count && (fillRoster || i < 4))
            {
                userId = claimUsers[i].Id;
                claimedAt = scheduledAt.AddHours(-2);
            }

            db.EventPositions.Add(new EventPosition
            {
                EventId = ev.Id,
                SlotId = slot.SlotId,
                Label = slot.Label,
                Side = slot.Side,
                X = slot.X,
                Y = slot.Y,
                UserId = userId,
                ClaimedAt = claimedAt,
            });
        }

        if (status == EventStatus.Completed && homeScore is int hs && awayScore is int aws)
        {
            var winning = hs > aws ? WinningSide.Home
                : aws > hs ? WinningSide.Away
                : WinningSide.Draw;

            var result = new EventResult
            {
                EventId = ev.Id,
                HomeScore = hs,
                AwayScore = aws,
                WinningSide = winning,
                SubmittedAt = scheduledAt.AddHours(2),
            };
            db.EventResults.Add(result);

            for (var i = 0; i < claimUsers.Count && i < template.Slots.Count; i++)
            {
                var side = template.Slots[i].Side;
                result.Roster.Add(new EventResultRoster
                {
                    UserId = claimUsers[i].Id,
                    Side = side,
                });
                await BumpPlayerStatsAsync(db, claimUsers[i].Id, format.SportId, side, winning, ct);
            }
        }

        return ev;
    }

    private static async Task EnsureDemoCompletedLeagueAsync(
        CityLeagueDbContext db,
        FormationProvider formations,
        User owner,
        EventFormat format,
        IReadOnlyList<User> roster,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var leagueName = $"{DemoMarkerPrefix} Summer Cup";
        if (await db.Leagues.AnyAsync(l => l.OwnerUserId == owner.Id && l.Name == leagueName, ct))
            return;

        var teamAPlayers = roster.Take(5).ToList();
        var teamBPlayers = roster.Skip(5).Take(5).ToList();
        if (teamBPlayers.Count < 3)
            return;

        var league = new League
        {
            Name = leagueName,
            OwnerUserId = owner.Id,
            SportId = FootballSportId,
            Status = LeagueStatus.Terminated,
            PlannedMatchCount = 2,
            StartedAt = now.AddDays(-30),
            CreatedAt = now.AddDays(-35),
        };
        db.Leagues.Add(league);

        var teamA = new LeagueTeam
        {
            LeagueId = league.Id,
            Name = "North United",
            SortOrder = 0,
            LeaderUserId = teamAPlayers[0].Id,
        };
        var teamB = new LeagueTeam
        {
            LeagueId = league.Id,
            Name = "South Rovers",
            SortOrder = 1,
            LeaderUserId = teamBPlayers[0].Id,
        };
        db.LeagueTeams.Add(teamA);
        db.LeagueTeams.Add(teamB);
        db.TeamSportStats.Add(new TeamSportStats { LeagueTeamId = teamA.Id, Played = 2, Wins = 1, Losses = 0, Draws = 1 });
        db.TeamSportStats.Add(new TeamSportStats { LeagueTeamId = teamB.Id, Played = 2, Wins = 0, Losses = 1, Draws = 1 });

        foreach (var user in teamAPlayers)
            db.LeagueParticipants.Add(new LeagueParticipant { LeagueId = league.Id, UserId = user.Id, LeagueTeamId = teamA.Id });
        foreach (var user in teamBPlayers)
            db.LeagueParticipants.Add(new LeagueParticipant { LeagueId = league.Id, UserId = user.Id, LeagueTeamId = teamB.Id });

        var match = await EnsureDemoEventAsync(db, formations, owner, format, roster, ct,
            title: $"{DemoMarkerPrefix} Summer Cup final",
            location: "Cup Stadium",
            scheduledAt: now.AddDays(-5).Date.AddHours(16),
            status: EventStatus.Completed,
            fillRoster: true,
            homeScore: 2,
            awayScore: 0);

        if (match is not null)
            db.LeagueEvents.Add(new LeagueEvent { LeagueId = league.Id, EventId = match.Id });
    }

    private static async Task BumpPlayerStatsAsync(
        CityLeagueDbContext db, Guid userId, int sportId, MatchSide side, WinningSide winning, CancellationToken ct)
    {
        var stats = db.PlayerSportStats.Local.FirstOrDefault(s => s.UserId == userId && s.SportId == sportId)
            ?? await db.PlayerSportStats.FirstOrDefaultAsync(s => s.UserId == userId && s.SportId == sportId, ct);
        if (stats is null)
        {
            stats = new PlayerSportStats { UserId = userId, SportId = sportId };
            db.PlayerSportStats.Add(stats);
        }

        stats.Played++;
        if (winning == WinningSide.Draw)
            stats.Draws++;
        else if ((winning == WinningSide.Home && side == MatchSide.Home) ||
                 (winning == WinningSide.Away && side == MatchSide.Away))
            stats.Wins++;
        else
            stats.Losses++;
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
