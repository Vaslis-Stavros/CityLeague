using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>A single match instance that participants join and claim positions in.</summary>
public class Event
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Optional series this event belongs to (enables result-gating).</summary>
    public Guid? SeriesId { get; set; }
    public EventSeries? Series { get; set; }

    public Guid OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public int EventFormatId { get; set; }
    public EventFormat? EventFormat { get; set; }

    public string Title { get; set; } = string.Empty;

    public DateTimeOffset ScheduledAt { get; set; }

    public string? Location { get; set; }

    public EventStatus Status { get; set; } = EventStatus.Open;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    public ICollection<EventPosition> Positions { get; set; } = new List<EventPosition>();
    public EventResult? Result { get; set; }
}
