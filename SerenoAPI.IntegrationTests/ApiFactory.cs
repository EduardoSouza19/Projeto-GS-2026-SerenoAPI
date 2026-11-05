using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using SerenoAPI.Data;

namespace SerenoAPI.IntegrationTests;

public class ApiFactory : WebApplicationFactory<Program>
{
    public const string SegredoTeste = "SegredoDeTesteComMaisDe32CaracteresParaHS256!";

    private static int _proximoUsuario = 1000;

    static ApiFactory()
    {
        Environment.SetEnvironmentVariable("JWT_SECRET", SegredoTeste);

        // Limite geral alto para não atrapalhar; o de reportes continua 10/min (testado no 429)
        Environment.SetEnvironmentVariable("RateLimit__GeralPorMinuto", "10000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("SerenoAPIIntegrationTests"));
        });
    }

    /// <summary>
    /// Gera um token (HS256, iss=sereno-auth, aud=sereno-app, sub, email, role).
    /// Cada chamada usa um usuário novo, para um teste não interferir no rate limit do outro.
    /// </summary>
    public static string GerarToken(string papel = "CIDADAO", int? usuarioId = null)
    {
        var id = usuarioId ?? Interlocked.Increment(ref _proximoUsuario);

        var token = new JwtSecurityToken(
            issuer: "sereno-auth",
            audience: "sereno-app",
            claims: new[]
            {
                new Claim("sub", id.ToString()),
                new Claim("email", $"usuario{id}@sereno.app"),
                new Claim("role", papel)
            },
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SegredoTeste)), SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public HttpClient CreateClientComToken(string papel = "CIDADAO", int? usuarioId = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", GerarToken(papel, usuarioId));
        return client;
    }
}
