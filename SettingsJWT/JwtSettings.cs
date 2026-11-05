namespace SerenoAPI.Settings;

/// <summary>
/// Configuração do JWT (o segredo vem da variável JWT_SECRET).
/// </summary>
public class JwtSettings
{
    public string Issuer { get; set; } = "sereno-auth";
    public string Audience { get; set; } = "sereno-app";
    public int ExpiracaoHoras { get; set; } = 2;
}
