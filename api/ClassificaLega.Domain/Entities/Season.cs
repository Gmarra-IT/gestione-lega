namespace ClassificaLega.Domain.Entities;

public class Season
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalStages { get; set; } = 12;
    public int CountingStages { get; set; } = 8;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Stage> Stages { get; set; } = [];
    public ICollection<Player> Players { get; set; } = [];
}
