using CityLeague.Api.Common;
using CityLeague.Api.Hubs;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Core.Enums;
using CityLeague.Core.Formations;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Services;

public class EventService(
    CityLeagueDbContext db,
    IFormationProvider formations,
    ApiMapper mapper,
    LeagueService leagues,
    IHubContext<EventHub, IEventClient> hub)
{
    private readonly CityLeagueDbContext _db = db;
    private readonly IFormationProvider _formations = formations;
    private readonly ApiMapper _mapper = mapper;
    private readonly LeagueService _leagues = leagues;
    private readonly IHubContext<EventHub, IEventClient> _hub = hub;

    // ---- Series ----

    public async Task<SeriesDto> CreateSeriesAsync(Guid ownerId, CreateSeriesRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw ServiceException.BadRequest("Series name is required.");

        var sport = await _db.Sports.FirstOrDefaultAsync(s => s.Id == request.SportId, ct)
            ?? throw ServiceException.BadRequest("Unknown sport.");
        if (sport.Availability != SportAvailability.Enabled)
            throw ServiceException.BadRequest($"{sport.Name} is coming soon.");

        var series = new EventSeries { Name = request.Name.Trim(), OwnerUserId = ownerId, SportId = request.SportId };
        _db.EventSeries.Add(series);
        await _db.SaveChangesAsync(ct);
        return new SeriesDto(series.Id, series.Name, series.SportId);
    }

    public async Task<List<SeriesDto>> GetSeriesAsync(Guid ownerId, CancellationToken ct = default)
        => await _db.EventSeries
            .Where(s => s.OwnerUserId == ownerId)
            .OrderBy(s => s.Name)
            .Select(s => new SeriesDto(s.Id, s.Name, s.SportId))
            .ToListAsync(ct);

    // ---- Events ----

    public async Task<EventDetailDto> CreateEventAsync(Guid ownerId, CreateEventRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw ServiceException.BadRequest("Title is required.");

        var format = await _db.EventFormats.Include(f => f.Sport)
            .FirstOrDefaultAsync(f => f.Id == request.EventFormatId, ct)
            ?? throw ServiceException.BadRequest("Unknown event format.");

        if (format.Sport!.Availability != SportAvailability.Enabled)
            throw ServiceException.BadRequest($"{format.Sport.Name} is coming soon.");

        await EnforceOutstandingResultGateAsync(ownerId, ct);

        if (request.SeriesId is Guid seriesId)
            await EnforceResultGatingAsync(ownerId, seriesId, ct);

        var template = _formations.GetTemplate(format.FormationTemplateId);
        if (template.Slots.Count == 0)
            throw ServiceException.BadRequest("No formation template for this format.");

        var ev = new Event
        {
            OwnerUserId = ownerId,
            SportId = format.SportId,
            EventFormatId = format.Id,
            SeriesId = request.SeriesId,
            Title = request.Title.Trim(),
            ScheduledAt = request.ScheduledAt,
            Location = request.Location?.Trim(),
            Status = EventStatus.Open,
        };
        _db.Events.Add(ev);

        foreach (var slot in template.Slots)
        {
            _db.EventPositions.Add(new EventPosition
            {
                EventId = ev.Id,
                SlotId = slot.SlotId,
                Label = slot.Label,
                Side = slot.Side,
                X = slot.X,
                Y = slot.Y,
            });
        }

        if (request.LeagueId is Guid leagueId)
        {
            // Validate membership / status without saving; event + link persist together below.
            await _leagues.ValidateLinkAsync(leagueId, ownerId, ct);
            _db.LeagueEvents.Add(new LeagueEvent { LeagueId = leagueId, EventId = ev.Id });
        }

        _db.EventParticipants.Add(new EventParticipant
        {
            EventId = ev.Id,
            UserId = ownerId,
            InvitedByUserId = null,
            CanInvite = true,
        });

        if (request.InviteUserIds is { Count: > 0 })
        {
            var invitees = await ResolveInvitableContactsAsync(ownerId, request.InviteUserIds, ct);
            foreach (var invitee in invitees)
                _db.EventParticipants.Add(new EventParticipant { EventId = ev.Id, UserId = invitee, InvitedByUserId = ownerId, CanInvite = true });
        }

        await _db.SaveChangesAsync(ct);
        return await GetEventAsync(ownerId, ev.Id, ct) ?? throw ServiceException.NotFound();
    }

    private async Task EnforceOutstandingResultGateAsync(Guid ownerId, CancellationToken ct)
    {
        await SyncLifecycleAsync(ownerId, ct);
        var now = DateTimeOffset.UtcNow;
        var pending = await _db.Events.AnyAsync(e =>
            e.OwnerUserId == ownerId
            && e.Status == EventStatus.Locked
            && e.ScheduledAt < now
            && e.Result == null, ct);
        if (pending)
            throw ServiceException.Conflict("Submit the pending match result before creating another event.");
    }

    private async Task EnforceResultGatingAsync(Guid ownerId, Guid seriesId, CancellationToken ct)
    {
        var series = await _db.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw ServiceException.NotFound("Series not found.");
        if (series.OwnerUserId != ownerId)
            throw ServiceException.Forbidden("Only the series owner can add matches.");

        var last = await _db.Events
            .Where(e => e.SeriesId == seriesId && e.Status != EventStatus.Cancelled && e.Status != EventStatus.Incomplete)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (last is not null && last.Status != EventStatus.Completed)
            throw ServiceException.Conflict("Submit result for the previous match first.");
    }

    /// <summary>Open events past kickoff become Incomplete; locked past events stay Locked (pending result).</summary>
    private async Task SyncLifecycleAsync(Guid userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var stale = await _db.Events
            .Where(e => e.Status == EventStatus.Open
                        && e.ScheduledAt < now
                        && e.Participants.Any(p => p.UserId == userId))
            .ToListAsync(ct);
        if (stale.Count == 0) return;

        foreach (var ev in stale)
            ev.Status = EventStatus.Incomplete;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<EventSummaryDto>> GetMyEventsAsync(Guid userId, CancellationToken ct = default)
    {
        await SyncLifecycleAsync(userId, ct);
        var now = DateTimeOffset.UtcNow;

        var events = await _db.Events
            .Where(e => (e.Status == EventStatus.Open || e.Status == EventStatus.Locked)
                        && e.ScheduledAt >= now
                        && e.Participants.Any(p => p.UserId == userId))
            .Include(e => e.Sport)
            .Include(e => e.EventFormat)
            .Include(e => e.Positions)
            .OrderBy(e => e.ScheduledAt)
            .ToListAsync(ct);

        return events.Select(e => ToSummary(e, userId, now)).ToList();
    }

    public async Task<List<EventSummaryDto>> GetIncompleteEventsAsync(Guid userId, CancellationToken ct = default)
    {
        await SyncLifecycleAsync(userId, ct);

        var events = await _db.Events
            .Where(e => e.Status == EventStatus.Incomplete && e.Participants.Any(p => p.UserId == userId))
            .Include(e => e.Sport)
            .Include(e => e.EventFormat)
            .Include(e => e.Positions)
            .OrderByDescending(e => e.ScheduledAt)
            .ToListAsync(ct);

        return events.Select(e => ToSummary(e, userId, DateTimeOffset.UtcNow)).ToList();
    }

    public async Task<List<EventSummaryDto>> GetPendingResultEventsAsync(Guid userId, CancellationToken ct = default)
    {
        await SyncLifecycleAsync(userId, ct);
        var now = DateTimeOffset.UtcNow;

        var events = await _db.Events
            .Where(e => e.OwnerUserId == userId
                        && e.Status == EventStatus.Locked
                        && e.ScheduledAt < now
                        && e.Result == null)
            .Include(e => e.Sport)
            .Include(e => e.EventFormat)
            .Include(e => e.Positions)
            .OrderBy(e => e.ScheduledAt)
            .ToListAsync(ct);

        return events.Select(e => ToSummary(e, userId, now)).ToList();
    }

    public async Task<List<EventSummaryDto>> GetPastEventsAsync(Guid userId, CancellationToken ct = default)
    {
        var events = await _db.Events
            .Where(e => e.Status == EventStatus.Completed && e.Participants.Any(p => p.UserId == userId))
            .Include(e => e.Sport)
            .Include(e => e.EventFormat)
            .Include(e => e.Positions)
            .Include(e => e.Result)
            .OrderByDescending(e => e.Result!.SubmittedAt)
            .ToListAsync(ct);

        return events.Select(e => ToSummary(e, userId, DateTimeOffset.UtcNow)).ToList();
    }

    public async Task DeleteEventAsync(Guid ownerId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.Events
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");

        if (ev.OwnerUserId != ownerId)
            throw ServiceException.Forbidden("Only the organizer can delete this event.");
        if (ev.Status == EventStatus.Completed)
            throw ServiceException.Conflict("Completed events appear in History and cannot be deleted.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync(ct);
    }

    public async Task LeaveEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await _db.Events
            .Include(e => e.Participants)
            .Include(e => e.Positions)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");

        if (ev.OwnerUserId == userId)
            throw ServiceException.Conflict("Organizers can't leave — delete or reschedule the match instead.");
        if (ev.Status == EventStatus.Completed)
            throw ServiceException.Conflict("Completed matches stay in History.");
        if (ev.Status == EventStatus.Locked)
            throw ServiceException.Conflict("This match is locked — you can't leave the roster.");

        var part = ev.Participants.FirstOrDefault(p => p.UserId == userId)
            ?? throw ServiceException.NotFound("You are not in this match.");

        foreach (var pos in ev.Positions.Where(p => p.UserId == userId))
        {
            pos.UserId = null;
            pos.ClaimedAt = null;
        }

        _db.EventParticipants.Remove(part);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EventDetailDto> UpdateEventAsync(Guid userId, Guid eventId, UpdateEventRequest request, CancellationToken ct = default)
    {
        var ev = await _db.Events
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");

        if (ev.OwnerUserId != userId)
            throw ServiceException.Forbidden("Only the organizer can edit this match.");
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw ServiceException.Conflict("This match can't be edited.");

        if (!string.IsNullOrWhiteSpace(request.Title))
            ev.Title = request.Title.Trim();
        if (request.Location is not null)
            ev.Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim();

        if (request.ScheduledAt is DateTimeOffset when)
        {
            ev.ScheduledAt = when;
            var now = DateTimeOffset.UtcNow;
            if (ev.Status == EventStatus.Incomplete && when >= now)
                ev.Status = EventStatus.Open;
            else if (ev.Status == EventStatus.Open && when < now)
                ev.Status = EventStatus.Incomplete;
            else if (ev.Status == EventStatus.Locked && when < now)
            {
                // stays Locked → pending result
            }
            else if (ev.Status == EventStatus.Locked && when >= now)
            {
                // remains Locked but back in upcoming
            }
        }

        await _db.SaveChangesAsync(ct);
        return await GetEventAsync(userId, eventId, ct) ?? throw ServiceException.NotFound();
    }

    public async Task<EventDetailDto> LockEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await LoadEventGraphAsync(eventId, ct);
        if (ev.OwnerUserId != userId)
            throw ServiceException.Forbidden("Only the organizer can lock the match.");
        if (ev.Status != EventStatus.Open)
            throw ServiceException.Conflict("Only open matches can be locked.");
        if (ev.ScheduledAt < DateTimeOffset.UtcNow)
            throw ServiceException.Conflict("This match is already past kickoff.");

        var claimed = ev.Positions.Count(p => p.UserId != null);
        if (claimed != ev.Positions.Count || ev.Positions.Count == 0)
            throw ServiceException.Conflict("Fill every spot before locking.");

        ev.Status = EventStatus.Locked;
        await _db.SaveChangesAsync(ct);
        return await GetEventAsync(userId, eventId, ct) ?? throw ServiceException.NotFound();
    }

    public async Task<EventDetailDto> UnlockEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var ev = await LoadEventGraphAsync(eventId, ct);
        if (ev.OwnerUserId != userId)
            throw ServiceException.Forbidden("Only the organizer can unlock the match.");
        if (ev.Status != EventStatus.Locked)
            throw ServiceException.Conflict("This match is not locked.");
        if (ev.ScheduledAt < DateTimeOffset.UtcNow)
            throw ServiceException.Conflict("Past locked matches need a result — they can't be unlocked.");

        ev.Status = EventStatus.Open;
        await _db.SaveChangesAsync(ct);
        return await GetEventAsync(userId, eventId, ct) ?? throw ServiceException.NotFound();
    }

    private async Task<Event> LoadEventGraphAsync(Guid eventId, CancellationToken ct)
        => await _db.Events
            .Include(e => e.Positions)
            .Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
           ?? throw ServiceException.NotFound("Event not found.");

    private static string StatusLabel(EventStatus status) => status switch
    {
        EventStatus.Locked => "Locked",
        EventStatus.Incomplete => "Incomplete",
        _ => status.ToString(),
    };

    private static EventSummaryDto ToSummary(Event e, Guid userId, DateTimeOffset now)
    {
        var pending = e.OwnerUserId == userId
                      && e.Status == EventStatus.Locked
                      && e.ScheduledAt < now
                      && e.Result is null;
        return new(
            e.Id,
            e.Title,
            e.Sport!.Key,
            e.EventFormat!.Name,
            e.ScheduledAt,
            e.Location,
            StatusLabel(e.Status),
            e.Positions.Count(p => p.UserId != null),
            e.Positions.Count,
            e.OwnerUserId == userId,
            pending,
            ApiMapper.ToResultDto(e.Result));
    }

    public async Task<EventDetailDto?> GetEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        await SyncLifecycleAsync(userId, ct);

        var ev = await _db.Events
            .Include(e => e.Sport)
            .Include(e => e.EventFormat)
            .Include(e => e.Positions).ThenInclude(p => p.User)
            .Include(e => e.Participants).ThenInclude(p => p.User)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct);

        if (ev is null) return null;

        var me = ev.Participants.FirstOrDefault(p => p.UserId == userId);
        if (me is null)
            throw ServiceException.Forbidden("You are not part of this event.");

        return ToDetail(ev, userId, me);
    }

    private EventDetailDto ToDetail(Event ev, Guid userId, EventParticipant me)
    {
        var now = DateTimeOffset.UtcNow;
        var isOwner = ev.OwnerUserId == userId;
        var claimed = ev.Positions.Count(p => p.UserId != null);
        var total = ev.Positions.Count;
        var isFull = total > 0 && claimed == total;
        var isPast = ev.ScheduledAt < now;
        var isPending = isOwner && ev.Status == EventStatus.Locked && isPast && ev.Result is null;
        var canLock = isOwner && ev.Status == EventStatus.Open && isFull && !isPast;
        var canUnlock = isOwner && ev.Status == EventStatus.Locked && !isPast;
        var canEditSchedule = isOwner && ev.Status is EventStatus.Open or EventStatus.Locked or EventStatus.Incomplete;
        var canSubmit = isOwner && ev.Status == EventStatus.Locked && isPast && ev.Result is null;
        var canLeave = !isOwner && ev.Status is EventStatus.Open or EventStatus.Incomplete;
        var canDelete = isOwner && ev.Status is not EventStatus.Completed;
        var canInvite = me.CanInvite && ev.Status == EventStatus.Open && !isPast;

        return new EventDetailDto(
            ev.Id,
            ev.Title,
            ev.Sport!.Key,
            ev.SportId,
            ev.EventFormat!.Key,
            ev.EventFormat.Name,
            ev.EventFormat.PlayersPerSide,
            ev.ScheduledAt,
            ev.Location,
            StatusLabel(ev.Status),
            isOwner,
            canInvite,
            ev.OwnerUserId,
            ev.Positions.OrderBy(p => p.Side).ThenBy(p => p.X).Select(_mapper.ToPositionDto).ToList(),
            ev.Participants.Select(p => _mapper.ToParticipantDto(p, ev.OwnerUserId)).ToList(),
            ApiMapper.ToResultDto(ev.Result),
            isFull,
            isPast,
            isPending,
            canLock,
            canUnlock,
            canEditSchedule,
            canSubmit,
            canLeave,
            canDelete);
    }

    // ---- Participation ----

    public async Task EnsureParticipantAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
        var isParticipant = await _db.EventParticipants.AnyAsync(p => p.EventId == eventId && p.UserId == userId, ct);
        if (!isParticipant)
            throw ServiceException.Forbidden("You are not part of this event.");
    }

    public async Task<List<ParticipantDto>> InviteAsync(Guid actingUserId, Guid eventId, IReadOnlyList<Guid> userIds, CancellationToken ct = default)
    {
        var ev = await _db.Events.Include(e => e.Participants)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");

        if (ev.Status is not EventStatus.Open)
            throw ServiceException.Conflict("Invites are only open before the match is locked.");
        if (ev.ScheduledAt < DateTimeOffset.UtcNow)
            throw ServiceException.Conflict("This match is past kickoff.");

        var me = ev.Participants.FirstOrDefault(p => p.UserId == actingUserId)
            ?? throw ServiceException.Forbidden("You are not part of this event.");
        if (!me.CanInvite)
            throw ServiceException.Forbidden("You cannot invite to this event.");

        var invitees = await ResolveInvitableContactsAsync(actingUserId, userIds, ct);
        var existing = ev.Participants.Select(p => p.UserId).ToHashSet();

        var added = new List<EventParticipant>();
        foreach (var invitee in invitees.Where(i => !existing.Contains(i)))
        {
            var participant = new EventParticipant { EventId = eventId, UserId = invitee, InvitedByUserId = actingUserId, CanInvite = true };
            _db.EventParticipants.Add(participant);
            added.Add(participant);
        }

        if (added.Count == 0)
            return [];

        await _db.SaveChangesAsync(ct);

        var addedIds = added.Select(a => a.UserId).ToList();
        var withUsers = await _db.EventParticipants
            .Include(p => p.User)
            .Where(p => p.EventId == eventId && addedIds.Contains(p.UserId))
            .ToListAsync(ct);

        var dtos = withUsers.Select(p => _mapper.ToParticipantDto(p, ev.OwnerUserId)).ToList();
        foreach (var dto in dtos)
            await _hub.Clients.Group(EventHub.GroupName(eventId)).ParticipantJoined(dto);

        return dtos;
    }

    /// <summary>Filters requested user ids to those that are accepted contacts of the acting user.</summary>
    private async Task<List<Guid>> ResolveInvitableContactsAsync(Guid actingUserId, IReadOnlyList<Guid> userIds, CancellationToken ct)
    {
        var distinct = userIds.Where(id => id != actingUserId).Distinct().ToList();
        if (distinct.Count == 0) return [];

        return await _db.Contacts
            .Where(c => c.OwnerUserId == actingUserId
                        && c.Status == ContactStatus.Accepted
                        && distinct.Contains(c.ContactUserId))
            .Select(c => c.ContactUserId)
            .ToListAsync(ct);
    }

    // ---- Positions ----

    public async Task<IReadOnlyList<PositionChangedDto>> ClaimPositionAsync(Guid userId, Guid eventId, string slotId, CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled or EventStatus.Incomplete)
            throw ServiceException.Conflict("This event is closed.");

        await EnsureParticipantAsync(userId, eventId, ct);

        if (ev.Status == EventStatus.Locked)
        {
            var alreadyOnPitch = await _db.EventPositions.AnyAsync(p => p.EventId == eventId && p.UserId == userId, ct);
            if (!alreadyOnPitch)
                throw ServiceException.Conflict("This match is locked — only position swaps are allowed.");
        }

        var target = await _db.EventPositions.AsNoTracking()
            .FirstOrDefaultAsync(p => p.EventId == eventId && p.SlotId == slotId, ct)
            ?? throw ServiceException.NotFound("Position not found.");

        if (target.UserId == userId)
            return [];

        // Release any slot the user currently holds in this event (a player occupies one slot).
        var previousSlots = await _db.EventPositions
            .Where(p => p.EventId == eventId && p.UserId == userId)
            .Select(p => p.SlotId)
            .ToListAsync(ct);

        if (previousSlots.Count > 0)
        {
            await _db.EventPositions
                .Where(p => p.EventId == eventId && p.UserId == userId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(p => p.UserId, (Guid?)null)
                    .SetProperty(p => p.ClaimedAt, (DateTimeOffset?)null), ct);
        }

        // Atomic claim: only succeeds if the slot is still empty.
        var affected = await _db.EventPositions
            .Where(p => p.EventId == eventId && p.SlotId == slotId && p.UserId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.UserId, userId)
                .SetProperty(p => p.ClaimedAt, DateTimeOffset.UtcNow), ct);

        if (affected == 0)
            throw ServiceException.Conflict("That position was just taken.");

        var changedSlots = previousSlots.Append(slotId).Distinct().ToList();
        return await BroadcastPositionChangesAsync(eventId, changedSlots, ct);
    }

    public async Task<IReadOnlyList<PositionChangedDto>> ReleasePositionAsync(Guid userId, Guid eventId, string slotId, CancellationToken ct = default)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled or EventStatus.Incomplete)
            throw ServiceException.Conflict("This event is closed.");
        if (ev.Status == EventStatus.Locked)
            throw ServiceException.Conflict("This match is locked — swap into another spot instead of leaving empty.");

        await EnsureParticipantAsync(userId, eventId, ct);

        var affected = await _db.EventPositions
            .Where(p => p.EventId == eventId && p.SlotId == slotId && p.UserId == userId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.UserId, (Guid?)null)
                .SetProperty(p => p.ClaimedAt, (DateTimeOffset?)null), ct);

        return affected == 0 ? [] : await BroadcastPositionChangesAsync(eventId, [slotId], ct);
    }

    private async Task<IReadOnlyList<PositionChangedDto>> BroadcastPositionChangesAsync(Guid eventId, IReadOnlyList<string> slotIds, CancellationToken ct)
    {
        var positions = await _db.EventPositions
            .Include(p => p.User)
            .Where(p => p.EventId == eventId && slotIds.Contains(p.SlotId))
            .ToListAsync(ct);

        var changes = positions.Select(p => _mapper.ToPositionChanged(eventId, p)).ToList();
        foreach (var change in changes)
            await _hub.Clients.Group(EventHub.GroupName(eventId)).PositionChanged(change);

        return changes;
    }

    // ---- Results & stats ----

    public async Task<ResultDto> SubmitResultAsync(Guid ownerId, Guid eventId, SubmitResultRequest request, CancellationToken ct = default)
    {
        if (request.HomeScore < 0 || request.AwayScore < 0)
            throw ServiceException.BadRequest("Scores cannot be negative.");

        var ev = await _db.Events
            .Include(e => e.Positions)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.Id == eventId, ct)
            ?? throw ServiceException.NotFound("Event not found.");

        if (ev.OwnerUserId != ownerId)
            throw ServiceException.Forbidden("Only the organizer can submit the result.");
        if (ev.Result is not null || ev.Status == EventStatus.Completed)
            throw ServiceException.Conflict("Result already submitted.");
        if (ev.Status != EventStatus.Locked)
            throw ServiceException.Conflict("Lock the match and wait until kickoff before submitting a result.");
        if (ev.ScheduledAt >= DateTimeOffset.UtcNow)
            throw ServiceException.Conflict("Results can be submitted after kickoff.");
        if (ev.Status == EventStatus.Cancelled)
            throw ServiceException.Conflict("This event was cancelled.");

        var winningSide = request.HomeScore > request.AwayScore ? WinningSide.Home
            : request.AwayScore > request.HomeScore ? WinningSide.Away
            : WinningSide.Draw;

        var result = new EventResult
        {
            EventId = ev.Id,
            HomeScore = request.HomeScore,
            AwayScore = request.AwayScore,
            WinningSide = winningSide,
        };
        _db.EventResults.Add(result);

        // Roster = players who claimed a position, attributed to their slot's side.
        var rostered = ev.Positions
            .Where(p => p.UserId != null)
            .GroupBy(p => p.UserId!.Value)
            .Select(g => new { UserId = g.Key, g.First().Side })
            .ToList();

        foreach (var entry in rostered)
        {
            result.Roster.Add(new EventResultRoster { UserId = entry.UserId, Side = entry.Side });
            await ApplyStatsAsync(entry.UserId, ev.SportId, entry.Side, winningSide, ct);
        }

        ev.Status = EventStatus.Completed;
        await _db.SaveChangesAsync(ct);

        await _leagues.OnLeagueMatchCompletedAsync(eventId, winningSide, ct);

        var dto = new ResultDto(result.HomeScore, result.AwayScore, result.WinningSide.ToString(), result.SubmittedAt);
        await _hub.Clients.Group(EventHub.GroupName(eventId)).EventCompleted(dto);
        return dto;
    }

    private async Task ApplyStatsAsync(Guid userId, int sportId, MatchSide side, WinningSide winning, CancellationToken ct)
    {
        var stats = await _db.PlayerSportStats.FirstOrDefaultAsync(s => s.UserId == userId && s.SportId == sportId, ct);
        if (stats is null)
        {
            stats = new PlayerSportStats { UserId = userId, SportId = sportId };
            _db.PlayerSportStats.Add(stats);
        }

        stats.Played++;
        if (winning == WinningSide.Draw)
            stats.Draws++;
        else if ((winning == WinningSide.Home && side == MatchSide.Home) ||
                 (winning == WinningSide.Away && side == MatchSide.Away))
            stats.Wins++;
        else
            stats.Losses++;
    }
}
