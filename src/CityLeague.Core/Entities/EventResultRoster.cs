using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>Records which side each player was on for a completed event, for stats attribution.</summary>
public class EventResultRoster
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventResultId { get; set; }
    public EventResult? EventResult { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public MatchSide Side { get; set; }
}
