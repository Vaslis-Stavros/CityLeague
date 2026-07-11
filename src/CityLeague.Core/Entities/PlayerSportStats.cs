namespace CityLeague.Core.Entities;

/// <summary>Aggregated per-user, per-sport statistics.</summary>
public class PlayerSportStats
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    public int SportId { get; set; }
    public Sport? Sport { get; set; }

    public int Played { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
}
