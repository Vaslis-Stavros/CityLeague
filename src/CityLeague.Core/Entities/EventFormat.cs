namespace CityLeague.Core.Entities;

/// <summary>A concrete match format for a sport, e.g. football 7v7.</summary>
public class EventFormat
{
    public int Id { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    /// <summary>Stable machine key, e.g. "football-7v7".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human label, e.g. "7 vs 7".</summary>
    public string Name { get; set; } = string.Empty;

    public int PlayersPerSide { get; set; }

    /// <summary>Key used to look up the formation template (usually equal to <see cref="Key"/>).</summary>
    public string FormationTemplateId { get; set; } = string.Empty;
}
