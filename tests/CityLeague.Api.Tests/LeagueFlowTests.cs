using System.Net;
using System.Net.Http.Json;
using CityLeague.Core.Dtos;
using Xunit;

namespace CityLeague.Api.Tests;

public class LeagueFlowTests
{
    private const int FootballSportId = 1;
    private const int SevenASideFormatId = 3;

    [Fact]
    public async Task Create_start_move_extend_and_finish_league()
    {
        await using var factory = new TestAppFactory();
        var owner = await factory.CreateUserAsync($"lg-own-{Guid.NewGuid():N}@test.com", "Owner", $"own{Guid.NewGuid():N}"[..12]);
        var leaderA = await factory.CreateUserAsync($"lg-a-{Guid.NewGuid():N}@test.com", "Leader A", $"lda{Guid.NewGuid():N}"[..12]);
        var leaderB = await factory.CreateUserAsync($"lg-b-{Guid.NewGuid():N}@test.com", "Leader B", $"ldb{Guid.NewGuid():N}"[..12]);
        var floater = await factory.CreateUserAsync($"lg-f-{Guid.NewGuid():N}@test.com", "Floater", $"flt{Guid.NewGuid():N}"[..12]);

        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(leaderA.UserId, null));
        await leaderA.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null);
        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(leaderB.UserId, null));
        await leaderB.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null);
        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(floater.UserId, null));
        await floater.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null);

        var create = await owner.Client.PostAsJsonAsync("/api/leagues",
            new CreateLeagueRequest(
                "Summer Cup",
                FootballSportId,
                "North FC",
                "South United",
                PlannedMatchCount: 2,
                Team1LeaderUserId: leaderA.UserId,
                Team2LeaderUserId: leaderB.UserId,
                ParticipantUserIds: [floater.UserId]));
        create.EnsureSuccessStatusCode();
        var created = await create.Content.ReadFromJsonAsync<LeagueDto>();
        Assert.Equal("Draft", created!.Status);
        Assert.Equal(2, created.PlannedMatchCount);
        Assert.Equal(0, created.CompletedMatchCount);
        Assert.Equal(0, created.ProgressFraction);
        Assert.Equal("North FC", created.Team1Name);
        Assert.True(created.CanStart);

        var detail = await owner.Client.GetFromJsonAsync<LeagueDetailDto>($"/api/leagues/{created.Id}");
        Assert.Equal(2, detail!.Teams.Count);
        Assert.Contains(detail.Participants, p => p.UserId == floater.UserId);

        var team1 = detail.Teams.Single(t => t.SortOrder == 0);
        var team2 = detail.Teams.Single(t => t.SortOrder == 1);

        // Floater can move before/after start.
        var move = await floater.Client.PutAsJsonAsync(
            $"/api/leagues/{created.Id}/participants/{floater.UserId}/team",
            new MoveLeagueParticipantRequest(team1.Id));
        move.EnsureSuccessStatusCode();

        var started = await owner.Client.PostAsync($"/api/leagues/{created.Id}/start", null);
        started.EnsureSuccessStatusCode();
        var active = await started.Content.ReadFromJsonAsync<LeagueDetailDto>();
        Assert.Equal("Active", active!.Status);
        Assert.True(active.HasStarted);

        // Leader cannot change teams after start.
        var leaderMove = await leaderA.Client.PutAsJsonAsync(
            $"/api/leagues/{created.Id}/participants/{leaderA.UserId}/team",
            new MoveLeagueParticipantRequest(team2.Id));
        Assert.Equal(HttpStatusCode.Conflict, leaderMove.StatusCode);

        // Floater can still move.
        var floaterMove = await floater.Client.PutAsJsonAsync(
            $"/api/leagues/{created.Id}/participants/{floater.UserId}/team",
            new MoveLeagueParticipantRequest(team2.Id));
        floaterMove.EnsureSuccessStatusCode();

        // Add another person during the league.
        var late = await factory.CreateUserAsync($"lg-late-{Guid.NewGuid():N}@test.com", "Late Join", $"late{Guid.NewGuid():N}"[..12]);
        await owner.Client.PostAsJsonAsync("/api/contacts", new CreateContactRequest(late.UserId, null));
        await late.Client.PostAsync($"/api/contacts/{owner.UserId}/accept", null);
        var add = await leaderA.Client.PostAsJsonAsync($"/api/leagues/{created.Id}/participants",
            new AddLeagueParticipantsRequest([late.UserId]));
        add.EnsureSuccessStatusCode();

        // Play one match linked to the league.
        var evResponse = await owner.Client.PostAsJsonAsync("/api/events",
            new CreateEventRequest(SevenASideFormatId, "Cup Match 1", DateTimeOffset.UtcNow.AddDays(1),
                "Pitch", null, null, created.Id));
        evResponse.EnsureSuccessStatusCode();
        var ev = await evResponse.Content.ReadFromJsonAsync<EventDetailDto>();
        await owner.Client.PostAsync($"/api/events/{ev!.Id}/positions/h_gk/claim", null);
        var result = await owner.Client.PostAsJsonAsync($"/api/events/{ev.Id}/result", new SubmitResultRequest(2, 1));
        result.EnsureSuccessStatusCode();

        detail = await owner.Client.GetFromJsonAsync<LeagueDetailDto>($"/api/leagues/{created.Id}");
        Assert.Equal(1, detail!.CompletedMatchCount);
        Assert.Single(detail.MatchResults);
        Assert.Equal(0.5, detail.ProgressFraction, 2);
        Assert.Equal(1, detail.Teams.Single(t => t.SortOrder == 0).Wins);

        // Leader extends then finishes early.
        var extend = await leaderB.Client.PostAsJsonAsync($"/api/leagues/{created.Id}/extend",
            new ExtendLeagueRequest(3));
        extend.EnsureSuccessStatusCode();
        var extended = await extend.Content.ReadFromJsonAsync<LeagueDetailDto>();
        Assert.Equal(5, extended!.PlannedMatchCount);

        var finish = await leaderA.Client.PostAsync($"/api/leagues/{created.Id}/end", null);
        finish.EnsureSuccessStatusCode();
        var finished = await finish.Content.ReadFromJsonAsync<LeagueDetailDto>();
        Assert.Equal("Finished", finished!.Status);

        var completed = await owner.Client.GetFromJsonAsync<List<LeagueDto>>("/api/leagues/completed");
        Assert.Contains(completed!, l => l.Id == created.Id);
    }

    [Fact]
    public async Task Cannot_start_without_both_leaders()
    {
        await using var factory = new TestAppFactory();
        var owner = await factory.CreateUserAsync($"lg2-{Guid.NewGuid():N}@test.com", "Owner", $"o2{Guid.NewGuid():N}"[..12]);

        var create = await owner.Client.PostAsJsonAsync("/api/leagues",
            new CreateLeagueRequest("No Leaders", FootballSportId, "Alpha", "Beta", 5));
        create.EnsureSuccessStatusCode();
        var league = await create.Content.ReadFromJsonAsync<LeagueDto>();

        var start = await owner.Client.PostAsync($"/api/leagues/{league!.Id}/start", null);
        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
    }
}
