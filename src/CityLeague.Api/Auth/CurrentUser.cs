using CityLeague.Infrastructure.Auth;
using Microsoft.AspNetCore.Http;

namespace CityLeague.Api.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid UserId { get; }
    bool TryGetUserId(out Guid userId);
}

public class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor = accessor;

    public bool IsAuthenticated => _accessor.HttpContext?.User?.Identity?.IsAuthenticated == true;

    public Guid UserId => TryGetUserId(out var id)
        ? id
        : throw new InvalidOperationException("No authenticated user in the current context.");

    public bool TryGetUserId(out Guid userId)
    {
        userId = Guid.Empty;
        var value = _accessor.HttpContext?.User?.FindFirst(AppClaims.UserId)?.Value;
        return value is not null && Guid.TryParse(value, out userId);
    }
}
