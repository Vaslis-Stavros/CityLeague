using System.Security.Cryptography;
using CityLeague.Api.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Api.Tests;

public class AppleClientSecretTests
{
    private const string ServiceId = "com.cityleague.service";
    private const string TeamId = "ABCDE12345";
    private const string KeyId = "KEY123456";

    [Fact]
    public async Task Creates_an_es256_secret_apple_can_verify()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var factory = CreateFactory(key.ExportPkcs8PrivateKeyPem());

        var secret = factory.Create(ServiceId);

        Assert.NotNull(secret);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(secret, new TokenValidationParameters
        {
            ValidIssuer = TeamId,
            ValidAudience = "https://appleid.apple.com",
            IssuerSigningKey = new ECDsaSecurityKey(key),
        });

        Assert.True(result.IsValid, result.Exception?.Message);
        var token = Assert.IsType<JsonWebToken>(result.SecurityToken);
        Assert.Equal(ServiceId, token.Subject);
        Assert.Equal(KeyId, token.Kid);
        Assert.Equal(SecurityAlgorithms.EcdsaSha256, token.Alg);
    }

    [Fact]
    public void Accepts_a_key_without_pem_headers()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var factory = CreateFactory(Convert.ToBase64String(key.ExportPkcs8PrivateKey()));

        Assert.NotNull(factory.Create(ServiceId));
    }

    [Fact]
    public void Returns_null_when_apple_is_not_configured()
    {
        var factory = new AppleClientSecretFactory(
            Options.Create(new AuthOptions { Apple = new AppleProviderOptions { ClientId = ServiceId } }),
            TimeProvider.System);

        Assert.Null(factory.Create(ServiceId));
    }

    private static AppleClientSecretFactory CreateFactory(string privateKey) => new(
        Options.Create(new AuthOptions
        {
            Apple = new AppleProviderOptions
            {
                ClientId = ServiceId,
                TeamId = TeamId,
                KeyId = KeyId,
                PrivateKey = privateKey,
            },
        }),
        TimeProvider.System);
}
