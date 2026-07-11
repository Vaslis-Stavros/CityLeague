using CityLeague.Core.Enums;

namespace CityLeague.Core.Formations;

/// <summary>A single position slot within a formation template.</summary>
/// <param name="SlotId">Unique slot id within the template, e.g. "h_gk".</param>
/// <param name="Label">Position label, e.g. "GK".</param>
/// <param name="Side">Which side of the pitch the slot belongs to.</param>
/// <param name="X">Normalized x (0 = home goal line, 1 = away goal line).</param>
/// <param name="Y">Normalized y (0 = top touchline, 1 = bottom touchline).</param>
public record SlotTemplate(string SlotId, string Label, MatchSide Side, double X, double Y);

/// <summary>A full pitch layout for a given format, with home and away slots.</summary>
public record FormationTemplate(string FormatKey, int PlayersPerSide, IReadOnlyList<SlotTemplate> Slots);
