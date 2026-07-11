using CityLeague.Api.Auth;
using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/leagues")]
[Authorize]
public class LeaguesController(LeagueService leagues, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> List(CancellationToken ct)
        => await leagues.GetMyLeaguesAsync(currentUser.UserId, ct);

    [HttpGet("completed")]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> Completed(CancellationToken ct)
        => await leagues.GetCompletedLeaguesAsync(currentUser.UserId, ct);

    [HttpPost]
    public async Task<ActionResult<LeagueDto>> Create([FromBody] CreateLeagueRequest request, CancellationToken ct)
        => await leagues.CreateAsync(currentUser.UserId, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await leagues.DeleteAsync(currentUser.UserId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<LeagueDto>> End(Guid id, CancellationToken ct)
        => await leagues.EndAsync(currentUser.UserId, id, ct);
}
