namespace ClassificaLega.Domain.Entities;

public static class UserRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string LeagueAdmin = "LeagueAdmin";
}

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    // BCrypt hash della password.
    public string PasswordHash { get; set; } = string.Empty;
    // UserRoles.SuperAdmin (globale, LeagueId null) oppure UserRoles.LeagueAdmin (legato a una lega).
    public string Role { get; set; } = UserRoles.LeagueAdmin;
    // null per i super-admin; valorizzato per gli admin di lega.
    public int? LeagueId { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public League? League { get; set; }
}
