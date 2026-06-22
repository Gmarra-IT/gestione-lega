namespace ClassificaLega.Api.Dtos;

public record SeasonDto(int Id, string Name, int TotalStages, int CountingStages, bool IsActive);

public record StandingRowDto(int Position, int PlayerId, string DisplayName, int BestN, int TotalPoints);

/// <summary>Voce minima giocatore per il picker (id + nome, senza punteggi).</summary>
public record PlayerLiteDto(int Id, string DisplayName);

/// <summary>Pagina di giocatori filtrata: items della pagina corrente + totale per il "carica altri".</summary>
public record PlayerPageDto(IReadOnlyList<PlayerLiteDto> Items, int Total);

public record StageDto(int Id, int Number, string? Name, DateOnly? Date, string? EventLinkId, int ResultCount);

public record StageResultDto(
    int Id,
    int PlayerId,
    string DisplayName,
    int MatchPoints,
    int BonusRisultato,
    int BonusPartecipazione,
    int TotalPoints);

public record ProgressionPointDto(int StageNumber, int? StageTotal, int Cumulative);

public record ProgressionDto(int PlayerId, string DisplayName, IReadOnlyList<ProgressionPointDto> Points);

public record MatrixCellDto(int StageNumber, int? TotalPoints);

public record MatrixRowDto(
    int Position,
    int PlayerId,
    string DisplayName,
    IReadOnlyList<MatrixCellDto> Cells,
    int BestN,
    int TotalPoints);

public record MatrixDto(
    int TotalStages,
    int CountingStages,
    IReadOnlyList<int> StageNumbers,
    IReadOnlyList<MatrixRowDto> Rows);
