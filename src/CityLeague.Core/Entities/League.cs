using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>A league groups two teams, participants, and match results until finished.</summary>
public class League
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public LeagueStatus Status { get; set; } = LeagueStatus.Draft;

    /// <summary>How many completed matches finish the league unless leaders extend it.</summary>
    public int PlannedMatchCount { get; set; } = 10;

    /// <summary>Set when the league starts; null while still configuring (Draft).</summary>
    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<LeagueTeam> Teams { get; set; } = new List<LeagueTeam>();
    public ICollection<LeagueParticipant> Participants { get; set; } = new List<LeagueParticipant>();
    public ICollection<LeagueEvent> Events { get; set; } = new List<LeagueEvent>();
}

/// <summary>A named team within a league, with an optional custom logo and locked leader.</summary>
public class LeagueTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>0 = home/team A, 1 = away/team B for match result attribution.</summary>
    public int SortOrder { get; set; }

    public string? LogoBlobUrl { get; set; }

    /// <summary>Must be set before the league starts; cannot change teams afterward.</summary>
    public Guid? LeaderUserId { get; set; }
    public User? LeaderUser { get; set; }

    public TeamSportStats? Stats { get; set; }
}

/// <summary>A user participating in a league, optionally assigned to a team.</summary>
public class LeagueParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid? LeagueTeamId { get; set; }
    public LeagueTeam? LeagueTeam { get; set; }
}

/// <summary>Links an event to the league it was played under.</summary>
public class LeagueEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public Guid EventId { get; set; }
    public Event? Event { get; set; }
}

/// <summary>Aggregated standings for a league team.</summary>
public class TeamSportStats
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeagueTeamId { get; set; }
    public LeagueTeam? LeagueTeam { get; set; }

    public int Played { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
}
