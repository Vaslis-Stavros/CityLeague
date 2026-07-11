using CityLeague.Api.Common;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Core.Enums;
using CityLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Services;

public class LeagueService(CityLeagueDbContext db)
{
    public async Task<List<LeagueDto>> GetMyLeaguesAsync(Guid userId, CancellationToken ct)
    {
        var leagues = await db.Leagues
            .Include(l => l.Sport)
            .Include(l => l.Participants)
            .Include(l => l.Teams)
            .Where(l => l.Status == LeagueStatus.Active &&
                        (l.OwnerUserId == userId || l.Participants.Any(p => p.UserId == userId)))
            .OrderBy(l => l.Name)
            .ToListAsync(ct);

        var result = new List<LeagueDto>(leagues.Count);
        foreach (var league in leagues)
            result.Add(await MapAsync(league, userId, ct));
        return result;
    }

    public async Task<List<LeagueDto>> GetCompletedLeaguesAsync(Guid userId, CancellationToken ct)
    {
        var leagues = await db.Leagues
            .Include(l => l.Sport)
            .Include(l => l.Participants)
            .Include(l => l.Teams)
            .Where(l => l.Status == LeagueStatus.Terminated &&
                        (l.OwnerUserId == userId || l.Participants.Any(p => p.UserId == userId)))
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

        var result = new List<LeagueDto>(leagues.Count);
        foreach (var league in leagues)
            result.Add(await MapAsync(league, userId, ct));
        return result;
    }

    public async Task<LeagueDto> CreateAsync(Guid ownerId, CreateLeagueRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw ServiceException.BadRequest("League name is required.");

        var sport = await db.Sports.FirstOrDefaultAsync(s => s.Id == request.SportId, ct)
            ?? throw ServiceException.BadRequest("Unknown sport.");

        var league = new League
        {
            Name = request.Name.Trim(),
            OwnerUserId = ownerId,
            SportId = request.SportId,
            Status = LeagueStatus.Active,
        };
        db.Leagues.Add(league);

        db.LeagueParticipants.Add(new LeagueParticipant
        {
            LeagueId = league.Id,
            UserId = ownerId,
        });

        await db.SaveChangesAsync(ct);

        league.Sport = sport;
        league.Participants = [new LeagueParticipant { UserId = ownerId }];
        return await MapAsync(league, ownerId, ct);
    }

    public async Task DeleteAsync(Guid ownerId, Guid leagueId, CancellationToken ct)
    {
        var league = await db.Leagues.FirstOrDefaultAsync(l => l.Id == leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        if (league.OwnerUserId != ownerId)
            throw ServiceException.Forbidden("Only the league owner can delete it.");

        var completedMatches = await GetCompletedMatchCountAsync(leagueId, ct);
        if (completedMatches > 0)
            throw ServiceException.Conflict("This league has finished matches. End the league instead of deleting it.");

        db.Leagues.Remove(league);
        await db.SaveChangesAsync(ct);
    }

    public async Task<LeagueDto> EndAsync(Guid ownerId, Guid leagueId, CancellationToken ct)
    {
        var league = await db.Leagues
            .Include(l => l.Sport)
            .Include(l => l.Participants)
            .Include(l => l.Teams)
            .FirstOrDefaultAsync(l => l.Id == leagueId, ct)
            ?? throw ServiceException.NotFound("League not found.");

        if (league.OwnerUserId != ownerId)
            throw ServiceException.Forbidden("Only the league owner can end it.");

        var completedMatches = await GetCompletedMatchCountAsync(leagueId, ct);
        if (completedMatches == 0)
            throw ServiceException.Conflict("This league has no finished matches yet. You can delete it instead.");

        league.Status = LeagueStatus.Terminated;
        await db.SaveChangesAsync(ct);
        return await MapAsync(league, ownerId, ct);
    }

    private async Task<int> GetCompletedMatchCountAsync(Guid leagueId, CancellationToken ct)
        => await db.LeagueEvents
            .Where(le => le.LeagueId == leagueId)
            .Join(db.Events, le => le.EventId, e => e.Id, (_, e) => e)
            .CountAsync(e => e.Status == EventStatus.Completed, ct);

    private async Task<LeagueDto> MapAsync(League l, Guid userId, CancellationToken ct)
    {
        var completed = await GetCompletedMatchCountAsync(l.Id, ct);
        var isOwner = l.OwnerUserId == userId;

        return new LeagueDto(
            l.Id,
            l.Name,
            l.Sport!.Key,
            l.Sport.Name,
            l.Status.ToString(),
            isOwner,
            l.Participants.Count,
            l.Teams.Count,
            completed,
            isOwner && l.Status == LeagueStatus.Active && completed == 0,
            isOwner && l.Status == LeagueStatus.Active && completed > 0);
    }
}
