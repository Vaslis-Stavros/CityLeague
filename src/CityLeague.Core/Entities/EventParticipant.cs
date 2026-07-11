namespace CityLeague.Core.Entities;

/// <summary>Links a user to an event they have joined or been invited to.</summary>
public class EventParticipant
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid EventId { get; set; }
    public Event? Event { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Who invited this participant (null for the organizer).</summary>
    public Guid? InvitedByUserId { get; set; }

    /// <summary>All accepted participants may invite their own contacts.</summary>
    public bool CanInvite { get; set; } = true;

    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
}
