using CityLeague.Core.Validation;
using Xunit;

namespace CityLeague.Core.Tests;

public class HandleValidatorTests
{
    [Theory]
    [InlineData("alex_k")]
    [InlineData("abc")]
    [InlineData("player_123")]
    public void Valid_handles_pass(string handle)
    {
        Assert.True(HandleValidator.IsValid(handle, out var reason));
        Assert.Null(reason);
    }

    [Theory]
    [InlineData("ab")]              // too short
    [InlineData("this_handle_is_way_too_long")] // too long
    [InlineData("Alex K")]          // space + uppercase
    [InlineData("bad-char!")]       // invalid chars
    [InlineData("admin")]           // reserved
    public void Invalid_handles_fail(string handle)
    {
        Assert.False(HandleValidator.IsValid(handle, out var reason));
        Assert.NotNull(reason);
    }

    [Fact]
    public void Normalize_lowercases_and_trims()
    {
        Assert.Equal("alex_k", HandleValidator.Normalize("  Alex_K  "));
    }
}
