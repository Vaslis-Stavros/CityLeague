using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using CityLeague.Core.Dtos;

namespace CityLeague.Api.Tests;

public class AvatarTests
{
    [Fact]
    public async Task Upload_returns_an_absolute_url_the_client_can_fetch()
    {
        await using var factory = new TestAppFactory
        {
            Settings =
            {
                ["AvatarStorage:Provider"] = "Local",
                ["AvatarStorage:PublicBaseUrl"] = "",
            },
        };

        var user = await factory.CreateUserAsync("avatar@CityLeague.test", "Ava Tar", "avatar_user");

        using var content = new MultipartFormDataContent();
        // Minimal valid 1x1 PNG.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "avatar.png");

        var response = await user.Client.PostAsync("/api/me/avatar", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<UserDto>();
        Assert.NotNull(updated?.AvatarUrl);
        Assert.StartsWith("http://", updated.AvatarUrl, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/uploads/avatars/", updated.AvatarUrl, StringComparison.OrdinalIgnoreCase);

        // The file must actually be served from the resolved URL.
        var image = await factory.CreateClient().GetAsync(updated.AvatarUrl);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        var bytes = await image.Content.ReadAsByteArrayAsync();
        Assert.Equal(png, bytes);

        // Profile and /api/me keep returning the same absolute URL.
        var me = await user.Client.GetFromJsonAsync<UserDto>("/api/me");
        Assert.Equal(updated.AvatarUrl, me!.AvatarUrl);
    }

    [Fact]
    public async Task Non_image_uploads_are_rejected()
    {
        await using var factory = new TestAppFactory();
        var user = await factory.CreateUserAsync("badavatar@CityLeague.test", "Bad", "bad_avatar");

        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not-an-image"));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(file, "file", "notes.txt");

        var response = await user.Client.PostAsync("/api/me/avatar", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Explicit_PublicBaseUrl_wins_over_the_request_host()
    {
        await using var factory = new TestAppFactory
        {
            Settings =
            {
                ["AvatarStorage:Provider"] = "Local",
                ["AvatarStorage:PublicBaseUrl"] = "https://cdn.example.test",
            },
        };

        var user = await factory.CreateUserAsync("cdn@CityLeague.test", "Cdn User", "cdn_user");

        using var content = new MultipartFormDataContent();
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "avatar.png");

        var response = await user.Client.PostAsync("/api/me/avatar", content);
        response.EnsureSuccessStatusCode();
        var updated = await response.Content.ReadFromJsonAsync<UserDto>();

        Assert.StartsWith("https://cdn.example.test/uploads/avatars/", updated!.AvatarUrl);
    }
}
