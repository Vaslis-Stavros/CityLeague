using System.Net;
using System.Net.Http.Json;
using CityLeague.Core.Dtos;

namespace CityLeague.Api.Tests;

public class LocalAuthTests
{
    [Fact]
    public async Task Register_login_and_reject_bad_password()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();
        var username = $"user_{Guid.NewGuid():N}"[..16];

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new LocalRegisterRequest(username, "secret12", $"{username}@CityLeague.test"));
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);

        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal(username, auth.User.Handle);
        Assert.False(auth.NeedsHandle);

        var login = await client.PostAsJsonAsync("/api/auth/login",
            new LocalLoginRequest(username, "secret12"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var bad = await client.PostAsJsonAsync("/api/auth/login",
            new LocalLoginRequest(username, "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        var duplicate = await client.PostAsJsonAsync("/api/auth/register",
            new LocalRegisterRequest(username, "secret12", $"{username}@CityLeague.test"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Social_exchange_works_without_email_in_dev_mode()
    {
        await using var factory = new TestAppFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(null, "google", null, null, null));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
