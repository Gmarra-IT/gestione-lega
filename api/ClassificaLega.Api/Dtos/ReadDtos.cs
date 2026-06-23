using ClassificaLega.Domain.Services;

namespace ClassificaLega.Api.Dtos;

public record SeasonDto(int Id, string Name, int TotalStages, int CountingStages, bool IsActive, ScoringRule ScoringRule);

/// <summary>Composizione del punteggio di un torneo contato (somma componenti == Total).</summary>
public record TournamentBreakdownDto(
    int TournamentId,
    string TournamentName,
    DateOnly? Date,
    int MatchPoints,
    int PositionBonus,
    int ScoreBonus,
    int ParticipationPoints,
    int Total);

public record StandingRowDto(
    int Rank,
    int PlayerId,
    string DisplayName,
    int TotalPoints,
    int AbsoluteTotal,
    int TournamentsPlayed,
    int TournamentsCountedForTotal,
    IReadOnlyList<TournamentBreakdownDto> BestResults);

/// <summary>Voce minima giocatore per il picker (id + nome, senza punteggi).</summary>
public record PlayerLiteDto(int Id, string DisplayName);

/// <summary>Pagina di giocatori filtrata: items della pagina corrente + totale per il "carica altri".</summary>
public record PlayerPageDto(IReadOnlyList<PlayerLiteDto> Items, int Total);

public record StageDto(int Id, int Number, string? Name, DateOnly? Date, string? EventLinkId, int ResultCount);

public record StageResultDto(
    int Id,
    int PlayerId,
    string DisplayName,
    int? Wins,
    int? Draws,
    int? Losses,
    int? Position,
    int MatchPoints,
    int ScoreBonus,
    int PositionBonus,
    int ParticipationPoints,
    int TotalPoints);

public record ProgressionPointDto(int StageNumber, int? StageTotal, int Cumulative);

public record ProgressionDto(int PlayerId, string DisplayName, IReadOnlyList<ProgressionPointDto> Points);

public record MatrixCellDto(int StageNumber, int? TotalPoints);

public record MatrixRowDto(
    int Rank,
    int PlayerId,
    string DisplayName,
    IReadOnlyList<MatrixCellDto> Cells,
    int TotalPoints,
    int AbsoluteTotal);

public record MatrixDto(
    int TotalStages,
    int CountingStages,
    IReadOnlyList<int> StageNumbers,
    IReadOnlyList<MatrixRowDto> Rows);
