using ClassificaLega.Domain.Services;

namespace ClassificaLega.Domain.Entities;

public class Season
{
    public int Id { get; set; }
    public int LeagueId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalStages { get; set; } = 12;
    public int CountingStages { get; set; } = 8;  // = CountBestN della ScoringRule.
    // Regola di scoring configurabile (1:1 con la stagione), serializzata a jsonb.
    public ScoringRule ScoringRule { get; set; } = ScoringRule.Default();
    // Attiva: una sola per lega.
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public League League { get; set; } = null!;
    public ICollection<Stage> Stages { get; set; } = [];
    public ICollection<Player> Players { get; set; } = [];
}
