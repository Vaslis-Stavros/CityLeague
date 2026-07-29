using CityLeague.Core.Validation;
using Xunit;

namespace CityLeague.Api.Tests;

public class DisplayNameResolverTests
{
    [Theory]
    [InlineData("Alex Smith", "alex@gmail.com", null, "Alex Smith")]
    [InlineData("Google player", "alex.smith@gmail.com", null, "Alex Smith")]
    [InlineData("Microsoft player", "jordan@outlook.com", "jordan", "Jordan")]
    [InlineData("Player", null, "cool_handle", "cool_handle")]
    [InlineData(null, "sam.lee@cityleague.app", null, "Sam Lee")]
    [InlineData("", "single@test.com", null, "Single")]
    [InlineData("Apple player", null, null, "Player")]
    public void Resolve_prefers_real_name_then_email_local_then_handle(
        string? name, string? email, string? handle, string expected)
    {
        Assert.Equal(expected, DisplayNameResolver.Resolve(name, email, handle));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("   ", true)]
    [InlineData("Player", true)]
    [InlineData("Google player", true)]
    [InlineData("Microsoft player", true)]
    [InlineData("Apple player", true)]
    [InlineData("Alex", false)]
    [InlineData("Alex Smith", false)]
    public void IsPlaceholder_detects_provider_defaults(string? name, bool expected)
        => Assert.Equal(expected, DisplayNameResolver.IsPlaceholder(name));

    [Fact]
    public void FromEmail_formats_dots_and_underscores()
    {
        Assert.Equal("Alex Smith", DisplayNameResolver.FromEmail("alex.smith@gmail.com"));
        Assert.Equal("Jordan Lee", DisplayNameResolver.FromEmail("jordan_lee@outlook.com"));
        Assert.Null(DisplayNameResolver.FromEmail(null));
        Assert.Null(DisplayNameResolver.FromEmail("not-an-email"));
    }
}
