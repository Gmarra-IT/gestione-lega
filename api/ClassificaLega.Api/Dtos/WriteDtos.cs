namespace ClassificaLega.Api.Dtos;

public record LoginRequest(string Username, string Password);

public record LoginResponse(string Token, DateTimeOffset ExpiresAt);

public record UpdateSeasonRequest(int TotalStages, int CountingStages);

public record CreateSeasonRequest(string Name, int TotalStages, int CountingStages);

public record UpsertResultRequest(int StageNumber, int? PlayerId, string? NewPlayerName, int MatchPoints);

public record UpdateResultRequest(int MatchPoints);

public record UpsertStageRequest(int Number, string? Name, DateOnly? Date, string? EventLinkId);
