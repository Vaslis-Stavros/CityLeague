using CityLeague.Core.Enums;
using CityLeague.Core.Formations;
using Xunit;

namespace CityLeague.Core.Tests;

public class FormationProviderTests
{
    private readonly FormationProvider _provider = new();

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    public void Template_has_correct_slot_counts_per_side(int perSide)
    {
        var template = _provider.GetTemplate(FormationProvider.FormatKey(perSide));

        Assert.Equal(perSide, template.PlayersPerSide);
        Assert.Equal(perSide * 2, template.Slots.Count);
        Assert.Equal(perSide, template.Slots.Count(s => s.Side == MatchSide.Home));
        Assert.Equal(perSide, template.Slots.Count(s => s.Side == MatchSide.Away));
    }

    [Fact]
    public void Slot_ids_are_unique_and_each_side_has_a_goalkeeper()
    {
        var template = _provider.GetTemplate(FormationProvider.FormatKey(11));

        Assert.Equal(template.Slots.Count, template.Slots.Select(s => s.SlotId).Distinct().Count());
        Assert.Contains(template.Slots, s => s.SlotId == "h_gk" && s.Side == MatchSide.Home);
        Assert.Contains(template.Slots, s => s.SlotId == "a_gk" && s.Side == MatchSide.Away);
    }

    [Fact]
    public void Coordinates_are_normalized_and_sides_are_mirrored()
    {
        var template = _provider.GetTemplate(FormationProvider.FormatKey(7));

        Assert.All(template.Slots, s =>
        {
            Assert.InRange(s.X, 0.0, 1.0);
            Assert.InRange(s.Y, 0.0, 1.0);
        });

        // Home keeper is on the left, away keeper mirrored on the right.
        var home = template.Slots.Single(s => s.SlotId == "h_gk");
        var away = template.Slots.Single(s => s.SlotId == "a_gk");
        Assert.True(home.X < 0.5);
        Assert.True(away.X > 0.5);
        Assert.Equal(1.0 - home.X, away.X, precision: 5);
    }

    [Fact]
    public void Unknown_format_returns_empty_template()
    {
        Assert.False(_provider.TryGetTemplate("football-99v99", out var template));
        Assert.Empty(template.Slots);
    }
}
