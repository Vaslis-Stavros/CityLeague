using System.Collections.Concurrent;
using CityLeague.Core.Enums;

namespace CityLeague.Core.Formations;

/// <summary>
/// Builds football formation templates deterministically for 5v5 through 11v11.
///
/// Rows run from each team's own goal outward toward the halfway line. The home team
/// occupies the left half (x in [0.06, 0.45]); the away team mirrors on the right
/// (x' = 1 - x). Within a row, players are spread evenly across the pitch width (y).
/// </summary>
public interface IFormationProvider
{
    FormationTemplate GetTemplate(string formatKey);
    bool TryGetTemplate(string formatKey, out FormationTemplate template);
}

public sealed class FormationProvider : IFormationProvider
{
    // Outfield rows (defense -> attack) per players-per-side. GK is added separately.
    private static readonly IReadOnlyDictionary<int, string[][]> Rows = new Dictionary<int, string[][]>
    {
        [5] = [["CB", "CB"], ["CM"], ["ST"]],
        [6] = [["CB", "CB"], ["CM", "CM"], ["ST"]],
        [7] = [["LB", "CB", "RB"], ["CM", "CM"], ["ST"]],
        [8] = [["LB", "CB", "RB"], ["LM", "CM", "RM"], ["ST"]],
        [9] = [["LB", "CB", "RB"], ["LM", "CM", "RM"], ["LF", "RF"]],
        [10] = [["LB", "LCB", "RCB", "RB"], ["LM", "CM", "RM"], ["LF", "RF"]],
        [11] = [["LB", "LCB", "RCB", "RB"], ["LM", "LCM", "RCM", "RM"], ["LS", "RS"]],
    };

    private readonly ConcurrentDictionary<string, FormationTemplate> _cache = new();

    public static string FormatKey(int playersPerSide) => $"football-{playersPerSide}v{playersPerSide}";

    public FormationTemplate GetTemplate(string formatKey)
    {
        if (!TryGetTemplate(formatKey, out var template))
            throw new KeyNotFoundException($"No formation template for '{formatKey}'.");
        return template;
    }

    public bool TryGetTemplate(string formatKey, out FormationTemplate template)
    {
        var cached = _cache.GetOrAdd(formatKey, Build);
        template = cached;
        return cached.Slots.Count > 0;
    }

    private static FormationTemplate Build(string formatKey)
    {
        var playersPerSide = ParsePlayersPerSide(formatKey);
        if (playersPerSide is null || !Rows.TryGetValue(playersPerSide.Value, out var rows))
            return new FormationTemplate(formatKey, 0, Array.Empty<SlotTemplate>());

        var slots = new List<SlotTemplate>(playersPerSide.Value * 2);
        BuildSide(MatchSide.Home, playersPerSide.Value, rows, slots);
        BuildSide(MatchSide.Away, playersPerSide.Value, rows, slots);
        return new FormationTemplate(formatKey, playersPerSide.Value, slots);
    }

    private static void BuildSide(MatchSide side, int playersPerSide, string[][] rows, List<SlotTemplate> slots)
    {
        var prefix = side == MatchSide.Home ? "h" : "a";
        var counters = new Dictionary<string, int>();
        var seq = 0;

        // Goalkeeper.
        var gkX = 0.06;
        slots.Add(new SlotTemplate(
            $"{prefix}_gk",
            "GK",
            side,
            MapX(side, gkX),
            0.5));

        var rowCount = rows.Length;
        for (var r = 0; r < rowCount; r++)
        {
            var row = rows[r];
            var rowX = rowCount == 1 ? 0.30 : 0.15 + 0.30 * r / (rowCount - 1);
            for (var i = 0; i < row.Length; i++)
            {
                var label = row[i];
                var y = (i + 1.0) / (row.Length + 1.0);
                counters.TryGetValue(label, out var n);
                counters[label] = n + 1;
                var slotId = $"{prefix}_{label.ToLowerInvariant()}{(row.Length > 1 ? (n + 1).ToString() : string.Empty)}_{seq++}";
                slots.Add(new SlotTemplate(slotId, label, side, MapX(side, rowX), y));
            }
        }
    }

    private static double MapX(MatchSide side, double homeX) => side == MatchSide.Home ? homeX : 1.0 - homeX;

    private static int? ParsePlayersPerSide(string formatKey)
    {
        // Expected form: "football-7v7".
        var dash = formatKey.IndexOf('-');
        if (dash < 0) return null;
        var spec = formatKey[(dash + 1)..];
        var v = spec.IndexOf('v');
        if (v <= 0) return null;
        return int.TryParse(spec[..v], out var pps) ? pps : null;
    }
}
