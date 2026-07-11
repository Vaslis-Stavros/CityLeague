using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>Phase 2: a league groups events and tracks team standings until terminated.</summary>
public class League
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public LeagueStatus Status { get; set; } = LeagueStatus.Active;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<LeagueTeam> Teams { get; set; } = new List<LeagueTeam>();
    public ICollection<LeagueParticipant> Participants { get; set; } = new List<LeagueParticipant>();
    public ICollection<LeagueEvent> Events { get; set; } = new List<LeagueEvent>();
}

/// <summary>Phase 2: a named team within a league, with an optional custom logo.</summary>
public class LeagueTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? LogoBlobUrl { get; set; }

    public TeamSportStats? Stats { get; set; }
}

/// <summary>Phase 2: a user participating in a league, optionally assigned to a team.</summary>
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

/// <summary>Phase 2: links an event to the league it was played under.</summary>
public class LeagueEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid LeagueId { get; set; }
    public League? League { get; set; }

    public Guid EventId { get; set; }
    public Event? Event { get; set; }
}

/// <summary>Phase 2: aggregated standings for a league team.</summary>
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
