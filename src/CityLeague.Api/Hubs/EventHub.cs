using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using CityLeague.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CityLeague.Api.Hubs;

/// <summary>Client methods pushed by the server for live event updates.</summary>
public interface IEventClient
{
    Task PositionChanged(PositionChangedDto change);
    Task ParticipantJoined(ParticipantDto participant);
    Task EventCompleted(ResultDto result);
}

[Authorize]
public class EventHub(EventService events) : Hub<IEventClient>
{
    private readonly EventService _events = events;

    public static string GroupName(Guid eventId) => $"event:{eventId}";

    private Guid CurrentUserId
    {
        get
        {
            var value = Context.User?.FindFirst(AppClaims.UserId)?.Value;
            return Guid.TryParse(value, out var id) ? id : Guid.Empty;
        }
    }

    /// <summary>Subscribes the caller to an event's live updates (must be a participant).</summary>
    public async Task JoinEvent(Guid eventId)
    {
        await _events.EnsureParticipantAsync(CurrentUserId, eventId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(eventId));
    }

    public Task LeaveEvent(Guid eventId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(eventId));

    public Task ClaimPosition(Guid eventId, string slotId)
        => _events.ClaimPositionAsync(CurrentUserId, eventId, slotId);

    public Task ReleasePosition(Guid eventId, string slotId)
        => _events.ReleasePositionAsync(CurrentUserId, eventId, slotId);
}
