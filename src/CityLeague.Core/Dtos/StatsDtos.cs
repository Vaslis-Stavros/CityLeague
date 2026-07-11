namespace CityLeague.Core.Dtos;

public record PlayerStatsDto(
    int SportId,
    string SportKey,
    string SportName,
    int Played,
    int Wins,
    int Losses,
    int Draws);

public record MyStatsDto(
    UserDto User,
    IReadOnlyList<PlayerStatsDto> Stats);
