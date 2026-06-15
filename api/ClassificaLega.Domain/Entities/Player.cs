namespace ClassificaLega.Domain.Entities;

public class Player
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string NormalizedKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Season Season { get; set; } = null!;
    public ICollection<Result> Results { get; set; } = [];
}
