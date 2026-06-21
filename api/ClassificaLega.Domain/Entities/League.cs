namespace ClassificaLega.Domain.Entities;

public class League
{
    public int Id { get; set; }
    // URL slug, lowercase, primo segmento dopo /lega-pauper/ (es. "massarosa", "pisa").
    public string Slug { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    // Etichetta di branding mostrata in testata (fallback: Name).
    public string? Title { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Season> Seasons { get; set; } = [];
}
