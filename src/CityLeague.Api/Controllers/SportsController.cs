using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/sports")]
[Authorize]
public class SportsController(CityLeagueDbContext db, ApiMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SportDto>>> List(CancellationToken ct)
    {
        var sports = await db.Sports
            .Include(s => s.Formats)
            .OrderBy(s => s.SortOrder)
            .ToListAsync(ct);

        return sports.Select(mapper.ToSportDto).ToList();
    }
}
