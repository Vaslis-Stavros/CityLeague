namespace CityLeague.Core.Dtos;

public record LeagueDto(
    Guid Id,
    string Name,
    string SportKey,
    string SportName,
    string Status,
    bool IsOwner,
    bool IsTeamLeader,
    int ParticipantCount,
    int TeamCount,
    int CompletedMatchCount,
    int PlannedMatchCount,
    double ProgressFraction,
    string? Team1Name,
    string? Team2Name,
    bool HasStarted,
    bool CanDelete,
    bool CanEnd,
    bool CanExtend,
    bool CanStart);

public record LeagueTeamDto(
    Guid Id,
    string Name,
    int SortOrder,
    string? LogoUrl,
    Guid? LeaderUserId,
    string? LeaderHandle,
    string? LeaderDisplayName,
    int Played,
    int Wins,
    int Losses,
    int Draws,
    int MemberCount);

public record LeagueParticipantDto(
    Guid UserId,
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    Guid? LeagueTeamId,
    string? TeamName,
    bool IsLeader,
    bool CanChangeTeam);

public record LeagueMatchResultDto(
    Guid EventId,
    string Title,
    DateTimeOffset ScheduledAt,
    int HomeScore,
    int AwayScore,
    string WinningSide,
    string? HomeTeamName,
    string? AwayTeamName,
    DateTimeOffset SubmittedAt);

public record LeagueDetailDto(
    Guid Id,
    string Name,
    string SportKey,
    string SportName,
    int SportId,
    string Status,
    bool IsOwner,
    bool IsTeamLeader,
    int CompletedMatchCount,
    int PlannedMatchCount,
    double ProgressFraction,
    bool HasStarted,
    bool CanDelete,
    bool CanEnd,
    bool CanExtend,
    bool CanStart,
    bool CanAddParticipants,
    bool CanUploadLogo,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    IReadOnlyList<LeagueTeamDto> Teams,
    IReadOnlyList<LeagueParticipantDto> Participants,
    IReadOnlyList<LeagueMatchResultDto> MatchResults);

public record CreateLeagueRequest(
    string Name,
    int SportId,
    string Team1Name,
    string Team2Name,
    int PlannedMatchCount,
    Guid? Team1LeaderUserId = null,
    Guid? Team2LeaderUserId = null,
    IReadOnlyList<Guid>? ParticipantUserIds = null);

public record AddLeagueParticipantsRequest(IReadOnlyList<Guid> UserIds);

public record MoveLeagueParticipantRequest(Guid? LeagueTeamId);

public record SetLeagueTeamLeaderRequest(Guid UserId);

public record RenameLeagueTeamRequest(string Name);

public record ExtendLeagueRequest(int AdditionalMatches);
