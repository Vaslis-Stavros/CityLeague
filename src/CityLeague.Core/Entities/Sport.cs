using CityLeague.Core.Enums;

namespace CityLeague.Core.Entities;

/// <summary>A sport supported (or planned) by the app.</summary>
public class Sport
{
    public int Id { get; set; }

    /// <summary>Stable machine key, e.g. "football", "padel".</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public SportAvailability Availability { get; set; } = SportAvailability.ComingSoon;

    public int SortOrder { get; set; }

    public ICollection<EventFormat> Formats { get; set; } = new List<EventFormat>();
}
