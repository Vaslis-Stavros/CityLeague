using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>
/// A single position slot on the pitch for an event, copied from a formation template.
/// A slot is claimed when <see cref="UserId"/> is set.
/// </summary>
public class EventPosition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    /// <summary>Template slot id, e.g. "h_gk", unique within an event.</summary>
    public string SlotId { get; set; } = string.Empty;

    /// <summary>Position label, e.g. "GK", "CB", "ST".</summary>
    public string Label { get; set; } = string.Empty;

    public MatchSide Side { get; set; }

    /// <summary>Normalized pitch coordinates in the range 0..1.</summary>
    public double X { get; set; }
    public double Y { get; set; }

    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public DateTimeOffset? ClaimedAt { get; set; }

    /// <summary>Concurrency token to make position claiming race-safe.</summary>
    public uint RowVersion { get; set; }
}
