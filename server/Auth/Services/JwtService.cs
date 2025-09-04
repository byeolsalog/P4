using System.IdentityModel.Tokens.Jwt;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using Auth.Models;
using Microsoft.Extensions.Configuration;

namespace Auth.Services;
public sealed class JwtService
{
    private readonly string _key, _issuer, _audience;
    private readonly int _accessMinutes;

    public JwtService(IConfiguration cfg)
    {
        _key = cfg["Jwt:Key"] ?? throw new Exception("Jwt:Key missing");
        _issuer = cfg["Jwt:Issuer"] ?? "p4";
        _audience = cfg["Jwt:Audience"] ?? "p4-clients";
        _accessMinutes = cfg.GetValue("Jwt:AccessMinutes", 15);
    }

    public string CreateAccessToken(UserRecord user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim("provider", user.Provider),
            new Claim("psub", user.ProviderSub),
            new Claim(JwtRegisteredClaimNames.Iss, _issuer),
            new Claim(JwtRegisteredClaimNames.Aud, _audience),
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(_accessMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}