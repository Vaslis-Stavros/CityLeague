using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>
/// A directed contact edge. A request creates one row (Owner -> Contact, Pending).
/// On acceptance a reciprocal row is created so both users see each other.
/// </summary>
public class Contact
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid OwnerUserId { get; set; }
    public User? OwnerUser { get; set; }

    public Guid ContactUserId { get; set; }
    public User? ContactUser { get; set; }

    public ContactStatus Status { get; set; } = ContactStatus.Pending;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
