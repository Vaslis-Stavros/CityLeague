namespace CityLeague.Core.Dtos;

public record LeagueDto(
    Guid Id,
    string Name,
    string SportKey,
    string SportName,
    string Status,
    bool IsOwner,
    int ParticipantCount,
    int TeamCount,
    int CompletedMatchCount,
    bool CanDelete,
    bool CanEnd);

public record CreateLeagueRequest(string Name, int SportId);
