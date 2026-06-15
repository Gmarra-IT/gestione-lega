namespace ClassificaLega.Domain.Services;

public static class ScoringService
{
    private static readonly Dictionary<int, int> BonusRisultatoTable = new()
    {
        [12] = 8,
        [10] = 6,
        [9]  = 4,
        [8]  = 3,
        [7]  = 2,
        [6]  = 1,
    };

    public static int BonusRisultato(int matchPoints) =>
        BonusRisultatoTable.TryGetValue(matchPoints, out var bonus) ? bonus : 0;

    public static int BonusPartecipazione(int previousParticipations) =>
        previousParticipations < 5 ? 1 : 2;

    public static int ComputeTotalPoints(int matchPoints, int bonusRisultato, int bonusPartecipazione) =>
        matchPoints + bonusRisultato + bonusPartecipazione;

    public static int BestN(IEnumerable<int> stageTotals, int n)
    {
        var sorted = stageTotals.OrderByDescending(x => x).ToList();
        return sorted.Take(n).Sum();
    }

    /// <summary>
    /// Recomputes BonusPartecipazione for all results of a player, ordered by Stage.Number.
    /// Returns updated (stageId → bonusPartecipazione) map.
    /// </summary>
    public static Dictionary<int, int> RecomputePartecipazione(IEnumerable<(int stageId, int stageNumber)> playerStages)
    {
        var ordered = playerStages.OrderBy(s => s.stageNumber).ToList();
        var result = new Dictionary<int, int>();
        for (int i = 0; i < ordered.Count; i++)
            result[ordered[i].stageId] = BonusPartecipazione(previousParticipations: i);
        return result;
    }

    public static IReadOnlyList<StandingEntry> ComputeStandings(
        IEnumerable<PlayerScoreData> players,
        int countingStages)
    {
        var ranked = players
            .Select(p =>
            {
                var bestN = BestN(p.StageTotals, countingStages);
                var absoluteTotal = p.StageTotals.Sum();
                return new StandingEntry(p.PlayerId, p.DisplayName, bestN, absoluteTotal);
            })
            .OrderByDescending(x => x.BestN)
            .ThenByDescending(x => x.AbsoluteTotal)
            .ThenBy(x => x.DisplayName)
            .ToList();

        // assign positions (ties share same position)
        int pos = 1;
        for (int i = 0; i < ranked.Count; i++)
        {
            if (i > 0 &&
                ranked[i].BestN == ranked[i - 1].BestN &&
                ranked[i].AbsoluteTotal == ranked[i - 1].AbsoluteTotal)
                ranked[i] = ranked[i] with { Position = ranked[i - 1].Position };
            else
                ranked[i] = ranked[i] with { Position = pos };
            pos++;
        }

        return ranked;
    }
}

public record PlayerScoreData(int PlayerId, string DisplayName, IReadOnlyList<int> StageTotals);

public record StandingEntry(int PlayerId, string DisplayName, int BestN, int AbsoluteTotal)
{
    public int Position { get; init; }
}
