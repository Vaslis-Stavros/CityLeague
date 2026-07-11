using System.Net;
using System.Net.Http.Json;
using CityLeague.Core.Dtos;
using Xunit;

namespace CityLeague.Api.Tests;

public class EventFlowTests : IClassFixture<TestAppFactory>
{
    private const int FootballSportId = 1;
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

        // Submit result for the first event, then the second is allowed.
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

        // Owner claims a home position, then submits a home win.
        var claim = await owner.Client.PostAsync($"/api/events/{ev.Id}/positions/h_gk/claim", null);
        claim.EnsureSuccessStatusCode();

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

        // Become contacts: owner requests, friend accepts.
        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(friend.UserId, null));
        var accept = await friend.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null);
        accept.EnsureSuccessStatusCode();

        var ev = await CreateEventAsync(owner, null, "Race Match", [friend.UserId]);

        // Friend claims the slot first.
        var friendClaim = await friend.Client.PostAsync($"/api/events/{ev.Id}/positions/h_gk/claim", null);
        friendClaim.EnsureSuccessStatusCode();

        // Owner tries the same slot -> conflict (single winner).
        var ownerClaim = await owner.Client.PostAsync($"/api/events/{ev.Id}/positions/h_gk/claim", null);
        Assert.Equal(HttpStatusCode.Conflict, ownerClaim.StatusCode);
    }

    private static async Task<EventDetailDto> CreateEventAsync(TestUser owner, Guid? seriesId, string title, IReadOnlyList<Guid>? invites = null)
    {
        var response = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(SevenASideFormatId, title, DateTimeOffset.UtcNow.AddDays(1), "Test Pitch", seriesId, invites));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<EventDetailDto>())!;
    }
}
