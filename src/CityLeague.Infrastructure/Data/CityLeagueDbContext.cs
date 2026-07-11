using CityLeague.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CityLeague.Infrastructure.Data;

public class CityLeagueDbContext(DbContextOptions<CityLeagueDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Sport> Sports => Set<Sport>();
    public DbSet<EventFormat> EventFormats => Set<EventFormat>();
    public DbSet<EventSeries> EventSeries => Set<EventSeries>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventParticipant> EventParticipants => Set<EventParticipant>();
    public DbSet<EventPosition> EventPositions => Set<EventPosition>();
    public DbSet<EventResult> EventResults => Set<EventResult>();
    public DbSet<EventResultRoster> EventResultRosters => Set<EventResultRoster>();
    public DbSet<PlayerSportStats> PlayerSportStats => Set<PlayerSportStats>();

    // Phase 2 (schema only).
    public DbSet<League> Leagues => Set<League>();
    public DbSet<LeagueTeam> LeagueTeams => Set<LeagueTeam>();
    public DbSet<LeagueParticipant> LeagueParticipants => Set<LeagueParticipant>();
    public DbSet<LeagueEvent> LeagueEvents => Set<LeagueEvent>();
    public DbSet<TeamSportStats> TeamSportStats => Set<TeamSportStats>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        var isSqlServer = Database.IsSqlServer();

        b.Entity<User>(e =>
        {
            e.Property(u => u.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(u => u.UniqueHandle).HasMaxLength(20);
            e.Property(u => u.Email).HasMaxLength(256);
            e.Property(u => u.B2CObjectId).HasMaxLength(128);
            e.Property(u => u.PasswordHash).HasMaxLength(256);

            var handleIx = e.HasIndex(u => u.UniqueHandle).IsUnique();
            var b2cIx = e.HasIndex(u => u.B2CObjectId).IsUnique();
            var emailIx = e.HasIndex(u => u.Email).IsUnique();
            if (isSqlServer)
            {
                // SQL Server treats NULLs as equal in unique indexes, so filter them out.
                handleIx.HasFilter("[UniqueHandle] IS NOT NULL");
                b2cIx.HasFilter("[B2CObjectId] IS NOT NULL");
                emailIx.HasFilter("[Email] IS NOT NULL");
            }
        });

        b.Entity<Contact>(e =>
        {
            e.HasIndex(c => new { c.OwnerUserId, c.ContactUserId }).IsUnique();
            e.HasOne(c => c.OwnerUser).WithMany(u => u.ContactsOwned)
                .HasForeignKey(c => c.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(c => c.ContactUser).WithMany()
                .HasForeignKey(c => c.ContactUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Sport>(e =>
        {
            e.Property(s => s.Key).HasMaxLength(32).IsRequired();
            e.Property(s => s.Name).HasMaxLength(64).IsRequired();
            e.HasIndex(s => s.Key).IsUnique();
        });

        b.Entity<EventFormat>(e =>
        {
            e.Property(f => f.Key).HasMaxLength(48).IsRequired();
            e.Property(f => f.Name).HasMaxLength(64).IsRequired();
            e.Property(f => f.FormationTemplateId).HasMaxLength(48).IsRequired();
            e.HasIndex(f => f.Key).IsUnique();
            e.HasOne(f => f.Sport).WithMany(s => s.Formats)
                .HasForeignKey(f => f.SportId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<EventSeries>(e =>
        {
            e.Property(s => s.Name).HasMaxLength(128).IsRequired();
            e.HasOne(s => s.OwnerUser).WithMany()
                .HasForeignKey(s => s.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(s => s.Sport).WithMany()
                .HasForeignKey(s => s.SportId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Event>(e =>
        {
            e.Property(ev => ev.Title).HasMaxLength(128).IsRequired();
            e.Property(ev => ev.Location).HasMaxLength(256);
            e.HasOne(ev => ev.OwnerUser).WithMany()
                .HasForeignKey(ev => ev.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.Series).WithMany(s => s.Events)
                .HasForeignKey(ev => ev.SeriesId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(ev => ev.Sport).WithMany()
                .HasForeignKey(ev => ev.SportId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(ev => ev.EventFormat).WithMany()
                .HasForeignKey(ev => ev.EventFormatId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(ev => ev.SeriesId);
        });

        b.Entity<EventParticipant>(e =>
        {
            e.HasIndex(p => new { p.EventId, p.UserId }).IsUnique();
            e.HasOne(p => p.Event).WithMany(ev => ev.Participants)
                .HasForeignKey(p => p.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User).WithMany()
                .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<EventPosition>(e =>
        {
            e.Property(p => p.SlotId).HasMaxLength(48).IsRequired();
            e.Property(p => p.Label).HasMaxLength(16).IsRequired();
            e.HasIndex(p => new { p.EventId, p.SlotId }).IsUnique();
            e.HasOne(p => p.Event).WithMany(ev => ev.Positions)
                .HasForeignKey(p => p.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User).WithMany()
                .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<EventResult>(e =>
        {
            e.HasOne(r => r.Event).WithOne(ev => ev.Result)
                .HasForeignKey<EventResult>(r => r.EventId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.EventId).IsUnique();
        });

        b.Entity<EventResultRoster>(e =>
        {
            e.HasOne(r => r.EventResult).WithMany(res => res.Roster)
                .HasForeignKey(r => r.EventResultId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.User).WithMany()
                .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PlayerSportStats>(e =>
        {
            e.HasIndex(s => new { s.UserId, s.SportId }).IsUnique();
            e.HasOne(s => s.User).WithMany(u => u.Stats)
                .HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(s => s.Sport).WithMany()
                .HasForeignKey(s => s.SportId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureLeagues(b);

        if (!isSqlServer)
            ApplySqliteDateTimeOffsetConversion(b);
    }

    /// <summary>
    /// SQLite cannot compare/order native DateTimeOffset. For the dev SQLite provider we store
    /// them as sortable Unix milliseconds (UTC). SQL Server keeps native datetimeoffset.
    /// </summary>
    private static void ApplySqliteDateTimeOffsetConversion(ModelBuilder b)
    {
        var converter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUniversalTime().ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v));

        foreach (var entityType in b.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(converter);
            }
        }
    }

    private static void ConfigureLeagues(ModelBuilder b)
    {
        b.Entity<League>(e =>
        {
            e.Property(l => l.Name).HasMaxLength(128).IsRequired();
            e.HasOne(l => l.OwnerUser).WithMany().HasForeignKey(l => l.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(l => l.Sport).WithMany().HasForeignKey(l => l.SportId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<LeagueTeam>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(128).IsRequired();
            e.HasOne(t => t.League).WithMany(l => l.Teams).HasForeignKey(t => t.LeagueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(t => t.Stats).WithOne(s => s.LeagueTeam)
                .HasForeignKey<TeamSportStats>(s => s.LeagueTeamId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<LeagueParticipant>(e =>
        {
            e.HasOne(p => p.League).WithMany(l => l.Participants).HasForeignKey(p => p.LeagueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(p => p.User).WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(p => p.LeagueTeam).WithMany().HasForeignKey(p => p.LeagueTeamId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<LeagueEvent>(e =>
        {
            e.HasOne(le => le.League).WithMany(l => l.Events).HasForeignKey(le => le.LeagueId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(le => le.Event).WithMany().HasForeignKey(le => le.EventId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
