namespace CityLeague.Core.Entities;

/// <summary>
/// A recurring group of events (e.g. "Friday Football"). Used to enforce the
/// "submit result before starting the next match" rule.
/// </summary>
public class EventSeries
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Event> Events { get; set; } = new List<Event>();
}
