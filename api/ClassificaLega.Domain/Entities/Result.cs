namespace ClassificaLega.Domain.Entities;

public class Result
{
    public int Id { get; set; }
    public int StageId { get; set; }
    public int PlayerId { get; set; }
    public int MatchPoints { get; set; }
    public int BonusRisultato { get; set; }
    public int BonusPartecipazione { get; set; }
    public int TotalPoints { get; set; }
    public int? WinPctOpp { get; set; }
    public int? GameWinPct { get; set; }
    public int? GameWinPctOpp { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Stage Stage { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
