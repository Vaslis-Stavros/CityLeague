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

    private async Task EnforceResultGatingAsync(Guid ownerId, Guid seriesId, CancellationToken ct)
    {
        var series = await _db.EventSeries.FirstOrDefaultAsync(s => s.Id == seriesId, ct)
            ?? throw ServiceException.NotFound("Series not found.");
        if (series.OwnerUserId != ownerId)
            throw ServiceException.Forbidden("Only the series owner can add matches.");

        var last = await _db.Events
            .Where(e => e.SeriesId == seriesId && e.Status != EventStatus.Cancelled)
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (last is not null && last.Status != EventStatus.Completed)
            throw ServiceException.Conflict("Submit result for the previous match first.");
    }

    public async Task<List<EventSummaryDto>> GetMyEventsAsync(Guid userId, CancellationToken ct = default)
    {
        var events = await _db.Events
            .Where(e => (e.Status == EventStatus.Open || e.Status == EventStatus.InProgress)
                        && e.Participants.Any(p => p.UserId == userId))
            .Include(e => e.Sport)
            .Include(e => e.EventFormat)
            .Include(e => e.Positions)
            .OrderBy(e => e.ScheduledAt)
            .ToListAsync(ct);

        return events.Select(e => ToSummary(e, userId)).ToList();
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

        return events.Select(e => ToSummary(e, userId)).ToList();
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
            throw ServiceException.Conflict("Completed events appear in Past events and cannot be deleted.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync(ct);
    }

    private static EventSummaryDto ToSummary(Event e, Guid userId) => new(
        e.Id,
        e.Title,
        e.Sport!.Key,
        e.EventFormat!.Name,
        e.ScheduledAt,
        e.Location,
        e.Status.ToString(),
        e.Positions.Count(p => p.UserId != null),
        e.Positions.Count,
        e.OwnerUserId == userId,
        ApiMapper.ToResultDto(e.Result));

    public async Task<EventDetailDto?> GetEventAsync(Guid userId, Guid eventId, CancellationToken ct = default)
    {
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
            ev.Status.ToString(),
            ev.OwnerUserId == userId,
            me.CanInvite,
            ev.OwnerUserId,
            ev.Positions.OrderBy(p => p.Side).ThenBy(p => p.X).Select(_mapper.ToPositionDto).ToList(),
            ev.Participants.Select(p => _mapper.ToParticipantDto(p, ev.OwnerUserId)).ToList(),
            ApiMapper.ToResultDto(ev.Result));
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

        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw ServiceException.Conflict("This event is closed.");

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
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw ServiceException.Conflict("This event is closed.");

        await EnsureParticipantAsync(userId, eventId, ct);

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
        if (ev.Status is EventStatus.Completed or EventStatus.Cancelled)
            throw ServiceException.Conflict("This event is closed.");

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
