namespace ClassificaLega.Domain.Entities;

/// <summary>Logo della lega, blob su DB. Tabella separata (1-1 con League) così i byte
/// non vengono mai caricati dalle query normali su League (es. risoluzione tenant).</summary>
public class LeagueLogo
{
    // PK = FK verso League (relazione 1-1).
    public int LeagueId { get; set; }
    public League League { get; set; } = null!;

    public byte[] Bytes { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    // Hash dei byte (base64url) per ETag/cache HTTP e cache-busting lato client.
    public string ETag { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
