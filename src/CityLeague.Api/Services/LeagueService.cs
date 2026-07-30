using CityLeague.Api.Common;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Core.Enums;
using CityLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Services;

public class LeagueService(CityLeagueDbContext db, ApiMapper mapper)
{
    public async Task<List<LeagueDto>> GetMyLeaguesAsync(Guid userId, CancellationToken ct)
    {
        var leagues = await db.Leagues
            .Include(l => l.Sport)
            .Include(l => l.Participants)
            .Include(l => l.Teams).ThenInclude(t => t.Stats)
            .Where(l => (l.Status == LeagueStatus.Draft || l.Status == LeagueStatus.Active) &&
                        (l.OwnerUserId == userId || l.Participants.Any(p => p.UserId == userId)))
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var result = new List<LeagueDto>(leagues.Count);
        foreach (var league in leagues)
            result.Add(await MapSummaryAsync(league, userId, ct));
        return result;
    }

    public async Task<List<LeagueDto>> GetCompletedLeaguesAsync(Guid userId, CancellationToken ct)
    {
        var leagues = await db.Leagues
            .Include(l => l.Sport)
            .Include(l => l.Participants)
            .Include(l => l.Teams).ThenInclude(t => t.Stats)
            .Where(l => l.Status == LeagueStatus.Terminated &&
                        (l.OwnerUserId == userId || l.Participants.Any(p => p.UserId == userId)))
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var result = new List<LeagueDto>(leagues.Count);
        foreach (var league in leagues)
            result.Add(await MapSummaryAsync(league, userId, ct));
        return result;
    }

    public async Task<LeagueDetailDto> GetDetailAsync(Guid userId, Guid leagueId, CancellationToken ct)
    {
        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        EnsureMember(league, userId);
        return await MapDetailAsync(league, userId, ct);
    }

    public async Task<LeagueDto> CreateAsync(Guid ownerId, CreateLeagueRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw ServiceException.BadRequest("League name is required.");
        if (string.IsNullOrWhiteSpace(request.Team1Name) || string.IsNullOrWhiteSpace(request.Team2Name))
            throw ServiceException.BadRequest("Both team names are required.");
        if (string.Equals(request.Team1Name.Trim(), request.Team2Name.Trim(), StringComparison.OrdinalIgnoreCase))
            throw ServiceException.BadRequest("Team names must be different.");
        if (request.PlannedMatchCount is < 1 or > 200)
            throw ServiceException.BadRequest("Planned matches must be between 1 and 200.");

        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Id == request.SportId, ct)
            ?? throw ServiceException.BadRequest("Unknown sport.");
        if (sport.Availability != SportAvailability.Enabled)
            throw ServiceException.BadRequest($"{sport.Name} is coming soon.");

        if (request.Team1LeaderUserId is Guid l1 && request.Team2LeaderUserId is Guid l2 && l1 == l2)
            throw ServiceException.BadRequest("Each team needs a different leader.");

        var league = new League
        {
            Name = request.Name.Trim(),
            OwnerUserId = ownerId,
            SportId = request.SportId,
            Status = LeagueStatus.Draft,
            PlannedMatchCount = request.PlannedMatchCount,
        };
        db.Leagues.Add(league);

        var team1 = new LeagueTeam
        {
            LeagueId = league.Id,
            Name = request.Team1Name.Trim(),
            SortOrder = 0,
            LeaderUserId = request.Team1LeaderUserId,
        };
        var team2 = new LeagueTeam
        {
            LeagueId = league.Id,
            Name = request.Team2Name.Trim(),
            SortOrder = 1,
            LeaderUserId = request.Team2LeaderUserId,
        };
        db.LeagueTeams.Add(team1);
        db.LeagueTeams.Add(team2);
        db.TeamSportStats.Add(new TeamSportStats { LeagueTeamId = team1.Id });
        db.TeamSportStats.Add(new TeamSportStats { LeagueTeamId = team2.Id });

        var memberIds = new HashSet<Guid> { ownerId };
        if (request.ParticipantUserIds is not null)
        {
            foreach (var id in request.ParticipantUserIds)
                memberIds.Add(id);
        }
        if (request.Team1LeaderUserId is Guid t1Leader)
            memberIds.Add(t1Leader);
        if (request.Team2LeaderUserId is Guid t2Leader)
            memberIds.Add(t2Leader);

        foreach (var userId in memberIds)
        {
            if (!await db.Users.AnyAsync(u => u.Id == userId, ct))
                throw ServiceException.BadRequest("One or more participants were not found.");

            Guid? teamId = null;
            if (userId == request.Team1LeaderUserId)
                teamId = team1.Id;
            else if (userId == request.Team2LeaderUserId)
                teamId = team2.Id;

            db.LeagueParticipants.Add(new LeagueParticipant
            {
                LeagueId = league.Id,
                UserId = userId,
                LeagueTeamId = teamId,
            });
        }

        await db.SaveChangesAsync(ct);

        league = await LoadLeagueGraphAsync(league.Id, ct)
            ?? throw ServiceException.NotFound("League not found.");
        return await MapSummaryAsync(league, ownerId, ct);
    }

    public async Task DeleteAsync(Guid userId, Guid leagueId, CancellationToken ct)
    {
        var league = await db.Leagues
            .Include(l => l.Teams)
            .Include(l => l.Participants)
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        if (league.OwnerUserId != userId)
            throw ServiceException.Forbidden("Only the league owner can delete it.");

        var completedMatches = await GetCompletedMatchCountAsync(leagueId, ct);
        if (completedMatches > 0)
            throw ServiceException.Conflict("This league has finished matches. End the league instead of deleting it.");

        db.Leagues.Remove(league);
        await db.SaveChangesAsync(ct);
    }

    public async Task<LeagueDetailDto> StartAsync(Guid userId, Guid leagueId, CancellationToken ct)
    {
        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        EnsureCanManageLifecycle(league, userId);
        if (!league.IsDraft())
            throw ServiceException.Conflict("This league has already started or finished.");

        var teams = league.Teams.OrderBy(t => t.SortOrder).ToList();
        if (teams.Count < 2)
            throw ServiceException.Conflict("League needs two teams before it can start.");
        if (teams.Any(t => t.LeaderUserId is null))
            throw ServiceException.Conflict("Both team leaders must be set before the league starts.");

        // Ensure leaders are on their teams.
        foreach (var team in teams)
        {
            var leaderPart = league.Participants.FirstOrDefault(p => p.UserId == team.LeaderUserId);
            if (leaderPart is null)
            {
                leaderPart = new LeagueParticipant
                {
                    LeagueId = league.Id,
                    UserId = team.LeaderUserId!.Value,
                    LeagueTeamId = team.Id,
                };
                db.LeagueParticipants.Add(leaderPart);
                league.Participants.Add(leaderPart);
            }
            else
            {
                leaderPart.LeagueTeamId = team.Id;
            }
        }

        league.Status = LeagueStatus.Active;
        league.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(league, userId, ct);
    }

    public async Task<LeagueDetailDto> EndAsync(Guid userId, Guid leagueId, CancellationToken ct)
    {
        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        EnsureCanManageLifecycle(league, userId);
        if (league.Status == LeagueStatus.Terminated)
            throw ServiceException.Conflict("This league has already finished.");

        league.Status = LeagueStatus.Terminated;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(league, userId, ct);
    }

    public async Task<LeagueDetailDto> ExtendAsync(Guid userId, Guid leagueId, ExtendLeagueRequest request, CancellationToken ct)
    {
        if (request.AdditionalMatches is < 1 or > 100)
            throw ServiceException.BadRequest("Add between 1 and 100 matches.");

        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        EnsureCanManageLifecycle(league, userId);
        if (league.Status != LeagueStatus.Active)
            throw ServiceException.Conflict("Only a running league can be extended.");

        league.PlannedMatchCount += request.AdditionalMatches;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(league, userId, ct);
    }

    public async Task<LeagueDetailDto> AddParticipantsAsync(
        Guid userId, Guid leagueId, AddLeagueParticipantsRequest request, CancellationToken ct)
    {
        if (request.UserIds is null || request.UserIds.Count == 0)
            throw ServiceException.BadRequest("Select at least one person to add.");

        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        EnsureCanManageRoster(league, userId);
        if (league.Status == LeagueStatus.Terminated)
            throw ServiceException.Conflict("Cannot add people to a finished league.");

        var existing = league.Participants.Select(p => p.UserId).ToHashSet();
        foreach (var addId in request.UserIds.Distinct())
        {
            if (existing.Contains(addId))
                continue;
            if (!await db.Users.AnyAsync(u => u.Id == addId, ct))
                throw ServiceException.BadRequest("One or more users were not found.");

            var part = new LeagueParticipant { LeagueId = league.Id, UserId = addId };
            db.LeagueParticipants.Add(part);
            league.Participants.Add(part);
        }

        await db.SaveChangesAsync(ct);
        league = await LoadLeagueGraphAsync(leagueId, ct) ?? league;
        return await MapDetailAsync(league, userId, ct);
    }

    public async Task<LeagueDetailDto> MoveParticipantAsync(
        Guid actorId, Guid leagueId, Guid targetUserId, MoveLeagueParticipantRequest request, CancellationToken ct)
    {
        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        EnsureMember(league, actorId);
        if (league.Status == LeagueStatus.Terminated)
            throw ServiceException.Conflict("This league has finished.");

        // Users move themselves; owners/leaders can also place others (except locked leaders).
        if (actorId != targetUserId && !IsOwnerOrLeader(league, actorId))
            throw ServiceException.Forbidden("You can only change your own team.");

        var participant = league.Participants.FirstOrDefault(p => p.UserId == targetUserId)
            ?? throw ServiceException.NotFound("That person is not in this league.");

        if (IsTeamLeaderUser(league, targetUserId) && !league.IsDraft())
            throw ServiceException.Conflict("Team leaders cannot change teams after the league starts.");

        if (request.LeagueTeamId is Guid teamId)
        {
            var team = league.Teams.FirstOrDefault(t => t.Id == teamId)
                ?? throw ServiceException.BadRequest("Unknown team.");
            participant.LeagueTeamId = team.Id;
        }
        else
        {
            participant.LeagueTeamId = null;
        }

        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(league, actorId, ct);
    }

    public async Task<LeagueDetailDto> SetTeamLeaderAsync(
        Guid userId, Guid leagueId, Guid teamId, SetLeagueTeamLeaderRequest request, CancellationToken ct)
    {
        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        if (league.OwnerUserId != userId && !IsTeamLeaderUser(league, userId))
            throw ServiceException.Forbidden("Only the owner or a team leader can assign leaders.");
        if (!league.IsDraft())
            throw ServiceException.Conflict("Team leaders cannot be changed after the league starts.");

        var team = league.Teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw ServiceException.NotFound("Team not found.");
        var other = league.Teams.FirstOrDefault(t => t.Id != teamId);
        if (other?.LeaderUserId == request.UserId)
            throw ServiceException.BadRequest("That person already leads the other team.");

        if (!await db.Users.AnyAsync(u => u.Id == request.UserId, ct))
            throw ServiceException.BadRequest("User not found.");

        var participant = league.Participants.FirstOrDefault(p => p.UserId == request.UserId);
        if (participant is null)
        {
            participant = new LeagueParticipant
            {
                LeagueId = league.Id,
                UserId = request.UserId,
                LeagueTeamId = team.Id,
            };
            db.LeagueParticipants.Add(participant);
            league.Participants.Add(participant);
        }
        else
        {
            participant.LeagueTeamId = team.Id;
        }

        team.LeaderUserId = request.UserId;
        await db.SaveChangesAsync(ct);
        league = await LoadLeagueGraphAsync(leagueId, ct) ?? league;
        return await MapDetailAsync(league, userId, ct);
    }

    public async Task<LeagueDetailDto> UploadTeamLogoAsync(
        Guid userId, Guid leagueId, Guid teamId, Stream content, string fileName, string contentType,
        IAvatarStorage storage, CancellationToken ct)
    {
        var league = await LoadLeagueGraphAsync(leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        var team = league.Teams.FirstOrDefault(t => t.Id == teamId)
            ?? throw ServiceException.NotFound("Team not found.");

        var canUpload = league.OwnerUserId == userId
                        || team.LeaderUserId == userId
                        || (league.Status == LeagueStatus.Draft && IsMember(league, userId));
        if (!canUpload)
            throw ServiceException.Forbidden("Only the owner or that team's leader can set the logo.");
        if (league.Status == LeagueStatus.Terminated)
            throw ServiceException.Conflict("This league has finished.");

        var ext = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(ext))
            ext = contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ? ".jpg" : ".png";
        var blobPath = $"league-logos/{leagueId:N}/{teamId:N}/{Guid.NewGuid():N}{ext}";
        await storage.SaveAsync(blobPath, content, contentType, ct);
        team.LogoBlobUrl = blobPath;
        await db.SaveChangesAsync(ct);
        return await MapDetailAsync(league, userId, ct);
    }

    /// <summary>Called when a match result is submitted for an event linked to a league.</summary>
    public async Task OnLeagueMatchCompletedAsync(Guid eventId, WinningSide winningSide, CancellationToken ct)
    {
        var link = await db.LeagueEvents
            .Include(le => le.League!)
                .ThenInclude(l => l.Teams)
                    .ThenInclude(t => t.Stats)
            .FirstOrDefaultAsync(le => le.EventId == eventId, ct);
        if (link?.League is null || link.League.Status == LeagueStatus.Terminated)
            return;

        var teams = link.League.Teams.OrderBy(t => t.SortOrder).ToList();
        if (teams.Count < 2)
            return;

        var home = teams[0];
        var away = teams[1];
        ApplyTeamResult(home, MatchSide.Home, winningSide);
        ApplyTeamResult(away, MatchSide.Away, winningSide);

        var completed = await GetCompletedMatchCountAsync(link.LeagueId, ct);
        if (completed >= link.League.PlannedMatchCount && link.League.Status == LeagueStatus.Active)
            link.League.Status = LeagueStatus.Terminated;

        await db.SaveChangesAsync(ct);
    }

    public async Task ValidateLinkAsync(Guid leagueId, Guid actorId, CancellationToken ct)
    {
        var league = await db.Leagues
            .Include(l => l.Participants)
            .Include(l => l.Teams)
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct)
            ?? throw ServiceException.BadRequest("Unknown league.");

        EnsureMember(league, actorId);
        if (league.Status == LeagueStatus.Terminated)
            throw ServiceException.Conflict("Cannot add matches to a finished league.");
        if (league.IsDraft())
            throw ServiceException.Conflict("Start the league before adding matches.");
    }

    public async Task LinkEventAsync(Guid leagueId, Guid eventId, Guid actorId, CancellationToken ct)
    {
        await ValidateLinkAsync(leagueId, actorId, ct);

        var exists = await db.LeagueEvents.AnyAsync(le => le.LeagueId == leagueId && le.EventId == eventId, ct);
        if (exists)
            return;

        db.LeagueEvents.Add(new LeagueEvent { LeagueId = leagueId, EventId = eventId });
        await db.SaveChangesAsync(ct);
    }

    private static void ApplyTeamResult(LeagueTeam team, MatchSide side, WinningSide winning)
    {
        team.Stats ??= new TeamSportStats { LeagueTeamId = team.Id };
        team.Stats.Played++;
        if (winning == WinningSide.Draw)
            team.Stats.Draws++;
        else if ((winning == WinningSide.Home && side == MatchSide.Home) ||
                 (winning == WinningSide.Away && side == MatchSide.Away))
            team.Stats.Wins++;
        else
            team.Stats.Losses++;
    }

    private async Task<League?> LoadLeagueGraphAsync(Guid leagueId, CancellationToken ct)
        => await db.Leagues
            .Include(l => l.Sport)
            .Include(l => l.Teams).ThenInclude(t => t.Stats)
            .Include(l => l.Teams).ThenInclude(t => t.LeaderUser)
            .Include(l => l.Participants).ThenInclude(p => p.User)
            .Include(l => l.Participants).ThenInclude(p => p.LeagueTeam)
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct);

    private async Task<int> GetCompletedMatchCountAsync(Guid leagueId, CancellationToken ct)
        => await db.LeagueEvents
            .Where(le => le.LeagueId == leagueId)
            .Join(db.Events, le => le.EventId, e => e.Id, (_, e) => e)
            .CountAsync(e => e.Status == EventStatus.Completed, ct);

    private async Task<List<LeagueMatchResultDto>> GetMatchResultsAsync(League league, CancellationToken ct)
    {
        var teams = league.Teams.OrderBy(t => t.SortOrder).ToList();
        var homeName = teams.ElementAtOrDefault(0)?.Name;
        var awayName = teams.ElementAtOrDefault(1)?.Name;

        var rows = await db.LeagueEvents
            .Where(le => le.LeagueId == league.Id)
            .Join(db.Events.Include(e => e.Result), le => le.EventId, e => e.Id, (_, e) => e)
            .Where(e => e.Status == EventStatus.Completed && e.Result != null)
            .OrderByDescending(e => e.Result!.SubmittedAt)
            .ToListAsync(ct);

        return rows.Select(e => new LeagueMatchResultDto(
            e.Id,
            e.Title,
            e.ScheduledAt,
            e.Result!.HomeScore,
            e.Result.AwayScore,
            e.Result.WinningSide.ToString(),
            homeName,
            awayName,
            e.Result.SubmittedAt)).ToList();
    }

    private async Task<LeagueDto> MapSummaryAsync(League l, Guid userId, CancellationToken ct)
    {
        var completed = await GetCompletedMatchCountAsync(l.Id, ct);
        var planned = Math.Max(l.PlannedMatchCount, 1);
        var teams = l.Teams.OrderBy(t => t.SortOrder).ToList();
        var isOwner = l.OwnerUserId == userId;
        var isLeader = IsTeamLeaderUser(l, userId);
        var hasStarted = l.HasStarted();
        var isOpen = l.Status is LeagueStatus.Draft or LeagueStatus.Active;
        var bothLeadersSet = l.Teams.Count >= 2 && l.Teams.All(t => t.LeaderUserId is not null);

        return new LeagueDto(
            l.Id,
            l.Name,
            l.Sport!.Key,
            l.Sport.Name,
            StatusLabel(l),
            isOwner,
            isLeader,
            l.Participants.Count,
            l.Teams.Count,
            completed,
            l.PlannedMatchCount,
            Math.Clamp(completed / (double)planned, 0, 1),
            teams.ElementAtOrDefault(0)?.Name,
            teams.ElementAtOrDefault(1)?.Name,
            hasStarted,
            isOwner && isOpen && completed == 0,
            (isOwner || isLeader) && l.Status == LeagueStatus.Active,
            (isOwner || isLeader) && l.Status == LeagueStatus.Active,
            (isOwner || isLeader) && l.IsDraft() && bothLeadersSet);
    }

    private async Task<LeagueDetailDto> MapDetailAsync(League l, Guid userId, CancellationToken ct)
    {
        var completed = await GetCompletedMatchCountAsync(l.Id, ct);
        var planned = Math.Max(l.PlannedMatchCount, 1);
        var isOwner = l.OwnerUserId == userId;
        var isLeader = IsTeamLeaderUser(l, userId);
        var hasStarted = l.HasStarted();
        var isOpen = l.Status is LeagueStatus.Draft or LeagueStatus.Active;
        var bothLeadersSet = l.Teams.Count >= 2 && l.Teams.All(t => t.LeaderUserId is not null);
        var results = await GetMatchResultsAsync(l, ct);

        var teamDtos = l.Teams
            .OrderBy(t => t.SortOrder)
            .Select(t =>
            {
                var members = l.Participants.Count(p => p.LeagueTeamId == t.Id);
                return new LeagueTeamDto(
                    t.Id,
                    t.Name,
                    t.SortOrder,
                    mapper.ToPublicAvatarUrl(t.LogoBlobUrl),
                    t.LeaderUserId,
                    t.LeaderUser?.UniqueHandle,
                    t.LeaderUser?.DisplayName,
                    t.Stats?.Played ?? 0,
                    t.Stats?.Wins ?? 0,
                    t.Stats?.Losses ?? 0,
                    t.Stats?.Draws ?? 0,
                    members);
            })
            .ToList();

        var participantDtos = l.Participants
            .OrderBy(p => p.User?.DisplayName)
            .Select(p =>
            {
                var isTeamLeader = IsTeamLeaderUser(l, p.UserId);
                var canChange = !isTeamLeader || l.IsDraft();
                return new LeagueParticipantDto(
                    p.UserId,
                    p.User?.UniqueHandle ?? string.Empty,
                    p.User?.DisplayName ?? string.Empty,
                    mapper.ToPublicAvatarUrl(p.User?.AvatarBlobUrl),
                    p.LeagueTeamId,
                    p.LeagueTeam?.Name,
                    isTeamLeader,
                    canChange);
            })
            .ToList();

        return new LeagueDetailDto(
            l.Id,
            l.Name,
            l.Sport!.Key,
            l.Sport.Name,
            l.SportId,
            StatusLabel(l),
            isOwner,
            isLeader,
            completed,
            l.PlannedMatchCount,
            Math.Clamp(completed / (double)planned, 0, 1),
            hasStarted,
            isOwner && isOpen && completed == 0,
            (isOwner || isLeader) && l.Status == LeagueStatus.Active,
            (isOwner || isLeader) && l.Status == LeagueStatus.Active,
            (isOwner || isLeader) && l.IsDraft() && bothLeadersSet,
            (isOwner || isLeader) && isOpen,
            (isOwner || isLeader) && isOpen,
            l.CreatedAt,
            l.StartedAt,
            teamDtos,
            participantDtos,
            results);
    }

    private static string StatusLabel(League l) => l.Status switch
    {
        LeagueStatus.Terminated => "Finished",
        LeagueStatus.Draft => "Draft",
        LeagueStatus.Active when l.StartedAt is null => "Draft",
        _ => "Active",
    };

    private static void EnsureMember(League league, Guid userId)
    {
        if (!IsMember(league, userId))
            throw ServiceException.Forbidden("You are not in this league.");
    }

    private static bool IsMember(League league, Guid userId)
        => league.OwnerUserId == userId || league.Participants.Any(p => p.UserId == userId);

    private static bool IsTeamLeaderUser(League league, Guid userId)
        => league.Teams.Any(t => t.LeaderUserId == userId);

    private static bool IsOwnerOrLeader(League league, Guid userId)
        => league.OwnerUserId == userId || IsTeamLeaderUser(league, userId);

    private static void EnsureCanManageLifecycle(League league, Guid userId)
    {
        if (!IsOwnerOrLeader(league, userId))
            throw ServiceException.Forbidden("Only the owner or a team leader can do that.");
    }

    private static void EnsureCanManageRoster(League league, Guid userId)
    {
        if (!IsOwnerOrLeader(league, userId))
            throw ServiceException.Forbidden("Only the owner or a team leader can add people.");
    }
}

internal static class LeagueEntityExtensions
{
    public static bool IsDraft(this League league)
        => league.Status == LeagueStatus.Draft;

    /// <summary>True once the league has left Draft (including legacy Active rows).</summary>
    public static bool HasStarted(this League league)
        => league.Status == LeagueStatus.Active || league.StartedAt is not null;
}
