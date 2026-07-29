using CityLeague.Api.Auth;
using CityLeague.Core.Entities;
using CityLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Services;

/// <summary>Creates, links or updates the local user record for a verified external identity.</summary>
public class UserProvisioningService(CityLeagueDbContext db, TimeProvider time)
{
    private readonly CityLeagueDbContext _db = db;

    /// <summary>
    /// Thrown when the identity's email already belongs to another account that the provider
    /// has not proven ownership of, so linking would be a takeover.
    /// </summary>
    public class EmailAlreadyInUseException(string message) : Exception(message);

    public async Task<User> GetOrCreateAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var provider = SocialProviderCatalog.Normalize(identity.Provider) ?? "external";
        var subject = identity.Subject;
        var email = Normalize(identity.Email);

        var login = await _db.UserExternalLogins
            .Include(l => l.User)
            .FirstOrDefaultAsync(l => l.Provider == provider && l.Subject == subject, ct);
        if (login?.User is not null)
        {
            login.LastLoginAt = time.GetUtcNow();
            login.Email = email ?? login.Email;
            ApplyProfile(login.User, identity, email);
            await _db.SaveChangesAsync(ct);
            return login.User;
        }

        // Accounts created before external logins were tracked separately.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.B2CObjectId == subject, ct);

        // A second provider for the same person: only safe when the provider proved the email.
        if (user is null && identity.EmailVerified && email is not null)
            user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

        if (user is null)
        {
            if (email is not null && await _db.Users.AnyAsync(u => u.Email == email, ct))
                throw new EmailAlreadyInUseException(
                    "An account already uses this email address. Sign in with your username and password instead.");

            user = new User
            {
                B2CObjectId = subject,
                Email = email,
                DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? "Player" : identity.DisplayName!.Trim(),
                UniqueHandle = null,
            };
            _db.Users.Add(user);
        }
        else
        {
            ApplyProfile(user, identity, email);
        }

        _db.UserExternalLogins.Add(new UserExternalLogin
        {
            User = user,
            Provider = provider,
            Subject = subject,
            Email = email,
            CreatedAt = time.GetUtcNow(),
            LastLoginAt = time.GetUtcNow(),
        });

        await _db.SaveChangesAsync(ct);
        return user;
    }

    private static void ApplyProfile(User user, ExternalIdentity identity, string? email)
    {
        if (email is not null && string.IsNullOrWhiteSpace(user.Email))
            user.Email = email;

        if (string.IsNullOrWhiteSpace(user.DisplayName) && !string.IsNullOrWhiteSpace(identity.DisplayName))
            user.DisplayName = identity.DisplayName!.Trim();
    }

    private static string? Normalize(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}
