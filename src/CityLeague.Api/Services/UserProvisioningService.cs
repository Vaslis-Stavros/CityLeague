using CityLeague.Api.Auth;
using CityLeague.Core.Entities;
using CityLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Services;

/// <summary>Creates or updates the local user record for a verified external identity.</summary>
public class UserProvisioningService(CityLeagueDbContext db)
{
    private readonly CityLeagueDbContext _db = db;

    public async Task<User> GetOrCreateAsync(ExternalIdentity identity, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.B2CObjectId == identity.Subject, ct);
        if (user is not null)
        {
            var changed = false;
            if (!string.IsNullOrWhiteSpace(identity.Email) && user.Email != identity.Email)
            {
                user.Email = identity.Email;
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(user.DisplayName) && !string.IsNullOrWhiteSpace(identity.DisplayName))
            {
                user.DisplayName = identity.DisplayName!;
                changed = true;
            }
            if (changed) await _db.SaveChangesAsync(ct);
            return user;
        }

        user = new User
        {
            B2CObjectId = identity.Subject,
            Email = identity.Email,
            DisplayName = string.IsNullOrWhiteSpace(identity.DisplayName) ? "Player" : identity.DisplayName!,
            UniqueHandle = null,
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return user;
    }
}
