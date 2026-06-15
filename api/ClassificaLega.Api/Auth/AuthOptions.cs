namespace ClassificaLega.Api.Auth;

public class JwtOptions
{
    public string Issuer { get; set; } = "classifica-lega";
    public string Audience { get; set; } = "classifica-lega";
    public string Key { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 12;
}

public class AdminOptions
{
    public string Username { get; set; } = "admin";
    // BCrypt hash of the admin password. Provided via env (Admin__PasswordHash).
    public string PasswordHash { get; set; } = string.Empty;
}
