using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Core.Enums;

namespace CityLeague.Api.Services;

/// <summary>Maps entities to DTOs, resolving avatar blob paths to public URLs.</summary>
public class ApiMapper(IAvatarStorage avatarStorage)
{
    private readonly IAvatarStorage _avatarStorage = avatarStorage;

    public UserDto ToUserDto(User user) => new(
        user.Id,
        user.UniqueHandle,
        user.DisplayName,
        user.Email,
        _avatarStorage.ResolvePublicUrl(user.AvatarBlobUrl));

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
        _avatarStorage.ResolvePublicUrl(p.User?.AvatarBlobUrl));

    public ParticipantDto ToParticipantDto(EventParticipant p, Guid ownerUserId) => new(
        p.UserId,
        p.User?.UniqueHandle ?? string.Empty,
        p.User?.DisplayName ?? string.Empty,
        _avatarStorage.ResolvePublicUrl(p.User?.AvatarBlobUrl),
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
        _avatarStorage.ResolvePublicUrl(p.User?.AvatarBlobUrl));
}
