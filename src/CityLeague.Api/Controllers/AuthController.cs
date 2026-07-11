using CityLeague.Api.Auth;
using CityLeague.Api.Services;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Infrastructure.Auth;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(
    IExternalIdentityValidator validator,
    UserProvisioningService provisioning,
    LocalAuthService localAuth,
    IJwtTokenService tokens,
    CityLeagueDbContext db,
    ApiMapper mapper) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] LocalRegisterRequest request, CancellationToken ct)
    {
        var (user, error, status) = await localAuth.RegisterAsync(request.Username, request.Password, request.Email, ct);
        if (user is null)
            return StatusCode(status, new { detail = error });

        return Issue(user);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LocalLoginRequest request, CancellationToken ct)
    {
        var (user, error, status) = await localAuth.LoginAsync(request.Username, request.Password, ct);
        if (user is null)
            return StatusCode(status, new { detail = error });

        return Issue(user);
    }

    [HttpPost("exchange")]
    public async Task<ActionResult<AuthResponse>> Exchange([FromBody] AuthExchangeRequest request, CancellationToken ct)
    {
        var identity = await validator.ValidateAsync(request, ct);
        if (identity is null)
            return Unauthorized(new { detail = "Could not validate the sign-in token." });

        var user = await provisioning.GetOrCreateAsync(identity, ct);
        return Issue(user);
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var principal = tokens.ValidateRefreshToken(request.RefreshToken);
        var uid = principal?.FindFirst(AppClaims.UserId)?.Value;
        if (uid is null || !Guid.TryParse(uid, out var userId))
            return Unauthorized(new { detail = "Invalid refresh token." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return Unauthorized(new { detail = "User no longer exists." });

        return Issue(user);
    }

    private AuthResponse Issue(User user)
    {
        var pair = tokens.Issue(user);
        return new AuthResponse(
            pair.AccessToken,
            pair.RefreshToken,
            pair.ExpiresAt,
            mapper.ToUserDto(user),
            string.IsNullOrEmpty(user.UniqueHandle));
    }
}
