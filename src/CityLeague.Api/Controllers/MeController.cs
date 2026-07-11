using CityLeague.Api.Auth;
using CityLeague.Api.Common;
using CityLeague.Api.Services;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using CityLeague.Core.Validation;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/me")]
[Authorize]
public class MeController(
    CityLeagueDbContext db,
    ICurrentUser currentUser,
    IAvatarStorage avatarStorage,
    ApiMapper mapper) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<UserDto>> Get(CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw ServiceException.NotFound("User not found.");
        return mapper.ToUserDto(user);
    }

    [HttpPatch]
    public async Task<ActionResult<UserDto>> Update([FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw ServiceException.NotFound("User not found.");

        if (!string.IsNullOrWhiteSpace(request.DisplayName))
            user.DisplayName = request.DisplayName.Trim();
        if (request.AvatarBlobPath is not null)
            user.AvatarBlobUrl = string.IsNullOrWhiteSpace(request.AvatarBlobPath) ? null : request.AvatarBlobPath;

        await db.SaveChangesAsync(ct);
        return mapper.ToUserDto(user);
    }

    [HttpGet("handle/available")]
    public async Task<ActionResult<HandleAvailabilityDto>> CheckHandle([FromQuery] string handle, CancellationToken ct)
    {
        var normalized = HandleValidator.Normalize(handle);
        if (!HandleValidator.IsValid(normalized, out var reason))
            return new HandleAvailabilityDto(normalized, false, reason);

        var taken = await db.Users.AnyAsync(u => u.UniqueHandle == normalized && u.Id != currentUser.UserId, ct);
        return new HandleAvailabilityDto(normalized, !taken, taken ? "That handle is taken." : null);
    }

    [HttpPost("handle")]
    public async Task<ActionResult<UserDto>> SetHandle([FromBody] SetHandleRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw ServiceException.NotFound("User not found.");

        if (!string.IsNullOrEmpty(user.UniqueHandle))
            throw ServiceException.Conflict("Your handle has already been set.");

        var normalized = HandleValidator.Normalize(request.Handle);
        if (!HandleValidator.IsValid(normalized, out var reason))
            throw ServiceException.BadRequest(reason ?? "Invalid handle.");

        if (await db.Users.AnyAsync(u => u.UniqueHandle == normalized, ct))
            throw ServiceException.Conflict("That handle is taken.");

        user.UniqueHandle = normalized;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            throw ServiceException.Conflict("That handle is taken.");
        }

        return mapper.ToUserDto(user);
    }

    [HttpPost("avatar")]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public async Task<ActionResult<UserDto>> UploadAvatar(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw ServiceException.BadRequest("No file uploaded.");

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct)
            ?? throw ServiceException.NotFound("User not found.");

        var ext = Path.GetExtension(file.FileName);
        var blobPath = avatarStorage.BuildAvatarBlobPath(user.Id, ext);
        await using (var stream = file.OpenReadStream())
            await avatarStorage.SaveAsync(blobPath, stream, file.ContentType ?? "image/png", ct);

        user.AvatarBlobUrl = blobPath;
        await db.SaveChangesAsync(ct);
        return mapper.ToUserDto(user);
    }

    [HttpGet("avatar-ticket")]
    public async Task<ActionResult<AvatarUploadTicket>> GetAvatarTicket([FromQuery] string contentType, CancellationToken ct)
    {
        var ext = contentType?.ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" or "image/jpg" => ".jpg",
            "image/webp" => ".webp",
            _ => ".png",
        };
        var blobPath = avatarStorage.BuildAvatarBlobPath(currentUser.UserId, ext);
        try
        {
            return await avatarStorage.CreateUploadTicketAsync(blobPath, contentType ?? "image/png", ct);
        }
        catch (NotSupportedException)
        {
            throw ServiceException.BadRequest("Direct upload tickets are not available; POST the image to /api/me/avatar instead.");
        }
    }
}
