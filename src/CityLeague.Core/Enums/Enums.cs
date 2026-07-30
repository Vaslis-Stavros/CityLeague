namespace CityLeague.Core.Enums;

/// <summary>Whether a sport is fully playable or shown as a "Coming Soon" placeholder.</summary>
public enum SportAvailability
{
    Enabled = 0,
    ComingSoon = 1,
}

/// <summary>State of a contact relationship between two users.</summary>
public enum ContactStatus
{
    Pending = 0,
    Accepted = 1,
    Blocked = 2,
}

/// <summary>Lifecycle of a single match/event.</summary>
public enum EventStatus
{
    Open = 0,
    InProgress = 1,
    Completed = 2,
    Cancelled = 3,
}

/// <summary>Which side of the pitch a slot or player belongs to.</summary>
public enum MatchSide
{
    Home = 0,
    Away = 1,
}

/// <summary>Outcome of a completed match.</summary>
public enum WinningSide
{
    Home = 0,
    Away = 1,
    Draw = 2,
}

/// <summary>Lifecycle of a league.</summary>
public enum LeagueStatus
{
    /// <summary>Existing leagues created before Draft existed; treat as running.</summary>
    Active = 0,
    Terminated = 1,
    /// <summary>Created but not started; leaders and rosters can still be configured.</summary>
    Draft = 2,
}
