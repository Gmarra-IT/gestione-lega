namespace ClassificaLega.Api.Dtos;

// Vista pubblica della lega (picker/selettore).
public record LeagueDto(int Id, string Slug, string Name, string? Title, bool IsActive);

public record CreateLeagueRequest(string Slug, string Name, string? Title);

public record UpdateLeagueRequest(string? Name, string? Title, bool? IsActive);

public record CreateLeagueAdminRequest(string Username, string Password);

public record LeagueAdminDto(int Id, string Username);
