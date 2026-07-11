namespace CityLeague.Core.Dtos;

public record EventFormatDto(
    int Id,
    string Key,
    string Name,
    int PlayersPerSide);

public record SportDto(
    int Id,
    string Key,
    string Name,
    string Availability,
    IReadOnlyList<EventFormatDto> Formats);
