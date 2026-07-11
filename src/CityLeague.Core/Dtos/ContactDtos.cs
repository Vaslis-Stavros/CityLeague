namespace CityLeague.Core.Dtos;

public record ContactDto(
    Guid Id,
    UserDto User,
    string Status,
    bool IsIncomingRequest);

public record CreateContactRequest(Guid? UserId, string? Handle);
