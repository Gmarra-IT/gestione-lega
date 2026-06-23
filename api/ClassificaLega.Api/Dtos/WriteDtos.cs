using ClassificaLega.Domain.Services;

namespace ClassificaLega.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt);

public record UpdateSeasonRequest(int TotalStages, int CountingStages);

public record CreateSeasonRequest(string Name, int TotalStages, int CountingStages);

// Sostituisce l'intera ScoringRule della stagione corrente.
public record UpdateScoringRuleRequest(ScoringRule ScoringRule);

// W/D/L/Position opzionali. Se Wins/Draws/Losses tutti valorizzati → MatchPoints derivato dalla
// regola; altrimenti si usa MatchPoints (richiesto).
public record UpsertResultRequest(
    int StageNumber, int? PlayerId, string? NewPlayerName,
    int? MatchPoints, int? Wins, int? Draws, int? Losses, int? Position);

public record UpdateResultRequest(
    int? MatchPoints, int? Wins, int? Draws, int? Losses, int? Position);

public record UpsertStageRequest(int Number, string? Name, DateOnly? Date, string? EventLinkId);
