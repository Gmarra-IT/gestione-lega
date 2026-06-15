namespace ClassificaLega.Domain.Entities;

public class Stage
{
    public int Id { get; set; }
    public int SeasonId { get; set; }
    public int Number { get; set; }
    public string? Name { get; set; }
    public DateOnly? Date { get; set; }
    public string? EventLinkId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Season Season { get; set; } = null!;
    public ICollection<Result> Results { get; set; } = [];
}
