using CityLeague.Api.Auth;
using CityLeague.Api.Common;
using CityLeague.Api.Services;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Core.Enums;
using CityLeague.Core.Validation;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/contacts")]
[Authorize]
public class ContactsController(
    CityLeagueDbContext db,
    ICurrentUser currentUser,
    ApiMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ContactDto>>> List(CancellationToken ct)
    {
        var me = currentUser.UserId;

        var mine = await db.Contacts
            .Include(c => c.ContactUser)
            .Where(c => c.OwnerUserId == me)
            .ToListAsync(ct);

        var incoming = await db.Contacts
            .Include(c => c.OwnerUser)
            .Where(c => c.ContactUserId == me && c.Status == ContactStatus.Pending)
            .ToListAsync(ct);

        var result = new List<ContactDto>();
        result.AddRange(mine.Select(c => new ContactDto(c.Id, mapper.ToUserDto(c.ContactUser!), c.Status.ToString(), false)));
        result.AddRange(incoming.Select(c => new ContactDto(c.Id, mapper.ToUserDto(c.OwnerUser!), c.Status.ToString(), true)));

        return result
            .OrderByDescending(c => c.IsIncomingRequest)
            .ThenBy(c => c.User.DisplayName)
            .ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ContactDto>> Create([FromBody] CreateContactRequest request, CancellationToken ct)
    {
        var me = currentUser.UserId;

        var target = await ResolveTargetAsync(request, ct);
        if (target is null)
            throw ServiceException.NotFound("User not found.");
        if (target.Id == me)
            throw ServiceException.BadRequest("You cannot add yourself.");

        var outgoing = await db.Contacts.FirstOrDefaultAsync(c => c.OwnerUserId == me && c.ContactUserId == target.Id, ct);
        if (outgoing is { Status: ContactStatus.Accepted })
            return new ContactDto(outgoing.Id, mapper.ToUserDto(target), outgoing.Status.ToString(), false);
        if (outgoing is { Status: ContactStatus.Pending })
            return new ContactDto(outgoing.Id, mapper.ToUserDto(target), outgoing.Status.ToString(), false);

        // If the other user already requested me, accept immediately.
        var incoming = await db.Contacts.FirstOrDefaultAsync(c => c.OwnerUserId == target.Id && c.ContactUserId == me, ct);
        if (incoming is { Status: ContactStatus.Pending })
        {
            incoming.Status = ContactStatus.Accepted;
            var reciprocal = new Contact { OwnerUserId = me, ContactUserId = target.Id, Status = ContactStatus.Accepted };
            db.Contacts.Add(reciprocal);
            await db.SaveChangesAsync(ct);
            return new ContactDto(reciprocal.Id, mapper.ToUserDto(target), reciprocal.Status.ToString(), false);
        }

        var pending = new Contact { OwnerUserId = me, ContactUserId = target.Id, Status = ContactStatus.Pending };
        db.Contacts.Add(pending);
        await db.SaveChangesAsync(ct);
        return new ContactDto(pending.Id, mapper.ToUserDto(target), pending.Status.ToString(), false);
    }

    [HttpPost("{userId:guid}/accept")]
    public async Task<ActionResult<ContactDto>> Accept(Guid userId, CancellationToken ct)
    {
        var me = currentUser.UserId;

        var incoming = await db.Contacts
            .Include(c => c.OwnerUser)
            .FirstOrDefaultAsync(c => c.OwnerUserId == userId && c.ContactUserId == me && c.Status == ContactStatus.Pending, ct)
            ?? throw ServiceException.NotFound("No pending request from this user.");

        incoming.Status = ContactStatus.Accepted;

        var reciprocal = await db.Contacts.FirstOrDefaultAsync(c => c.OwnerUserId == me && c.ContactUserId == userId, ct);
        if (reciprocal is null)
        {
            reciprocal = new Contact { OwnerUserId = me, ContactUserId = userId, Status = ContactStatus.Accepted };
            db.Contacts.Add(reciprocal);
        }
        else
        {
            reciprocal.Status = ContactStatus.Accepted;
        }

        await db.SaveChangesAsync(ct);
        return new ContactDto(reciprocal.Id, mapper.ToUserDto(incoming.OwnerUser!), reciprocal.Status.ToString(), false);
    }

    private async Task<User?> ResolveTargetAsync(CreateContactRequest request, CancellationToken ct)
    {
        if (request.UserId is Guid id)
            return await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

        if (!string.IsNullOrWhiteSpace(request.Handle))
        {
            var normalized = HandleValidator.Normalize(request.Handle);
            return await db.Users.FirstOrDefaultAsync(u => u.UniqueHandle == normalized, ct);
        }

        return null;
    }
}
