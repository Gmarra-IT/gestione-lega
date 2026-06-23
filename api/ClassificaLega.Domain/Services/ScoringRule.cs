namespace ClassificaLega.Domain.Services;

/// <summary>Bonus per piazzamento finale di un torneo (1° posto, 2°, …).</summary>
public sealed class PositionBonus
{
    public int Position { get; set; }
    public int Points { get; set; }
}

/// <summary>Bonus a soglia sui punti-match del torneo (NUOVO): si applica la voce con
/// <see cref="FromMatchPoints"/> più alta ≤ matchPoints.</summary>
public sealed class ScoreBonus
{
    public int FromMatchPoints { get; set; }
    public int Points { get; set; }
}

/// <summary>Punti per fascia di presenza: si applica la voce con <see cref="FromTournament"/>
/// più alta ≤ indice progressivo (1-based) della partecipazione.</summary>
public sealed class ParticipationTier
{
    public int FromTournament { get; set; }
    public int PointsPerParticipation { get; set; }
}

/// <summary>
/// Configurazione di scoring 1:1 col campionato (stagione). Modello puro, nessuna dipendenza DB.
/// Tutta la logica di calcolo vive in <see cref="ScoringService"/>.
/// </summary>
public sealed class ScoringRule
{
    public int PointsPerWin { get; set; } = 3;
    public int PointsPerDraw { get; set; } = 1;
    public int PointsPerLoss { get; set; } = 0;

    public List<PositionBonus> PositionBonuses { get; set; } = [];
    public List<ScoreBonus> ScoreBonuses { get; set; } = [];
    public List<ParticipationTier> ParticipationTiers { get; set; } = [];

    /// <summary>Valida la regola. Ritorna il messaggio d'errore, o null se valida.</summary>
    public string? Validate()
    {
        if (PointsPerWin < 0) return "PointsPerWin deve essere >= 0.";
        if (PointsPerDraw < 0) return "PointsPerDraw deve essere >= 0.";
        if (PointsPerLoss < 0) return "PointsPerLoss deve essere >= 0.";

        if (PositionBonuses.Any(b => b.Position <= 0))
            return "PositionBonuses: Position deve essere > 0.";
        if (PositionBonuses.Select(b => b.Position).Distinct().Count() != PositionBonuses.Count)
            return "PositionBonuses: Position duplicate.";

        if (ScoreBonuses.Any(b => b.FromMatchPoints < 0))
            return "ScoreBonuses: FromMatchPoints deve essere >= 0.";
        if (ScoreBonuses.Select(b => b.FromMatchPoints).Distinct().Count() != ScoreBonuses.Count)
            return "ScoreBonuses: FromMatchPoints duplicati.";
        if (!IsAscending(ScoreBonuses.Select(b => b.FromMatchPoints)))
            return "ScoreBonuses: lista non ordinata per FromMatchPoints crescente.";

        if (ParticipationTiers.Any(t => t.FromTournament <= 0))
            return "ParticipationTiers: FromTournament deve essere > 0.";
        if (ParticipationTiers.Select(t => t.FromTournament).Distinct().Count() != ParticipationTiers.Count)
            return "ParticipationTiers: FromTournament duplicati.";
        if (!IsAscending(ParticipationTiers.Select(t => t.FromTournament)))
            return "ParticipationTiers: lista non ordinata per FromTournament crescente.";

        return null;
    }

    private static bool IsAscending(IEnumerable<int> values)
    {
        int? prev = null;
        foreach (var v in values)
        {
            if (prev is { } p && v < p) return false;
            prev = v;
        }
        return true;
    }

    /// <summary>Regola di default: riversa il cablato storico (lega Massarosa) come soglie.
    /// ScoreBonus exact→soglia: 6→1,7→2,8→3,9→4,10→6,12→8. Partecipazione: 1ª–5ª +1, dalla 6ª +2.</summary>
    public static ScoringRule Default() => new()
    {
        PointsPerWin = 3,
        PointsPerDraw = 1,
        PointsPerLoss = 0,
        PositionBonuses = [],
        ScoreBonuses =
        [
            new ScoreBonus { FromMatchPoints = 6,  Points = 1 },
            new ScoreBonus { FromMatchPoints = 7,  Points = 2 },
            new ScoreBonus { FromMatchPoints = 8,  Points = 3 },
            new ScoreBonus { FromMatchPoints = 9,  Points = 4 },
            new ScoreBonus { FromMatchPoints = 10, Points = 6 },
            new ScoreBonus { FromMatchPoints = 12, Points = 8 },
        ],
        ParticipationTiers =
        [
            new ParticipationTier { FromTournament = 1, PointsPerParticipation = 1 },
            new ParticipationTier { FromTournament = 6, PointsPerParticipation = 2 },
        ],
    };
}
