using CityLeague.Api.Auth;
using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/series")]
[Authorize]
public class SeriesController(EventService events, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SeriesDto>>> List(CancellationToken ct)
        => await events.GetSeriesAsync(currentUser.UserId, ct);

    [HttpPost]
    public async Task<ActionResult<SeriesDto>> Create([FromBody] CreateSeriesRequest request, CancellationToken ct)
        => await events.CreateSeriesAsync(currentUser.UserId, request, ct);
}
