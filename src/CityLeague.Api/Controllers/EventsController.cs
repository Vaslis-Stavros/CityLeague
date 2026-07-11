using CityLeague.Api.Auth;
using CityLeague.Api.Common;
using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/events")]
[Authorize]
public class EventsController(EventService events, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> List(CancellationToken ct)
        => await events.GetMyEventsAsync(currentUser.UserId, ct);

    [HttpGet("past")]
    public async Task<ActionResult<IReadOnlyList<EventSummaryDto>>> Past(CancellationToken ct)
        => await events.GetPastEventsAsync(currentUser.UserId, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EventDetailDto>> Get(Guid id, CancellationToken ct)
        => await events.GetEventAsync(currentUser.UserId, id, ct) ?? throw ServiceException.NotFound("Event not found.");

    [HttpPost]
    public async Task<ActionResult<EventDetailDto>> Create([FromBody] CreateEventRequest request, CancellationToken ct)
    {
        var detail = await events.CreateEventAsync(currentUser.UserId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = detail.Id }, detail);
    }

    [HttpPost("{id:guid}/invite")]
    public async Task<ActionResult<IReadOnlyList<ParticipantDto>>> Invite(Guid id, [FromBody] InviteRequest request, CancellationToken ct)
        => Ok(await events.InviteAsync(currentUser.UserId, id, request.UserIds, ct));

    [HttpPost("{id:guid}/positions/{slotId}/claim")]
    public async Task<ActionResult<IReadOnlyList<PositionChangedDto>>> Claim(Guid id, string slotId, CancellationToken ct)
        => Ok(await events.ClaimPositionAsync(currentUser.UserId, id, slotId, ct));

    [HttpPost("{id:guid}/positions/{slotId}/release")]
    public async Task<ActionResult<IReadOnlyList<PositionChangedDto>>> Release(Guid id, string slotId, CancellationToken ct)
        => Ok(await events.ReleasePositionAsync(currentUser.UserId, id, slotId, ct));

    [HttpPost("{id:guid}/result")]
    public async Task<ActionResult<ResultDto>> SubmitResult(Guid id, [FromBody] SubmitResultRequest request, CancellationToken ct)
        => await events.SubmitResultAsync(currentUser.UserId, id, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await events.DeleteEventAsync(currentUser.UserId, id, ct);
        return NoContent();
    }
}
