using System.Net;
using System.Net.Http.Json;
using CityLeague.Api.Auth;
using CityLeague.Core.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace CityLeague.Api.Tests;

public class SocialAuthTests
{
    private const string GoogleClientId = "google-client-id.apps.googleusercontent.com";
    private const string GoogleIssuer = "https://accounts.google.com";
    private const string MicrosoftClientId = "microsoft-client-id";
    private const string MicrosoftAuthority = "https://login.microsoftonline.com/common/v2.0";
    private const string AppleServiceId = "com.cityleague.service";
    private const string AppleBundleId = "com.CityLeague.app";

    [Fact]
    public async Task Providers_endpoint_describes_what_is_configured()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var response = await factory.CreateClient().GetFromJsonAsync<AuthProvidersResponse>("/api/auth/providers");

        Assert.NotNull(response);
        Assert.True(response.DevSignInEnabled);
        Assert.Equal(["google", "microsoft", "apple"], response.Providers.Select(p => p.Provider));

        var google = response.Providers.Single(p => p.Provider == "google");
        Assert.Equal(GoogleClientId, google.ClientId);
        Assert.Equal($"{GoogleIssuer}/authorize", google.AuthorizeUrl);
        Assert.True(google.UsePkce);
        // Google is a web client here, so the API bridges the callback to the app's scheme.
        Assert.Equal("https://api.cityleague.test/api/auth/callback/google", google.RedirectUri);
        Assert.Equal("cityleague://auth/callback", google.CallbackUrl);

        var microsoft = response.Providers.Single(p => p.Provider == "microsoft");
        Assert.Equal("cityleague://auth/callback", microsoft.RedirectUri);

        var apple = response.Providers.Single(p => p.Provider == "apple");
        Assert.Equal("form_post", apple.ResponseMode);
        Assert.False(apple.UsePkce);
        Assert.True(apple.SupportsNativeIos);
    }

    [Fact]
    public async Task Google_id_token_provisions_a_user()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var idToken = provider.CreateIdToken(
            GoogleIssuer, GoogleClientId, "google-subject-1", email: "alex@example.com", name: "Alex K");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(idToken, "google"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        Assert.Equal("Alex K", auth.User.DisplayName);
        Assert.True(auth.NeedsHandle);
    }

    [Fact]
    public async Task Signing_in_twice_returns_the_same_account()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);
        var client = factory.CreateClient();

        async Task<Guid> SignInAsync()
        {
            var token = provider.CreateIdToken(
                GoogleIssuer, GoogleClientId, "google-subject-2", email: "sam@example.com", name: "Sam");
            var response = await client.PostAsJsonAsync("/api/auth/exchange", new AuthExchangeRequest(token, "google"));
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<AuthResponse>())!.User.Id;
        }

        Assert.Equal(await SignInAsync(), await SignInAsync());
    }

    [Fact]
    public async Task A_second_provider_links_to_the_account_with_the_same_verified_email()
    {
        using var provider = new FakeOpenIdProvider();
        provider.Issuers[MicrosoftAuthority] = "https://login.microsoftonline.com/{tenantid}/v2.0";
        await using var factory = CreateFactory(provider);
        var client = factory.CreateClient();

        var googleToken = provider.CreateIdToken(
            GoogleIssuer, GoogleClientId, "google-subject-3", email: "jo@example.com", name: "Jo");
        var google = await client.PostAsJsonAsync("/api/auth/exchange", new AuthExchangeRequest(googleToken, "google"));
        google.EnsureSuccessStatusCode();
        var googleUserId = (await google.Content.ReadFromJsonAsync<AuthResponse>())!.User.Id;

        // A consumer Microsoft account: the tenant proves the email, and the issuer is resolved
        // from the multi-tenant "{tenantid}" template.
        var microsoftToken = provider.CreateIdToken(
            "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0",
            MicrosoftClientId,
            "microsoft-subject-3",
            email: "jo@example.com",
            extraClaims: new Dictionary<string, object> { ["tid"] = "9188040d-6c67-4c5b-b112-36a304b66dad" });

        var microsoft = await client.PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(microsoftToken, "microsoft"));
        microsoft.EnsureSuccessStatusCode();
        var microsoftUserId = (await microsoft.Content.ReadFromJsonAsync<AuthResponse>())!.User.Id;

        Assert.Equal(googleUserId, microsoftUserId);
    }

    [Fact]
    public async Task An_unverified_email_does_not_take_over_an_existing_account()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);
        var client = factory.CreateClient();

        var register = await client.PostAsJsonAsync("/api/auth/register",
            new LocalRegisterRequest("victim_user", "secret12", "victim@example.com"));
        register.EnsureSuccessStatusCode();

        var idToken = provider.CreateIdToken(
            GoogleIssuer, GoogleClientId, "attacker-subject", email: "victim@example.com", emailVerified: false);

        var response = await client.PostAsJsonAsync("/api/auth/exchange", new AuthExchangeRequest(idToken, "google"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("wrong-audience", null, null)]
    [InlineData(null, "https://evil.test", null)]
    [InlineData(null, null, "expired")]
    public async Task Invalid_id_tokens_are_rejected(string? audience, string? issuer, string? expired)
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var idToken = provider.CreateIdToken(
            issuer ?? GoogleIssuer,
            audience ?? GoogleClientId,
            "google-subject-4",
            email: "nope@example.com",
            expires: expired is null ? null : DateTime.UtcNow.AddMinutes(-10));

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(idToken, "google"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_replayed_token_with_the_wrong_nonce_is_rejected()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var idToken = provider.CreateIdToken(
            GoogleIssuer, GoogleClientId, "google-subject-5", email: "nonce@example.com", nonce: "issued-nonce");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(idToken, "google", Nonce: "expected-nonce"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_authorization_code_is_redeemed_at_the_provider()
    {
        using var provider = new FakeOpenIdProvider();
        var idToken = provider.CreateIdToken(
            GoogleIssuer, GoogleClientId, "google-subject-6", email: "code@example.com", name: "Cody");

        StubTokenEndpointHandler? handler = null;
        await using var factory = CreateFactory(provider, services =>
        {
            handler = new StubTokenEndpointHandler(_ => (HttpStatusCode.OK, StubTokenEndpointHandler.TokenResponse(idToken)));
            services.AddHttpClient(SocialIdentityValidator.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => handler);
        });

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(null, "google", Code: "auth-code", CodeVerifier: "verifier-123"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(handler!.LastRequest);
        Assert.Equal("authorization_code", handler.LastRequest["grant_type"]);
        Assert.Equal("auth-code", handler.LastRequest["code"]);
        Assert.Equal("verifier-123", handler.LastRequest["code_verifier"]);
        Assert.Equal("https://api.cityleague.test/api/auth/callback/google", handler.LastRequest["redirect_uri"]);
        Assert.Equal("google-secret", handler.LastRequest["client_secret"]);
    }

    [Fact]
    public async Task A_provider_error_is_surfaced_to_the_caller()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider, services =>
        {
            services.AddHttpClient(SocialIdentityValidator.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new StubTokenEndpointHandler(
                    _ => (HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Code was already redeemed."}""")));
        });

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(null, "google", Code: "used-code"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Code was already redeemed.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Native_apple_tokens_are_accepted_for_the_bundle_id()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var idToken = provider.CreateIdToken(
            "https://appleid.apple.com", AppleBundleId, "apple-subject-1", email: "apple@privaterelay.appleid.com");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(idToken, "apple", DisplayName: "Robin Doe"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        // Apple omits the name from the id_token, so the one-time value from the client is used.
        Assert.Equal("Robin Doe", auth!.User.DisplayName);
    }

    [Fact]
    public async Task The_callback_bridge_forwards_apples_form_post_to_the_app()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var response = await factory.CreateClient().PostAsync("/api/auth/callback/apple",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = "apple-code",
                ["state"] = "app-state",
            }));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("cityleague://auth/callback?code=apple-code&amp;state=app-state", body);
    }

    [Fact]
    public async Task The_dev_shim_is_refused_outside_dev_mode()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider, settings: new Dictionary<string, string?>
        {
            ["Auth:Mode"] = "Production",
        });

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(null, "google", Email: "spoofed@example.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_real_token_that_fails_validation_never_falls_back_to_the_dev_shim()
    {
        using var provider = new FakeOpenIdProvider();
        await using var factory = CreateFactory(provider);

        var idToken = provider.CreateIdToken(GoogleIssuer, "some-other-app", "google-subject-7");

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/exchange",
            new AuthExchangeRequest(idToken, "google", Email: "spoofed@example.com"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static TestAppFactory CreateFactory(
        FakeOpenIdProvider provider,
        Action<IServiceCollection>? configureServices = null,
        IDictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Auth:PublicBaseUrl"] = "https://api.cityleague.test",
            ["Auth:Google:ClientId"] = GoogleClientId,
            ["Auth:Google:ClientSecret"] = "google-secret",
            ["Auth:Microsoft:ClientId"] = MicrosoftClientId,
            ["Auth:Apple:ClientId"] = AppleServiceId,
            ["Auth:Apple:BundleId"] = AppleBundleId,
        };

        foreach (var (key, value) in settings ?? new Dictionary<string, string?>())
            values[key] = value;

        return new TestAppFactory
        {
            Settings = values,
            ConfigureTestServices = services =>
            {
                services.AddSingleton<IOpenIdMetadataProvider>(provider);
                configureServices?.Invoke(services);
            },
        };
    }
}
