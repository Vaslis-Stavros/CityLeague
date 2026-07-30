using CityLeague.Api.Auth;
using CityLeague.Api.Services;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/leagues")]
[Authorize]
public class LeaguesController(LeagueService leagues, ICurrentUser currentUser, IAvatarStorage avatarStorage) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> List(CancellationToken ct)
        => await leagues.GetMyLeaguesAsync(currentUser.UserId, ct);

    [HttpGet("completed")]
    public async Task<ActionResult<IReadOnlyList<LeagueDto>>> Completed(CancellationToken ct)
        => await leagues.GetCompletedLeaguesAsync(currentUser.UserId, ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeagueDetailDto>> Get(Guid id, CancellationToken ct)
        => await leagues.GetDetailAsync(currentUser.UserId, id, ct);

    [HttpPost]
    public async Task<ActionResult<LeagueDto>> Create([FromBody] CreateLeagueRequest request, CancellationToken ct)
        => await leagues.CreateAsync(currentUser.UserId, request, ct);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await leagues.DeleteAsync(currentUser.UserId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/start")]
    public async Task<ActionResult<LeagueDetailDto>> Start(Guid id, CancellationToken ct)
        => await leagues.StartAsync(currentUser.UserId, id, ct);

    [HttpPost("{id:guid}/end")]
    public async Task<ActionResult<LeagueDetailDto>> End(Guid id, CancellationToken ct)
        => await leagues.EndAsync(currentUser.UserId, id, ct);

    [HttpPost("{id:guid}/extend")]
    public async Task<ActionResult<LeagueDetailDto>> Extend(Guid id, [FromBody] ExtendLeagueRequest request, CancellationToken ct)
        => await leagues.ExtendAsync(currentUser.UserId, id, request, ct);

    [HttpPost("{id:guid}/participants")]
    public async Task<ActionResult<LeagueDetailDto>> AddParticipants(
        Guid id, [FromBody] AddLeagueParticipantsRequest request, CancellationToken ct)
        => await leagues.AddParticipantsAsync(currentUser.UserId, id, request, ct);

    [HttpPut("{id:guid}/participants/{userId:guid}/team")]
    public async Task<ActionResult<LeagueDetailDto>> MoveParticipant(
        Guid id, Guid userId, [FromBody] MoveLeagueParticipantRequest request, CancellationToken ct)
        => await leagues.MoveParticipantAsync(currentUser.UserId, id, userId, request, ct);

    [HttpPut("{id:guid}/teams/{teamId:guid}")]
    public async Task<ActionResult<LeagueDetailDto>> RenameTeam(
        Guid id, Guid teamId, [FromBody] RenameLeagueTeamRequest request, CancellationToken ct)
        => await leagues.RenameTeamAsync(currentUser.UserId, id, teamId, request, ct);

    [HttpPut("{id:guid}/teams/{teamId:guid}/leader")]
    public async Task<ActionResult<LeagueDetailDto>> SetLeader(
        Guid id, Guid teamId, [FromBody] SetLeagueTeamLeaderRequest request, CancellationToken ct)
        => await leagues.SetTeamLeaderAsync(currentUser.UserId, id, teamId, request, ct);

    [HttpPost("{id:guid}/teams/{teamId:guid}/logo")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<LeagueDetailDto>> UploadLogo(Guid id, Guid teamId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { detail = "Choose an image file." });

        var contentType = string.IsNullOrWhiteSpace(file.ContentType) ? "image/png" : file.ContentType;
        await using var stream = file.OpenReadStream();
        return await leagues.UploadTeamLogoAsync(
            currentUser.UserId, id, teamId, stream, file.FileName, contentType, avatarStorage, ct);
    }
}
