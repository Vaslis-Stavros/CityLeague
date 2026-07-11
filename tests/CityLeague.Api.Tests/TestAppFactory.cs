using System.Net.Http.Headers;
using System.Net.Http.Json;
using CityLeague.Core.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CityLeague.Api.Tests;

/// <summary>Boots the API against an isolated temp SQLite database in Dev auth mode.</summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"cmi-test-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = "",
                ["ConnectionStrings:Sqlite"] = $"Data Source={_dbPath}",
                ["Auth:Mode"] = "Dev",
                ["AvatarStorage:Provider"] = "Local",
            });
        });
    }

    /// <summary>Registers (or restores) a user and returns an authenticated client.</summary>
    public async Task<TestUser> CreateUserAsync(string email, string displayName, string? handle = null)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(null, "google", null, email, displayName));
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        if (handle is not null)
        {
            var handleResponse = await client.PostAsJsonAsync("/api/me/handle", new SetHandleRequest(handle));
            handleResponse.EnsureSuccessStatusCode();
        }

        return new TestUser(client, auth.User.Id);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try { File.Delete(_dbPath); } catch { /* best effort cleanup */ }
        }
    }
}

public record TestUser(HttpClient Client, Guid UserId);
