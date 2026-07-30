using CityLeague.App.Helpers;
using CityLeague.Core.Dtos;

namespace CityLeague.App.ViewModels;

public enum HomeMatchKind
{
    Upcoming,
    PendingResult,
    Incomplete,
}

/// <summary>Presentation model for a scheduled match on the home glass schedule.</summary>
public sealed class HomeMatchItem
{
    public HomeMatchItem(EventSummaryDto summary, HomeMatchKind kind = HomeMatchKind.Upcoming)
    {
        Summary = summary;
        Kind = kind;
        var local = summary.ScheduledAt.ToLocalTime();
        DayLabel = FormatDay(local);
        TimeLabel = local.ToString("HH:mm");
        MetaLabel = BuildMeta(summary, kind);
        var open = Math.Max(0, summary.TotalSlots - summary.ClaimedCount);
        SpotsLabel = kind switch
        {
            HomeMatchKind.PendingResult => "Result needed",
            HomeMatchKind.Incomplete => summary.IsOwner ? "Reschedule or delete" : "Remove from your list",
            _ => open == 0
                ? (string.Equals(summary.Status, "Locked", StringComparison.OrdinalIgnoreCase) ? "Locked · Full" : "Full")
                : open == 1
                    ? "1 spot left"
                    : $"{open} spots left",
        };
        FillRatio = summary.TotalSlots <= 0
            ? 0
            : Math.Clamp((double)summary.ClaimedCount / summary.TotalSlots, 0, 1);
        IsAlmostFull = open > 0 && FillRatio >= 0.8;
        IsFull = open == 0;
        Accent = kind == HomeMatchKind.PendingResult
            ? Color.FromArgb("#F2A900")
            : SportColors.GetColor(summary.SportKey);
    }

    public EventSummaryDto Summary { get; }
    public HomeMatchKind Kind { get; }
    public string DayLabel { get; }
    public string TimeLabel { get; }
    public string MetaLabel { get; }
    public string SpotsLabel { get; }
    public double FillRatio { get; }
    public bool IsAlmostFull { get; }
    public bool IsFull { get; }
    public Color Accent { get; }
    public string Title => Summary.Title;

    private static string FormatDay(DateTimeOffset local)
    {
        var today = DateTimeOffset.Now.Date;
        var day = local.Date;
        if (day == today) return "Today";
        if (day == today.AddDays(1)) return "Tomorrow";
        if (day < today.AddDays(7) && day > today) return local.ToString("dddd");
        return local.ToString("ddd d MMM");
    }

    private static string BuildMeta(EventSummaryDto summary, HomeMatchKind kind)
    {
        var parts = new List<string>();
        if (kind == HomeMatchKind.PendingResult)
            parts.Add("Pending result");
        else if (kind == HomeMatchKind.Incomplete)
            parts.Add("Incomplete");
        parts.Add(summary.FormatName);
        if (!string.IsNullOrWhiteSpace(summary.Location))
            parts.Add(summary.Location.Trim());
        return string.Join(" · ", parts);
    }
}
