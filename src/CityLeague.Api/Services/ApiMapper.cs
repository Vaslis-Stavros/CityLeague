using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Core.Enums;

namespace CityLeague.Api.Services;

/// <summary>Maps entities to DTOs, resolving avatar blob paths to public URLs.</summary>
public class ApiMapper(IAvatarStorage avatarStorage, IHttpContextAccessor httpContextAccessor)
{
    private readonly IAvatarStorage _avatarStorage = avatarStorage;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public UserDto ToUserDto(User user) => new(
        user.Id,
        user.UniqueHandle,
        user.DisplayName,
        user.Email,
        ToPublicAvatarUrl(user.AvatarBlobUrl),
        HasPassword: !string.IsNullOrEmpty(user.PasswordHash));

    public SportDto ToSportDto(Sport sport) => new(
        sport.Id,
        sport.Key,
        sport.Name,
        sport.Availability.ToString(),
        sport.Formats
            .OrderBy(f => f.PlayersPerSide)
            .Select(f => new EventFormatDto(f.Id, f.Key, f.Name, f.PlayersPerSide))
            .ToList());

    public PositionDto ToPositionDto(EventPosition p) => new(
        p.SlotId,
        p.Label,
        p.Side.ToString(),
        p.X,
        p.Y,
        p.UserId,
        p.User?.UniqueHandle,
        p.User?.DisplayName,
        ToPublicAvatarUrl(p.User?.AvatarBlobUrl));

    public ParticipantDto ToParticipantDto(EventParticipant p, Guid ownerUserId) => new(
        p.UserId,
        p.User?.UniqueHandle ?? string.Empty,
        p.User?.DisplayName ?? string.Empty,
        ToPublicAvatarUrl(p.User?.AvatarBlobUrl),
        p.CanInvite,
        p.UserId == ownerUserId);

    public static ResultDto? ToResultDto(EventResult? result) => result is null
        ? null
        : new ResultDto(result.HomeScore, result.AwayScore, result.WinningSide.ToString(), result.SubmittedAt);

    public PositionChangedDto ToPositionChanged(Guid eventId, EventPosition p) => new(
        eventId,
        p.SlotId,
        p.UserId,
        p.User?.UniqueHandle,
        p.User?.DisplayName,
        ToPublicAvatarUrl(p.User?.AvatarBlobUrl));

    /// <summary>
    /// Turns a stored blob path into an absolute URL the mobile client can fetch. Relative
    /// local-dev paths are absolutized against the host the client actually called (so an
    /// Android emulator talking to 10.0.2.2 does not get an unreachable localhost URL).
    /// </summary>
    public string? ToPublicAvatarUrl(string? blobPath)
    {
        var url = _avatarStorage.ResolvePublicUrl(blobPath);
        if (string.IsNullOrWhiteSpace(url))
            return null;

        if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return url;

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request is null || !request.Host.HasValue)
            return url;

        return $"{request.Scheme}://{request.Host.Value}{url}";
    }
}
