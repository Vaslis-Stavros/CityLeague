using System.Text.Encodings.Web;
using System.Text.Json;
using CityLeague.Api.Auth;
using CityLeague.Api.Services;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using CityLeague.Core.Entities;
using CityLeague.Infrastructure.Auth;
using CityLeague.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(
    IExternalIdentityValidator validator,
    SocialProviderDirectory providerDirectory,
    SocialProviderCatalog providerCatalog,
    UserProvisioningService provisioning,
    LocalAuthService localAuth,
    IJwtTokenService tokens,
    CityLeagueDbContext db,
    ApiMapper mapper) : ControllerBase
{
    /// <summary>Sign-in options this deployment is configured for, used to drive the app's UI and flows.</summary>
    [HttpGet("providers")]
    public async Task<ActionResult<AuthProvidersResponse>> Providers(CancellationToken ct)
        => await providerDirectory.DescribeAsync(ct);

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
        ExternalIdentity? identity;
        try
        {
            identity = await validator.ValidateAsync(request, ct);
        }
        catch (SocialSignInException ex)
        {
            return Unauthorized(new { detail = ex.Message });
        }

        if (identity is null)
            return Unauthorized(new { detail = "Could not validate the sign-in token." });

        try
        {
            var user = await provisioning.GetOrCreateAsync(identity, ct);
            return Issue(user);
        }
        catch (UserProvisioningService.EmailAlreadyInUseException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Bridges providers that can only redirect to an https url (Apple, Google web clients) back
    /// to the app's custom scheme. Apple posts the result as a form; others use the query string.
    /// </summary>
    [HttpGet("callback/{provider}")]
    [HttpPost("callback/{provider}")]
    public IActionResult Callback(string provider)
    {
        if (!providerCatalog.TryGet(provider, out var descriptor))
            return NotFound(new { detail = $"Sign-in with '{provider}' is not configured." });

        var values = Request.HasFormContentType
            ? Request.Form.ToDictionary(f => f.Key, f => f.Value.ToString())
            : Request.Query.ToDictionary(q => q.Key, q => q.Value.ToString());

        var target = QueryHelpers.AddQueryString(descriptor.CallbackUrl,
            values.Where(v => !string.IsNullOrEmpty(v.Value))
                .ToDictionary(v => v.Key, v => (string?)v.Value));

        // GET (Google web client): 302 into the app scheme. POST (Apple form_post): Chrome often
        // refuses a bare 302 into a custom scheme after a form post, so serve a page that also
        // fires an Android intent:// deep link.
        if (HttpMethods.IsGet(Request.Method) && !Request.HasFormContentType)
            return Redirect(target);

        return Content(BuildCallbackBridgePage(target), "text/html; charset=utf-8");
    }

    private static string BuildCallbackBridgePage(string target)
    {
        var href = HtmlEncoder.Default.Encode(target);
        var script = JsonSerializer.Serialize(target);
        // intent://…#Intent;scheme=cityleague;package=…;end is what Custom Tabs will honor.
        var intentUrl = BuildAndroidIntentUrl(target);
        var intentScript = JsonSerializer.Serialize(intentUrl);
        return $$"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <meta http-equiv="refresh" content="0;url={{href}}" />
              <title>Signing you in…</title>
              <style>
                body { font-family: -apple-system, Segoe UI, Roboto, sans-serif; background: #0B6B2E;
                       color: #fff; display: grid; place-items: center; height: 100vh; margin: 0; }
                a { color: #fff; }
              </style>
            </head>
            <body>
              <p>Signing you in… <a href="{{href}}">Return to CityLeague</a></p>
              <script>
                (function () {
                  var target = {{script}};
                  var intent = {{intentScript}};
                  try { window.location.replace(target); } catch (e) {}
                  setTimeout(function () {
                    try { window.location.href = intent || target; } catch (e) {}
                  }, 250);
                })();
              </script>
            </body>
            </html>
            """;
    }

    private static string BuildAndroidIntentUrl(string target)
    {
        if (!Uri.TryCreate(target, UriKind.Absolute, out var uri))
            return target;

        var query = uri.Query.TrimStart('?');
        var path = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
            ? (uri.Host ?? "auth")
            : $"{uri.Host}{uri.AbsolutePath}";
        var suffix = string.IsNullOrEmpty(query) ? string.Empty : $"?{query}";
        return $"intent://{path}{suffix}#Intent;scheme={uri.Scheme};package=com.CityLeague.app;end";
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
