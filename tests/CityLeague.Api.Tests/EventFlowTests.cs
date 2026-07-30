using System.Net;
using System.Net.Http.Json;
using CityLeague.Core.Dtos;
using CityLeague.Core.Enums;
using CityLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CityLeague.Api.Tests;

public class EventFlowTests : IClassFixture<TestAppFactory>
{
    private const int FootballSportId = 1;
    private const int FiveASideFormatId = 1;
    private const int SevenASideFormatId = 3;

    private readonly TestAppFactory _factory;

    public EventFlowTests(TestAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Result_gating_blocks_second_event_until_previous_result_submitted()
    {
        var owner = await _factory.CreateUserAsync($"gating-{Guid.NewGuid():N}@test.com", "Gating Owner", $"gate{Guid.NewGuid():N}"[..12]);

        var series = await (await owner.Client.PostAsJsonAsync("/api/series",
            new CreateSeriesRequest("Test Series", FootballSportId))).Content.ReadFromJsonAsync<SeriesDto>();

        var first = await CreateEventAsync(owner, series!.Id, "Week 1");
        Assert.NotEqual(Guid.Empty, first.Id);

        // Second event in the same series must be blocked.
        var blocked = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(SevenASideFormatId, "Week 2", DateTimeOffset.UtcNow.AddDays(7), null, series.Id, null));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        await PrepareLockedPastAsync(first.Id);
        var result = await owner.Client.PostAsJsonAsync($"/api/events/{first.Id}/result", new SubmitResultRequest(1, 0));
        result.EnsureSuccessStatusCode();

        var allowed = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(SevenASideFormatId, "Week 2", DateTimeOffset.UtcNow.AddDays(7), null, series.Id, null));
        allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Submitting_result_updates_player_stats()
    {
        var owner = await _factory.CreateUserAsync($"stats-{Guid.NewGuid():N}@test.com", "Stats Owner", $"stat{Guid.NewGuid():N}"[..12]);

        var ev = await CreateEventAsync(owner, null, "Stats Match");

        var claim = await owner.Client.PostAsync($"/api/events/{ev.Id}/positions/h_gk/claim", null);
        claim.EnsureSuccessStatusCode();

        await PrepareLockedPastAsync(ev.Id);
        var result = await owner.Client.PostAsJsonAsync($"/api/events/{ev.Id}/result", new SubmitResultRequest(3, 1));
        result.EnsureSuccessStatusCode();

        var stats = await owner.Client.GetFromJsonAsync<MyStatsDto>("/api/stats/me");
        var football = stats!.Stats.Single(s => s.SportId == FootballSportId);
        Assert.Equal(1, football.Played);
        Assert.Equal(1, football.Wins);
        Assert.Equal(0, football.Losses);
        Assert.Equal(0, football.Draws);
    }

    [Fact]
    public async Task Claiming_an_occupied_slot_returns_conflict()
    {
        var owner = await _factory.CreateUserAsync($"race-a-{Guid.NewGuid():N}@test.com", "Race A", $"racea{Guid.NewGuid():N}"[..12]);
        var friend = await _factory.CreateUserAsync($"race-b-{Guid.NewGuid():N}@test.com", "Race B", $"raceb{Guid.NewGuid():N}"[..12]);

        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(friend.UserId, null));
        var accept = await friend.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null);
        accept.EnsureSuccessStatusCode();

        var ev = await CreateEventAsync(owner, null, "Race Match", [friend.UserId]);

        var friendClaim = await friend.Client.PostAsync($"/api/events/{ev.Id}/positions/h_gk/claim", null);
        friendClaim.EnsureSuccessStatusCode();

        var ownerClaim = await owner.Client.PostAsync($"/api/events/{ev.Id}/positions/h_gk/claim", null);
        Assert.Equal(HttpStatusCode.Conflict, ownerClaim.StatusCode);
    }

    [Fact]
    public async Task Past_open_match_moves_to_incomplete_and_can_be_rescheduled()
    {
        var owner = await _factory.CreateUserAsync($"inc-{Guid.NewGuid():N}@test.com", "Inc Owner", $"inc{Guid.NewGuid():N}"[..12]);
        var ev = await CreateEventAsync(owner, null, "Stale Match", scheduledAt: DateTimeOffset.UtcNow.AddHours(-2));

        var incomplete = await owner.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events/incomplete");
        Assert.Contains(incomplete!, e => e.Id == ev.Id && e.Status == "Incomplete");

        var upcoming = await owner.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events");
        Assert.DoesNotContain(upcoming!, e => e.Id == ev.Id);

        var future = DateTimeOffset.UtcNow.AddDays(3);
        var updated = await owner.Client.PatchAsJsonAsync($"/api/events/{ev.Id}", new UpdateEventRequest(ScheduledAt: future));
        updated.EnsureSuccessStatusCode();
        var detail = await updated.Content.ReadFromJsonAsync<EventDetailDto>();
        Assert.Equal("Open", detail!.Status);

        upcoming = await owner.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events");
        Assert.Contains(upcoming!, e => e.Id == ev.Id);
        incomplete = await owner.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events/incomplete");
        Assert.DoesNotContain(incomplete!, e => e.Id == ev.Id);
    }

    [Fact]
    public async Task Lock_unlock_and_pending_result_gate_create()
    {
        var owner = await _factory.CreateUserAsync($"lock-{Guid.NewGuid():N}@test.com", "Lock Owner", $"lock{Guid.NewGuid():N}"[..12]);
        var ev = await CreateEventAsync(owner, null, "Full Match", formatId: FiveASideFormatId);

        await FillAllPositionsAsync(ev.Id, owner.UserId);

        var locked = await owner.Client.PostAsync($"/api/events/{ev.Id}/lock", null);
        locked.EnsureSuccessStatusCode();
        var lockedDetail = await locked.Content.ReadFromJsonAsync<EventDetailDto>();
        Assert.Equal("Locked", lockedDetail!.Status);
        Assert.True(lockedDetail.CanUnlock);
        Assert.False(lockedDetail.CanLock);

        var unlocked = await owner.Client.PostAsync($"/api/events/{ev.Id}/unlock", null);
        unlocked.EnsureSuccessStatusCode();
        Assert.Equal("Open", (await unlocked.Content.ReadFromJsonAsync<EventDetailDto>())!.Status);

        // Lock again, then move past kickoff → pending result blocks create.
        (await owner.Client.PostAsync($"/api/events/{ev.Id}/lock", null)).EnsureSuccessStatusCode();
        await SetScheduledAtAsync(ev.Id, DateTimeOffset.UtcNow.AddHours(-1));

        var pending = await owner.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events/pending-results");
        Assert.Contains(pending!, e => e.Id == ev.Id && e.IsPendingResult);

        var blocked = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(FiveASideFormatId, "Another", DateTimeOffset.UtcNow.AddDays(2), null, null, null));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);

        var unlockPast = await owner.Client.PostAsync($"/api/events/{ev.Id}/unlock", null);
        Assert.Equal(HttpStatusCode.Conflict, unlockPast.StatusCode);

        (await owner.Client.PostAsJsonAsync($"/api/events/{ev.Id}/result", new SubmitResultRequest(2, 2))).EnsureSuccessStatusCode();

        var history = await owner.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events/past");
        Assert.Contains(history!, e => e.Id == ev.Id && e.Status == "Completed");

        var allowed = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(FiveASideFormatId, "After result", DateTimeOffset.UtcNow.AddDays(2), null, null, null));
        allowed.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Non_owner_can_leave_incomplete_match()
    {
        var owner = await _factory.CreateUserAsync($"leave-o-{Guid.NewGuid():N}@test.com", "Leave Owner", $"leaveo{Guid.NewGuid():N}"[..12]);
        var friend = await _factory.CreateUserAsync($"leave-f-{Guid.NewGuid():N}@test.com", "Leave Friend", $"leavef{Guid.NewGuid():N}"[..12]);

        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(friend.UserId, null));
        (await friend.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null)).EnsureSuccessStatusCode();

        var ev = await CreateEventAsync(owner, null, "Leave Match", [friend.UserId], DateTimeOffset.UtcNow.AddHours(-3));

        var incomplete = await friend.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events/incomplete");
        Assert.Contains(incomplete!, e => e.Id == ev.Id);

        var leave = await friend.Client.DeleteAsync($"/api/events/{ev.Id}/participation");
        leave.EnsureSuccessStatusCode();

        incomplete = await friend.Client.GetFromJsonAsync<List<EventSummaryDto>>("/api/events/incomplete");
        Assert.DoesNotContain(incomplete!, e => e.Id == ev.Id);
    }

    private async Task PrepareLockedPastAsync(Guid eventId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CityLeagueDbContext>();
        var ev = await db.Events.Include(e => e.Positions).FirstAsync(e => e.Id == eventId);
        ev.Status = EventStatus.Locked;
        ev.ScheduledAt = DateTimeOffset.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();
    }

    private async Task SetScheduledAtAsync(Guid eventId, DateTimeOffset when)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CityLeagueDbContext>();
        var ev = await db.Events.FirstAsync(e => e.Id == eventId);
        ev.ScheduledAt = when;
        await db.SaveChangesAsync();
    }

    private async Task FillAllPositionsAsync(Guid eventId, Guid fillerUserId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CityLeagueDbContext>();
        var positions = await db.EventPositions.Where(p => p.EventId == eventId).ToListAsync();
        foreach (var p in positions)
        {
            p.UserId = fillerUserId;
            p.ClaimedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    private static async Task<EventDetailDto> CreateEventAsync(
        TestUser owner,
        Guid? seriesId,
        string title,
        IReadOnlyList<Guid>? invites = null,
        DateTimeOffset? scheduledAt = null,
        int formatId = SevenASideFormatId)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(formatId, title, scheduledAt ?? DateTimeOffset.UtcNow.AddDays(1), "Test Pitch", seriesId, invites));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventDetailDto>())!;
    }
}
