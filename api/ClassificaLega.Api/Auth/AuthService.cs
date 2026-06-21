using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClassificaLega.Api.Dtos;
using ClassificaLega.Api.Tenancy;
using ClassificaLega.Domain.Entities;
using ClassificaLega.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClassificaLega.Api.Auth;

public class AuthService(AppDbContext db, IOptions<JwtOptions> jwt, LeagueContext league)
{
    private readonly JwtOptions _jwt = jwt.Value;

    public async Task<LoginResponse?> LoginAsync(LoginRequest req)
    {
        var candidates = await db.Users.AsNoTracking()
            .Where(u => u.Username == req.Username)
            .ToListAsync();
        if (candidates.Count == 0) return null;

        // Preferisci l'admin della lega corrente; altrimenti un super-admin globale.
        User? user = null;
        if (league.Current is { } lg)
            user = candidates.FirstOrDefault(u => u.LeagueId == lg.Id);
        user ??= candidates.FirstOrDefault(u => u.Role == UserRoles.SuperAdmin && u.LeagueId == null);
        if (user is null) return null;

        if (string.IsNullOrEmpty(user.PasswordHash) ||
            !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return null;

        return BuildToken(user);
    }

    private LoginResponse BuildToken(User user)
    {
        var expires = DateTimeOffset.UtcNow.AddHours(_jwt.ExpiryHours);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Username),
            new(ClaimTypes.Role, user.Role),
        };
        if (user.LeagueId is { } leagueId)
            claims.Add(new Claim("leagueId", leagueId.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        var jwtString = new JwtSecurityTokenHandler().WriteToken(token);
        return new LoginResponse(jwtString, expires);
    }
}
