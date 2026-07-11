using CityLeague.Api.Auth;
using CityLeague.Api.Common;
using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize]
public class StatsController(
    CityLeagueDbContext db,
    ICurrentUser currentUser,
    ApiMapper mapper) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MyStatsDto>> Mine(CancellationToken ct)
    {
        var me = currentUser.UserId;
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == me, ct)
            ?? throw ServiceException.NotFound("User not found.");

        var stats = await db.PlayerSportStats
            .Include(s => s.Sport)
            .Where(s => s.UserId == me)
            .OrderBy(s => s.Sport!.SortOrder)
            .Select(s => new PlayerStatsDto(
                s.SportId,
                s.Sport!.Key,
                s.Sport.Name,
                s.Played,
                s.Wins,
                s.Losses,
                s.Draws))
            .ToListAsync(ct);

        return new MyStatsDto(mapper.ToUserDto(user), stats);
    }
}
