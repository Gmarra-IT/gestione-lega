using ClassificaLega.Domain.Services;

namespace ClassificaLega.Tests;

public class ScoringServiceTests
{
    private static readonly ScoringRule Default = ScoringRule.Default();

    // Costruisce un giocatore da match points per tappa (null = assente). Position/date assenti →
    // ordinamento cronologico = numero tappa.
    private static PlayerScoreData Player(int id, string name, int?[] matchPointsPerStage) =>
        new(id, name, matchPointsPerStage
            .Select((mp, idx) => (mp, number: idx + 1))
            .Where(x => x.mp.HasValue)
            .Select(x => new TournamentScore(id * 100 + x.number, $"Tappa {x.number}", null, x.number, x.mp!.Value, null))
            .ToList());

    // --- matchPoints da W/D/L ---

    [Theory]
    [InlineData(4, 0, 0, 12)]  // 4-0-0
    [InlineData(3, 0, 1, 9)]   // 3-0-1
    [InlineData(2, 2, 0, 8)]   // 2 win + 2 draw
    [InlineData(0, 0, 4, 0)]
    public void MatchPoints_FromWinsDrawsLosses(int w, int d, int l, int expected) =>
        Assert.Equal(expected, ScoringService.MatchPoints(w, d, l, Default)); // 3/1/0

    [Fact]
    public void MatchPoints_HonorsConfiguredPoints()
    {
        var rule = new ScoringRule { PointsPerWin = 2, PointsPerDraw = 1, PointsPerLoss = 1 };
        Assert.Equal(2 * 2 + 1 * 1 + 1 * 1, ScoringService.MatchPoints(2, 1, 1, rule)); // 6
    }

    // --- scoreBonus a soglia ---

    [Theory]
    [InlineData(9, 0)]    // sotto la soglia minima (10)
    [InlineData(10, 5)]   // esattamente soglia inferiore
    [InlineData(11, 5)]   // intermedio → soglia inferiore
    [InlineData(12, 8)]   // soglia massima
    [InlineData(100, 8)]  // oltre la massima → soglia massima
    public void ScoreBonus_Threshold(int matchPoints, int expected)
    {
        // Esempio del brief che riversa il cablato: 10→+5, 12→+8.
        var rule = new ScoringRule
        {
            ScoreBonuses =
            [
                new ScoreBonus { FromMatchPoints = 10, Points = 5 },
                new ScoreBonus { FromMatchPoints = 12, Points = 8 },
            ],
        };
        Assert.Equal(expected, ScoringService.ScoreBonusFor(matchPoints, rule));
    }

    [Fact]
    public void ScoreBonus_NoBonusesConfigured_IsZero() =>
        Assert.Equal(0, ScoringService.ScoreBonusFor(50, new ScoringRule()));

    // --- participationPoints per indice progressivo ---

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]   // dalla 6ª
    [InlineData(20, 2)]
    public void ParticipationPoints_ByProgressiveIndex(int index, int expected) =>
        Assert.Equal(expected, ScoringService.ParticipationPointsFor(index, Default));

    [Fact]
    public void ParticipationPoints_BelowFirstTier_IsZero()
    {
        var rule = new ScoringRule
        {
            ParticipationTiers = [new ParticipationTier { FromTournament = 3, PointsPerParticipation = 1 }],
        };
        Assert.Equal(0, ScoringService.ParticipationPointsFor(2, rule));
        Assert.Equal(1, ScoringService.ParticipationPointsFor(3, rule));
    }

    // --- positionBonus ---

    [Fact]
    public void PositionBonus_MappedAndUnmapped()
    {
        var rule = new ScoringRule
        {
            PositionBonuses =
            [
                new PositionBonus { Position = 1, Points = 10 },
                new PositionBonus { Position = 2, Points = 6 },
            ],
        };
        Assert.Equal(10, ScoringService.PositionBonusFor(1, rule));
        Assert.Equal(6, ScoringService.PositionBonusFor(2, rule));
        Assert.Equal(0, ScoringService.PositionBonusFor(5, rule)); // non mappata
        Assert.Equal(0, ScoringService.PositionBonusFor(null, rule)); // assente
    }

    // --- BestN ---

    [Fact]
    public void BestN_TakesTopN()
    {
        var totals = new[] { 10, 20, 15, 5, 18 };
        Assert.Equal(20 + 18 + 15, ScoringService.BestN(totals, 3));
    }

    [Fact]
    public void BestN_FewerThanN_SumsAll()
    {
        var totals = new[] { 10, 20, 15 };
        Assert.Equal(45, ScoringService.BestN(totals, 8));
    }

    // --- ComputeBreakdowns: somma componenti == total ---

    [Fact]
    public void Breakdowns_ComponentsSumToTotal()
    {
        var player = Player(1, "Tizio", [9, 3, 7, 10, 9, 9, 9]);
        var breakdowns = ScoringService.ComputeBreakdowns(player.Tournaments, Default);
        Assert.NotEmpty(breakdowns);
        foreach (var b in breakdowns)
            Assert.Equal(b.MatchPoints + b.PositionBonus + b.ScoreBonus + b.ParticipationPoints, b.Total);
    }

    [Fact]
    public void Breakdowns_ParticipationByChronologicalOrder()
    {
        // mp identici: cambia solo la fascia presenza per indice. Tappa 6 e 7 → +2.
        var player = Player(1, "Tizio", [9, 9, 9, 9, 9, 9, 9]);
        var breakdowns = ScoringService.ComputeBreakdowns(player.Tournaments, Default)
            .OrderBy(b => b.TournamentId).ToList(); // id = 100+number → ordine tappa
        for (int i = 0; i < 5; i++)
            Assert.Equal(1, breakdowns[i].ParticipationPoints);
        Assert.Equal(2, breakdowns[5].ParticipationPoints);
        Assert.Equal(2, breakdowns[6].ParticipationPoints);
    }

    // --- ComputeStandings: best N, ordinamento, tie-break, rank ---

    [Fact]
    public void Standings_CountBestN_LimitsCountedTournaments()
    {
        // Regola "piatta" (nessun bonus) → total == matchPoints.
        var flat = new ScoringRule();
        var player = new PlayerScoreData(1, "A",
        [
            new TournamentScore(1, "t1", null, 1, 25, null),
            new TournamentScore(2, "t2", null, 2, 5, null),
            new TournamentScore(3, "t3", null, 3, 5, null),
        ]);

        var best1 = ScoringService.ComputeStandings([player], flat, 1)[0];
        Assert.Equal(25, best1.TotalPoints);
        Assert.Equal(1, best1.TournamentsCountedForTotal);
        Assert.Equal(3, best1.TournamentsPlayed);

        var all = ScoringService.ComputeStandings([player], flat, 8)[0];
        Assert.Equal(35, all.TotalPoints);
        Assert.Equal(3, all.TournamentsCountedForTotal);
    }

    [Fact]
    public void Standings_OrdersByTotalThenAbsoluteThenName()
    {
        var flat = new ScoringRule();
        var alice = new PlayerScoreData(1, "Alice",
            [new TournamentScore(1, "t", null, 1, 20, null), new TournamentScore(2, "t", null, 2, 10, null), new TournamentScore(3, "t", null, 3, 5, null)]); // best2=30 abs35
        var bob = new PlayerScoreData(2, "Bob",
            [new TournamentScore(4, "t", null, 1, 20, null), new TournamentScore(5, "t", null, 2, 10, null), new TournamentScore(6, "t", null, 3, 8, null)]); // best2=30 abs38

        var standings = ScoringService.ComputeStandings([alice, bob], flat, 2);
        Assert.Equal("Bob", standings[0].DisplayName);   // abs desc
        Assert.Equal("Alice", standings[1].DisplayName);
    }

    [Fact]
    public void Standings_TiedPlayersShareRank_NameTiebreak()
    {
        var flat = new ScoringRule();
        var anna = new PlayerScoreData(1, "Anna", [new TournamentScore(1, "t", null, 1, 20, null)]);
        var zara = new PlayerScoreData(2, "Zara", [new TournamentScore(2, "t", null, 1, 20, null)]);
        var carl = new PlayerScoreData(3, "Carl", [new TournamentScore(3, "t", null, 1, 10, null)]);

        var standings = ScoringService.ComputeStandings([zara, carl, anna], flat, 8);
        Assert.Equal("Anna", standings[0].DisplayName);
        Assert.Equal(1, standings[0].Rank);
        Assert.Equal("Zara", standings[1].DisplayName);
        Assert.Equal(1, standings[1].Rank); // pari merito
        Assert.Equal("Carl", standings[2].DisplayName);
        Assert.Equal(3, standings[2].Rank);
    }

    // --- Dati reali workbook (matchPoints per tappa) con la ScoringRule di default ---
    // Verifica che la pipeline (soglie + fasce + best N) riproduca la Classifica storica.
    private static readonly (string Name, int?[] MatchPoints)[] Workbook =
    [
        ("Bruno Barbieri",          [null,  9,  6,  9,  6, null, null]),
        ("Daniel Gemignani",        [null, null,  3, null, null,  6,  3]),
        ("Daniele Gambini",         [null,  9, null, null, null, null, null]),
        ("Dario Tommasi",           [null,  6,  9,  6, null, null,  6]),
        ("Gabriele Marraccini",     [ 9,  3,  7, 10,  9,  9,  9]),
        ("Gianmarco Bina",          [null,  4, null, null, null, null, null]),
        ("Gianmarco Del Bucchia",   [ 7,  0, 12,  6,  6,  9,  4]),
        ("Gianmarco Venturini",     [null, null,  3, null, null, null, null]),
        ("Gianmarco Volpe",         [null, null,  6,  6,  3,  4, null]),
        ("Gioca Turo",              [null, 12, null, null, null, null, null]),
        ("Giulio Bertozzi",         [ 0, null,  0, null, null, null, null]),
        ("Igor Fustini",            [ 9,  4,  6,  9,  6,  3, null]),
        ("Jhonathan Lipparelli",    [ 4,  6, null, null, null, null, null]),
        ("Leonardo Guerra Silicani",[null,  9, null, null, null, null, null]),
        ("Massimiliano Lombardi",   [null, null, null, null, null,  1, null]),
        ("Michele Pardini",         [null, null,  6,  6,  6, null, null]),
        ("Nicola Dalle Mura",       [null,  6,  9,  3, 12,  6,  9]),
        ("Nicola Pardini",          [null,  3,  3,  9, null,  9,  6]),
        ("Paolo Baroni",            [ 6,  6,  7,  4,  6,  9,  6]),
        ("Roberto Randazzo",        [null,  6,  6,  3,  3, null,  0]),
        ("Stefano Ghiara",          [null, null, null, null, null, null,  7]),
        ("Tommaso Duccini",         [null, null, null, null, null,  6,  9]),
    ];

    [Fact]
    public void Standings_RealWorkbookData_ReproducesClassifica()
    {
        var players = Workbook.Select((p, i) => Player(i + 1, p.Name, p.MatchPoints)).ToList();
        var standings = ScoringService.ComputeStandings(players, Default, countBestN: 8);

        Assert.Equal("Gabriele Marraccini", standings[0].DisplayName);
        Assert.Equal(89, standings[0].TotalPoints);
        Assert.Equal(1, standings[0].Rank);

        Assert.Equal("Nicola Dalle Mura", standings[1].DisplayName);
        Assert.Equal(70, standings[1].TotalPoints);

        Assert.Equal("Gianmarco Del Bucchia", standings[2].DisplayName);
        Assert.Equal(69, standings[2].TotalPoints);

        Assert.Equal("Paolo Baroni", standings[3].DisplayName);
        Assert.Equal(63, standings[3].TotalPoints);

        Assert.Equal("Igor Fustini", standings[4].DisplayName);
        Assert.Equal(54, standings[4].TotalPoints);
    }

    [Fact]
    public void Standings_RealData_CountBestN3_ReordersTop()
    {
        var players = Workbook.Select((p, i) => Player(i + 1, p.Name, p.MatchPoints)).ToList();
        var standings = ScoringService.ComputeStandings(players, Default, countBestN: 3);
        // Nicola Dalle Mura best3 = 21+15+14 = 50 supera Marraccini best3 = 17+15+15 = 47.
        Assert.Equal("Nicola Dalle Mura", standings[0].DisplayName);
        Assert.Equal(50, standings[0].TotalPoints);
        Assert.Equal("Gabriele Marraccini", standings[1].DisplayName);
        Assert.Equal(47, standings[1].TotalPoints);
    }

    // --- Validazione ScoringRule ---

    [Fact]
    public void Validate_Default_IsValid() => Assert.Null(Default.Validate());

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    public void Validate_NegativePoints_Fails(int w, int d, int l) =>
        Assert.NotNull(new ScoringRule { PointsPerWin = w, PointsPerDraw = d, PointsPerLoss = l }.Validate());

    [Fact]
    public void Validate_ScoreBonusesUnordered_Fails()
    {
        var rule = new ScoringRule
        {
            ScoreBonuses =
            [
                new ScoreBonus { FromMatchPoints = 10, Points = 5 },
                new ScoreBonus { FromMatchPoints = 6, Points = 1 },
            ],
        };
        Assert.NotNull(rule.Validate());
    }

    [Fact]
    public void Validate_DuplicatePosition_Fails()
    {
        var rule = new ScoringRule
        {
            PositionBonuses =
            [
                new PositionBonus { Position = 1, Points = 10 },
                new PositionBonus { Position = 1, Points = 6 },
            ],
        };
        Assert.NotNull(rule.Validate());
    }
}
