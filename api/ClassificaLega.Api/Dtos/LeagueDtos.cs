namespace ClassificaLega.Api.Dtos;

// Vista pubblica della lega (picker/selettore). HasLogo: il client sa se mostrare <img> o l'avatar.
public record LeagueDto(int Id, string Slug, string Name, string? Title, bool IsActive, bool HasLogo);

// Byte del logo per lo streaming dall'endpoint pubblico.
public record LeagueLogoData(byte[] Bytes, string ContentType, string ETag);

public record CreateLeagueRequest(string Slug, string Name, string? Title);

public record UpdateLeagueRequest(string? Name, string? Title, bool? IsActive);

public record CreateLeagueAdminRequest(string Username, string Password);

// Modifica admin esistente: campi opzionali (username e/o reset password).
public record UpdateLeagueAdminRequest(string? Username, string? Password);

public record LeagueAdminDto(int Id, string Username);
