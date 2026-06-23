namespace ClassificaLega.Domain.Entities;

public class Result
{
    public int Id { get; set; }
    public int StageId { get; set; }
    public int PlayerId { get; set; }

    // W/D/L opzionali (digitati a mano); MatchPoints è sempre persistito (derivato da W/D/L se
    // presenti, altrimenti diretto da import/inserimento). Position = piazzamento finale (opz., da PDF).
    public int? Wins { get; set; }
    public int? Draws { get; set; }
    public int? Losses { get; set; }
    public int? Position { get; set; }

    public int MatchPoints { get; set; }
    public int ScoreBonus { get; set; }          // bonus a soglia sui match points (era BonusRisultato)
    public int PositionBonus { get; set; }        // bonus piazzamento
    public int ParticipationPoints { get; set; }  // punti fascia presenza (era BonusPartecipazione)
    public int TotalPoints { get; set; }
    public int? WinPctOpp { get; set; }
    public int? GameWinPct { get; set; }
    public int? GameWinPctOpp { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Stage Stage { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
