using ClassificaLega.Domain.Services;

namespace ClassificaLega.Tests;

public class ScoringServiceTests
{
    // --- BonusRisultato ---

    [Theory]
    [InlineData(12, 8)]
    [InlineData(10, 6)]
    [InlineData(9,  4)]
    [InlineData(8,  3)]
    [InlineData(7,  2)]
    [InlineData(6,  1)]
    [InlineData(5,  0)]
    [InlineData(3,  0)]
    [InlineData(0,  0)]
    public void BonusRisultato_ReturnsCorrectBonus(int matchPoints, int expected) =>
        Assert.Equal(expected, ScoringService.BonusRisultato(matchPoints));

    // --- BonusPartecipazione ---

    [Theory]
    [InlineData(0, 1)]  // 1ª partecipazione
    [InlineData(1, 1)]
    [InlineData(4, 1)]  // 5ª partecipazione (prev=4)
    [InlineData(5, 2)]  // 6ª partecipazione (prev=5)
    [InlineData(10, 2)]
    public void BonusPartecipazione_Threshold(int previousParticipations, int expected) =>
        Assert.Equal(expected, ScoringService.BonusPartecipazione(previousParticipations));

    // --- ComputeTotalPoints ---

    [Fact]
    public void TotalPoints_SumsThreeComponents()
    {
        // 4-0-0: matchPoints=12, bonusRisultato=8, bonusPartecipazione=1 → 21
        var bp = ScoringService.BonusPartecipazione(0);
        var br = ScoringService.BonusRisultato(12);
        Assert.Equal(21, ScoringService.ComputeTotalPoints(12, br, bp));
    }

    [Fact]
    public void TotalPoints_MichelePardiniTappa1()
    {
        // matchPoints=12, BonusRisultato=8, BonusPartecipazione=1 (prima tappa) → 21
        var br = ScoringService.BonusRisultato(12);
        var bp = ScoringService.BonusPartecipazione(0);
        Assert.Equal(21, ScoringService.ComputeTotalPoints(12, br, bp));
    }

    // --- BestN ---

    [Fact]
    public void BestN_MoreThanN_TakesTopN()
    {
        var totals = new[] { 10, 20, 15, 5, 18, 12, 8, 25, 3, 17 };
        // top 8: 25,20,18,17,15,12,10,8 = 125
        Assert.Equal(125, ScoringService.BestN(totals, 8));
    }

    [Fact]
    public void BestN_LessThanN_SumsAll()
    {
        var totals = new[] { 10, 20, 15 };
        Assert.Equal(45, ScoringService.BestN(totals, 8));
    }

    [Fact]
    public void BestN_ExactlyN_SumsAll()
    {
        var totals = new[] { 10, 20, 15, 5, 18, 12, 8, 25 };
        Assert.Equal(113, ScoringService.BestN(totals, 8));
    }

    [Fact]
    public void BestN_N1_ReturnsMax()
    {
        var totals = new[] { 10, 20, 15 };
        Assert.Equal(20, ScoringService.BestN(totals, 1));
    }

    // --- RecomputePartecipazione ---

    [Fact]
    public void RecomputePartecipazione_OrdersByStageNumber()
    {
        // stages in reverse order — should still assign bonus by position
        var stages = new[]
        {
            (stageId: 3, stageNumber: 3),
            (stageId: 1, stageNumber: 1),
            (stageId: 2, stageNumber: 2),
        };
        var result = ScoringService.RecomputePartecipazione(stages);
        Assert.Equal(1, result[1]); // stageNumber=1 → prev=0 → 1
        Assert.Equal(1, result[2]); // stageNumber=2 → prev=1 → 1
        Assert.Equal(1, result[3]); // stageNumber=3 → prev=2 → 1
    }

    [Fact]
    public void RecomputePartecipazione_SixthStageGetsBonus2()
    {
        var stages = Enumerable.Range(1, 7)
            .Select(n => (stageId: n, stageNumber: n))
            .ToArray();
        var result = ScoringService.RecomputePartecipazione(stages);
        // stages 1-5 → prev 0-4 → bonus 1; stages 6-7 → prev 5-6 → bonus 2
        for (int id = 1; id <= 5; id++)
            Assert.Equal(1, result[id]);
        Assert.Equal(2, result[6]);
        Assert.Equal(2, result[7]);
    }

    // --- ComputeStandings ---

    [Fact]
    public void Standings_OrdersByBestNDesc()
    {
        var players = new[]
        {
            new PlayerScoreData(1, "Alice", [10, 20]),
            new PlayerScoreData(2, "Bob",   [15, 15]),
        };
        var standings = ScoringService.ComputeStandings(players, 8);
        // Alice BestN=30, Bob BestN=30 — tie → absolute same → name asc → Alice first
        Assert.Equal("Alice", standings[0].DisplayName);
        Assert.Equal("Bob", standings[1].DisplayName);
    }

    [Fact]
    public void Standings_TiebreakerAbsoluteTotal()
    {
        var players = new[]
        {
            new PlayerScoreData(1, "Alice", [20, 10, 5]),  // BestN(2)=30, abs=35
            new PlayerScoreData(2, "Bob",   [20, 10, 8]),  // BestN(2)=30, abs=38
        };
        var standings = ScoringService.ComputeStandings(players, 2);
        Assert.Equal("Bob", standings[0].DisplayName);
        Assert.Equal("Alice", standings[1].DisplayName);
    }

    [Fact]
    public void Standings_TiebreakerNameAsc()
    {
        var players = new[]
        {
            new PlayerScoreData(1, "Zara", [20]),
            new PlayerScoreData(2, "Anna", [20]),
        };
        var standings = ScoringService.ComputeStandings(players, 8);
        Assert.Equal("Anna", standings[0].DisplayName);
        Assert.Equal("Zara", standings[1].DisplayName);
    }

    [Fact]
    public void Standings_TiedPlayersSharePosition()
    {
        var players = new[]
        {
            new PlayerScoreData(1, "Anna", [20]),
            new PlayerScoreData(2, "Zara", [20]),
            new PlayerScoreData(3, "Carl", [10]),
        };
        var standings = ScoringService.ComputeStandings(players, 8);
        Assert.Equal(1, standings[0].Position);
        Assert.Equal(1, standings[1].Position);
        Assert.Equal(3, standings[2].Position);
    }

    [Fact]
    public void Standings_MarracciniFirst_BestN89()
    {
        // Minimal fixture: Marraccini has 7 tappe totalling BestN=89 with CountingStages=8
        // (fewer than 8 stages → sum all)
        var marracciniTotals = new[] { 14, 14, 13, 13, 12, 12, 11 }; // sum=89
        var otherTotals = new[] { 12, 12, 12, 12, 12, 11, 10 };      // sum=81

        var players = new[]
        {
            new PlayerScoreData(1, "Marraccini", marracciniTotals),
            new PlayerScoreData(2, "OtherPlayer", otherTotals),
        };
        var standings = ScoringService.ComputeStandings(players, 8);
        Assert.Equal(1, standings[0].Position);
        Assert.Equal("Marraccini", standings[0].DisplayName);
        Assert.Equal(89, standings[0].BestN);
    }

    // --- Real workbook data (foglio Tappe, 22 giocatori × 7 tappe) ---
    // Per-tappa totals straight from the workbook; verifies ComputeStandings reproduces
    // the Classifica sheet.
    private static readonly (string Name, int[] Totals)[] WorkbookTotals =
    [
        ("Bruno Barbieri",          [14, 8, 14, 8]),
        ("Daniel Gemignani",        [4, 8, 4]),
        ("Daniele Gambini",         [14]),
        ("Dario Tommasi",           [8, 14, 8, 8]),
        ("Gabriele Marraccini",     [14, 4, 10, 17, 14, 15, 15]),
        ("Gianmarco Bina",          [5]),
        ("Gianmarco Del Bucchia",   [10, 1, 21, 8, 8, 15, 6]),
        ("Gianmarco Venturini",     [4]),
        ("Gianmarco Volpe",         [8, 8, 4, 5]),
        ("Gioca Turo",              [21]),
        ("Giulio Bertozzi",         [1, 1]),
        ("Igor Fustini",            [14, 5, 8, 14, 8, 5]),
        ("Jhonathan Lipparelli",    [5, 8]),
        ("Leonardo Guerra Silicani",[14]),
        ("Massimiliano Lombardi",   [2]),
        ("Michele Pardini",         [8, 8, 8]),
        ("Nicola Dalle Mura",       [8, 14, 4, 21, 8, 15]),
        ("Nicola Pardini",          [4, 4, 14, 14, 8]),
        ("Paolo Baroni",            [8, 8, 10, 5, 8, 15, 9]),
        ("Roberto Randazzo",        [8, 8, 4, 4, 1]),
        ("Stefano Ghiara",          [10]),
        ("Tommaso Duccini",         [8, 14]),
    ];

    [Fact]
    public void Standings_RealWorkbookData_ReproducesClassifica()
    {
        var players = WorkbookTotals
            .Select((p, i) => new PlayerScoreData(i + 1, p.Name, p.Totals))
            .ToList();
        var standings = ScoringService.ComputeStandings(players, countingStages: 8);

        Assert.Equal("Gabriele Marraccini", standings[0].DisplayName);
        Assert.Equal(89, standings[0].BestN);
        Assert.Equal(1, standings[0].Position);

        Assert.Equal("Nicola Dalle Mura", standings[1].DisplayName);
        Assert.Equal(70, standings[1].BestN);

        Assert.Equal("Gianmarco Del Bucchia", standings[2].DisplayName);
        Assert.Equal(69, standings[2].BestN);

        Assert.Equal("Paolo Baroni", standings[3].DisplayName);
        Assert.Equal(63, standings[3].BestN);

        Assert.Equal("Igor Fustini", standings[4].DisplayName);
        Assert.Equal(54, standings[4].BestN);
    }

    [Fact]
    public void Standings_RealData_CountingStagesShrinksToTop3()
    {
        var players = WorkbookTotals
            .Select((p, i) => new PlayerScoreData(i + 1, p.Name, p.Totals))
            .ToList();
        // With only top-3 stages counting the ranking shifts: Nicola Dalle Mura best3 =
        // 21+15+14 = 50 overtakes Marraccini best3 = 17+15+15 = 47.
        var standings = ScoringService.ComputeStandings(players, countingStages: 3);
        Assert.Equal("Nicola Dalle Mura", standings[0].DisplayName);
        Assert.Equal(50, standings[0].BestN);
        Assert.Equal("Gabriele Marraccini", standings[1].DisplayName);
        Assert.Equal(47, standings[1].BestN);
    }

    [Fact]
    public void Standings_CountingStagesChange_ReordersRanking()
    {
        // Player A: high consistency; Player B: one big score
        var playerA = new PlayerScoreData(1, "A", [10, 10, 10, 10]);
        var playerB = new PlayerScoreData(2, "B", [25, 5, 5, 5]);

        // CountingStages=1 → B wins (25 vs 10)
        var s1 = ScoringService.ComputeStandings([playerA, playerB], 1);
        Assert.Equal("B", s1[0].DisplayName);

        // CountingStages=4 → A wins (40 vs 40 abs tie — same here; use name tiebreak A < B)
        // actually: A=40, B=40 → abs tie → name → A
        var s4 = ScoringService.ComputeStandings([playerA, playerB], 4);
        Assert.Equal("A", s4[0].DisplayName);
    }
}
