using CityLeague.Api.Auth;
using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using CityLeague.Core.Enums;
using CityLeague.Core.Validation;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(
    CityLeagueDbContext db,
    ICurrentUser currentUser,
    ApiMapper mapper) : ControllerBase
{
    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<UserSearchResultDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var query = HandleValidator.Normalize(q ?? string.Empty);
        if (query.Length < 2)
            return Ok(Array.Empty<UserSearchResultDto>());

        var me = currentUser.UserId;
        var matches = await db.Users
            .Where(u => u.Id != me && u.UniqueHandle != null && u.UniqueHandle.StartsWith(query))
            .OrderBy(u => u.UniqueHandle)
            .Take(20)
            .ToListAsync(ct);

        var matchIds = matches.Select(u => u.Id).ToList();
        var contacts = await db.Contacts
            .Where(c => (c.OwnerUserId == me && matchIds.Contains(c.ContactUserId))
                        || (c.ContactUserId == me && matchIds.Contains(c.OwnerUserId)))
            .ToListAsync(ct);

        var results = matches.Select(u =>
        {
            var relationship = ResolveRelationship(me, u.Id, contacts);
            return new UserSearchResultDto(
                u.Id,
                u.UniqueHandle!,
                u.DisplayName,
                mapper.ToPublicAvatarUrl(u.AvatarBlobUrl),
                relationship);
        }).ToList();

        return Ok(results);
    }

    private static string ResolveRelationship(Guid me, Guid other, List<Core.Entities.Contact> contacts)
    {
        var outgoing = contacts.FirstOrDefault(c => c.OwnerUserId == me && c.ContactUserId == other);
        var incoming = contacts.FirstOrDefault(c => c.OwnerUserId == other && c.ContactUserId == me);

        if (outgoing?.Status == ContactStatus.Accepted || incoming?.Status == ContactStatus.Accepted)
            return "accepted";
        if (outgoing?.Status == ContactStatus.Pending)
            return "pending_outgoing";
        if (incoming?.Status == ContactStatus.Pending)
            return "pending_incoming";
        return "none";
    }
}
