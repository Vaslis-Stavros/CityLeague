using System.Net.Mail;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Entities;
using CityLeague.Core.Validation;
using CityLeague.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CityLeague.Api.Services;

public class LocalAuthService(CityLeagueDbContext db, IPasswordHasher passwords)
{
    public async Task<(User? User, string? Error, int StatusCode)> RegisterAsync(
        string username, string password, string email, CancellationToken ct = default)
    {
        var handle = HandleValidator.Normalize(username);
        if (!HandleValidator.IsValid(handle, out var reason))
            return (null, reason, StatusCodes.Status400BadRequest);

        email = NormalizeEmail(email);
        if (!IsValidEmail(email))
            return (null, "Enter a valid email address.", StatusCodes.Status400BadRequest);

        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            return (null, "Password must be at least 6 characters.", StatusCodes.Status400BadRequest);

        if (await db.Users.AnyAsync(u => u.UniqueHandle == handle, ct))
            return (null, "That username is already taken.", StatusCodes.Status409Conflict);

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            return (null, "An account with this email already exists.", StatusCodes.Status409Conflict);

        var user = new User
        {
            B2CObjectId = $"local:{handle}",
            Email = email,
            PasswordHash = passwords.HashPassword(password),
            DisplayName = handle,
            UniqueHandle = handle,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return (user, null, StatusCodes.Status200OK);
    }

    public async Task<(User? User, string? Error, int StatusCode)> LoginAsync(
        string username, string password, CancellationToken ct = default)
    {
        var handle = HandleValidator.Normalize(username);
        if (string.IsNullOrEmpty(handle) || string.IsNullOrWhiteSpace(password))
            return (null, "Enter your username and password.", StatusCodes.Status400BadRequest);

        var user = await db.Users.FirstOrDefaultAsync(u => u.UniqueHandle == handle, ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return (null, "Invalid username or password.", StatusCodes.Status401Unauthorized);

        if (!passwords.VerifyPassword(password, user.PasswordHash))
            return (null, "Invalid username or password.", StatusCodes.Status401Unauthorized);

        return (user, null, StatusCodes.Status200OK);
    }

    public async Task<(User? User, string? Error, int StatusCode)> ChangePasswordAsync(
        Guid userId, string? currentPassword, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            return (null, "Password must be at least 6 characters.", StatusCodes.Status400BadRequest);

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return (null, "User not found.", StatusCodes.Status404NotFound);

        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(currentPassword)
                || !passwords.VerifyPassword(currentPassword, user.PasswordHash))
                return (null, "Current password is incorrect.", StatusCodes.Status400BadRequest);
        }

        user.PasswordHash = passwords.HashPassword(newPassword);
        await db.SaveChangesAsync(ct);
        return (user, null, StatusCodes.Status200OK);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static bool IsValidEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return false;
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@');
        }
        catch
        {
            return false;
        }
    }
}
