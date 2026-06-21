using ClassificaLega.Api.Services;
using ClassificaLega.Domain.Entities;

namespace ClassificaLega.Api.Tenancy;

/// <summary>Lega corrente della richiesta, risolta dall'header X-League-Slug dal middleware.
/// Scoped: una istanza per richiesta.</summary>
public class LeagueContext
{
    public League? Current { get; set; }

    // Stagione richiesta dall'header X-Season-Id (opzionale). Null → stagione attiva.
    // I service validano che appartenga alla lega corrente prima di usarla.
    public int? RequestedSeasonId { get; set; }

    public int RequireLeagueId() =>
        Current?.Id ?? throw ApiException.NotFound("Lega non specificata o inesistente.");
}
