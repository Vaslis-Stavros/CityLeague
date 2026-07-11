using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>The submitted result of a completed event.</summary>
public class EventResult
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public int HomeScore { get; set; }
    public int AwayScore { get; set; }

    public WinningSide WinningSide { get; set; }

    public DateTimeOffset SubmittedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<EventResultRoster> Roster { get; set; } = new List<EventResultRoster>();
}
