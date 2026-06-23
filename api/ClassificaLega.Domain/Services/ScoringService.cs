namespace ClassificaLega.Domain.Services;

/// <summary>
/// Logica di scoring pura (nessuna dipendenza DB), parametrizzata su <see cref="ScoringRule"/>.
/// TournamentTotal = matchPoints + positionBonus + scoreBonus + participationPoints.
/// </summary>
public static class ScoringService
{
    // --- componenti per singolo torneo ---

    public static int MatchPoints(int wins, int draws, int losses, ScoringRule rule) =>
        wins * rule.PointsPerWin + draws * rule.PointsPerDraw + losses * rule.PointsPerLoss;

    /// <summary>Bonus a soglia: voce con FromMatchPoints più alta ≤ matchPoints (0 se nessuna).</summary>
    public static int ScoreBonusFor(int matchPoints, ScoringRule rule) =>
        rule.ScoreBonuses
            .Where(b => b.FromMatchPoints <= matchPoints)
            .OrderByDescending(b => b.FromMatchPoints)
            .Select(b => b.Points)
            .FirstOrDefault();

    /// <summary>Bonus piazzamento (0 se Position assente o non mappata).</summary>
    public static int PositionBonusFor(int? position, ScoringRule rule) =>
        position is { } p
            ? rule.PositionBonuses.FirstOrDefault(b => b.Position == p)?.Points ?? 0
            : 0;

    /// <summary>Punti fascia di presenza per indice progressivo 1-based (0 se nessuna fascia).</summary>
    public static int ParticipationPointsFor(int progressiveIndex, ScoringRule rule) =>
        rule.ParticipationTiers
            .Where(t => t.FromTournament <= progressiveIndex)
            .OrderByDescending(t => t.FromTournament)
            .Select(t => t.PointsPerParticipation)
            .FirstOrDefault();

    /// <summary>Ordina le partecipazioni cronologicamente (data, poi numero tappa, poi id come
    /// tie-break stabile) e calcola il breakdown per torneo. L'indice 1-based serve alla fascia presenza.</summary>
    public static IReadOnlyList<TournamentBreakdown> ComputeBreakdowns(
        IEnumerable<TournamentScore> tournaments, ScoringRule rule)
    {
        var ordered = tournaments
            .OrderBy(t => t.Date ?? DateOnly.MaxValue)
            .ThenBy(t => t.StageNumber)
            .ThenBy(t => t.TournamentId)
            .ToList();

        var result = new List<TournamentBreakdown>(ordered.Count);
        for (int i = 0; i < ordered.Count; i++)
        {
            var t = ordered[i];
            var positionBonus = PositionBonusFor(t.Position, rule);
            var scoreBonus = ScoreBonusFor(t.MatchPoints, rule);
            var participation = ParticipationPointsFor(i + 1, rule);
            var total = t.MatchPoints + positionBonus + scoreBonus + participation;
            result.Add(new TournamentBreakdown(
                t.TournamentId, t.TournamentName, t.Date,
                t.MatchPoints, positionBonus, scoreBonus, participation, total));
        }
        return result;
    }

    /// <summary>Somma dei migliori <paramref name="countBestN"/> totali-torneo (o tutti se ≥ count).</summary>
    public static int BestN(IEnumerable<int> tournamentTotals, int countBestN) =>
        tournamentTotals.OrderByDescending(x => x).Take(countBestN).Sum();

    /// <summary>
    /// Classifica. Ordinamento (INVARIATO): TotalPoints (best N) desc → AbsoluteTotal desc → nome asc.
    /// Pari merito condividono il Rank. Espone breakdown dei tornei contati.
    /// </summary>
    public static IReadOnlyList<StandingEntry> ComputeStandings(
        IEnumerable<PlayerScoreData> players, ScoringRule rule, int countBestN)
    {
        var ranked = players
            .Select(p =>
            {
                var breakdowns = ComputeBreakdowns(p.Tournaments, rule);
                var absoluteTotal = breakdowns.Sum(b => b.Total);
                var counted = breakdowns.OrderByDescending(b => b.Total).Take(countBestN).ToList();
                var totalPoints = counted.Sum(b => b.Total);
                return new StandingEntry(
                    p.PlayerId, p.DisplayName, totalPoints, absoluteTotal,
                    breakdowns.Count, counted.Count, counted);
            })
            .OrderByDescending(x => x.TotalPoints)
            .ThenByDescending(x => x.AbsoluteTotal)
            .ThenBy(x => x.DisplayName)
            .ToList();

        int rank = 1;
        for (int i = 0; i < ranked.Count; i++)
        {
            if (i > 0 &&
                ranked[i].TotalPoints == ranked[i - 1].TotalPoints &&
                ranked[i].AbsoluteTotal == ranked[i - 1].AbsoluteTotal)
                ranked[i] = ranked[i] with { Rank = ranked[i - 1].Rank };
            else
                ranked[i] = ranked[i] with { Rank = rank };
            rank++;
        }

        return ranked;
    }
}

/// <summary>Dati grezzi di una partecipazione a un torneo (tappa) per il calcolo classifica.</summary>
public record TournamentScore(
    int TournamentId,
    string TournamentName,
    DateOnly? Date,
    int StageNumber,
    int MatchPoints,
    int? Position);

/// <summary>Un giocatore con tutte le sue partecipazioni.</summary>
public record PlayerScoreData(int PlayerId, string DisplayName, IReadOnlyList<TournamentScore> Tournaments);

/// <summary>Composizione del punteggio di un torneo: somma componenti == Total.</summary>
public record TournamentBreakdown(
    int TournamentId,
    string TournamentName,
    DateOnly? Date,
    int MatchPoints,
    int PositionBonus,
    int ScoreBonus,
    int ParticipationPoints,
    int Total);

/// <summary>Riga di classifica con breakdown dei tornei contati.</summary>
public record StandingEntry(
    int PlayerId,
    string DisplayName,
    int TotalPoints,
    int AbsoluteTotal,
    int TournamentsPlayed,
    int TournamentsCountedForTotal,
    IReadOnlyList<TournamentBreakdown> BestResults)
{
    public int Rank { get; init; }
}
