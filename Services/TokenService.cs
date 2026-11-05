using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SerenoAPI.Services;

/// <summary>
/// Gera o JWT 
/// </summary>
public class TokenService
{
    private readonly IConfiguration _configuration;

    public TokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiraEm) GerarToken(int usuarioId, string email, string papel)
    {
        var segredo = _configuration["JWT_SECRET"]
            ?? throw new InvalidOperationException("Configure a variável JWT_SECRET.");

        var expiraEm = DateTime.UtcNow.AddHours(_configuration.GetValue("Jwt:ExpiracaoHoras", 2));

        var claims = new[]
        {
            new Claim("sub", usuarioId.ToString()),
            new Claim("email", email),
            new Claim("role", papel)
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "sereno-auth",
            audience: _configuration["Jwt:Audience"] ?? "sereno-app",
            claims: claims,
            expires: expiraEm,
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo)),
                SecurityAlgorithms.HmacSha256));

        return (new JwtSecurityTokenHandler().WriteToken(token), expiraEm);
    }
}
