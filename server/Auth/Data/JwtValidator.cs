using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using System;

public static class JwtValidator
{
    public static ClaimsPrincipal? Validate(string token, IConfiguration cfg)
    {
        var key     = cfg["Jwt:Key"];
        var issuer  = cfg["Jwt:Issuer"];    // "game-login"
        var audience= cfg["Jwt:Audience"];  // "game-server"

        if (string.IsNullOrWhiteSpace(key)) return null;

        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(
                token,
                new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,
                    ValidateAudience = true,
                    ValidAudience = audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
                },
                out _
            );
            return principal;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.ToString());
            return null;
        }
    }
}