namespace CityLeague.Core.Dtos;

public record CreateSeriesRequest(string Name, int SportId);

public record SeriesDto(Guid Id, string Name, int SportId);

public record CreateEventRequest(
    int EventFormatId,
    string Title,
    DateTimeOffset ScheduledAt,
    string? Location,
    Guid? SeriesId,
    IReadOnlyList<Guid>? InviteUserIds);

public record EventSummaryDto(
    Guid Id,
    string Title,
    string SportKey,
    string FormatName,
    DateTimeOffset ScheduledAt,
    string? Location,
    string Status,
    int ClaimedCount,
    int TotalSlots,
    bool IsOwner,
    ResultDto? Result = null);

public record PositionDto(
    string SlotId,
    string Label,
    string Side,
    double X,
    double Y,
    Guid? UserId,
    string? UserHandle,
    string? UserDisplayName,
    string? UserAvatarUrl);

public record ParticipantDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    bool CanInvite,
    bool IsOwner);

public record ResultDto(
    int HomeScore,
    int AwayScore,
    string WinningSide,
    DateTimeOffset SubmittedAt);

public record EventDetailDto(
    Guid Id,
    string Title,
    string SportKey,
    int SportId,
    string FormatKey,
    string FormatName,
    int PlayersPerSide,
    DateTimeOffset ScheduledAt,
    string? Location,
    string Status,
    bool IsOwner,
    bool CanInvite,
    Guid OwnerUserId,
    IReadOnlyList<PositionDto> Positions,
    IReadOnlyList<ParticipantDto> Participants,
    ResultDto? Result);

public record InviteRequest(IReadOnlyList<Guid> UserIds);

public record SubmitResultRequest(int HomeScore, int AwayScore);

/// <summary>Broadcast over SignalR when a position is claimed or released.</summary>
public record PositionChangedDto(
    Guid EventId,
    string SlotId,
    Guid? UserId,
    string? UserHandle,
    string? UserDisplayName,
    string? UserAvatarUrl);
