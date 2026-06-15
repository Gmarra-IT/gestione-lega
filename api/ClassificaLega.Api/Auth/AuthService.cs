using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ClassificaLega.Api.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ClassificaLega.Api.Auth;

public class AuthService(IOptions<JwtOptions> jwt, IOptions<AdminOptions> admin)
{
    private readonly JwtOptions _jwt = jwt.Value;
    private readonly AdminOptions _admin = admin.Value;

    public LoginResponse? Login(LoginRequest req)
    {
        // if (!string.Equals(req.Username, _admin.Username, StringComparison.Ordinal))
        //     return null;
        // if (string.IsNullOrEmpty(_admin.PasswordHash) ||
        //     !BCrypt.Net.BCrypt.Verify(req.Password, _admin.PasswordHash))
        //     return null;

        var expires = DateTimeOffset.UtcNow.AddHours(_jwt.ExpiryHours);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, _admin.Username),
            new Claim(ClaimTypes.Role, "admin"),
        };
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
