using System.Net;
using System.Net.Http.Headers;

namespace CityLeague.App.Services;

/// <summary>Attaches the bearer token and transparently refreshes it once on a 401.</summary>
public class AuthMessageHandler(ITokenStore tokens, IAuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ApplyToken(request);
        var response = await base.SendAsync(request, ct);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        if (!await auth.TryRefreshAsync())
            return response;

        response.Dispose();
        var retry = await CloneAsync(request);
        ApplyToken(retry);
        return await base.SendAsync(retry, ct);
    }

    private void ApplyToken(HttpRequestMessage request)
    {
        if (!string.IsNullOrEmpty(tokens.AccessToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);
        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync();
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return clone;
    }
}
