using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Models;
using SerenoAPI.Services;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    // Usuários de teste (mesmos e-mails do Contrato do grupo). Nenhuma senha fica no código.
    private static readonly Dictionary<string, (int Id, string Nome, string Papel)> UsuariosTeste = new()
    {
        ["cidadao@sereno.app"] = (1, "Cidadão de Teste", "CIDADAO"),
        ["gestor@sereno.app"] = (2, "Gestor de Teste", "GESTOR")
    };

    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(TokenService tokenService, IConfiguration configuration, IWebHostEnvironment environment)
    {
        _tokenService = tokenService;
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>
    /// Gera o token JWT de teste.
    /// </summary>
    /// <remarks>
    /// Use <b>cidadao@sereno.app</b> ou <b>gestor@sereno.app</b>. Só funciona em Development
    /// ou com a variável AuthDev__Habilitado=true (o script da Azure já liga).
    /// </remarks>
    /// <response code="200">Token gerado. Copie e cole no botão Authorize.</response>
    /// <response code="404">Desligado em produção ou e-mail não é de teste.</response>
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErroResponse), StatusCodes.Status404NotFound)]
    [HttpPost("token")]
    [AllowAnonymous]
    public IActionResult GerarTokenDesenvolvimento([FromBody] TokenRequest request)
    {
        var habilitado = _environment.IsDevelopment() || _configuration.GetValue("AuthDev:Habilitado", false);

        if (!habilitado)
        {
            return NaoEncontrado("Geração de token desligada (AuthDev__Habilitado=false).");
        }

        if (!UsuariosTeste.TryGetValue(request.Email.Trim().ToLower(), out var usuario))
        {
            return NaoEncontrado("Use cidadao@sereno.app ou gestor@sereno.app.");
        }

        var (token, expiraEm) = _tokenService.GerarToken(usuario.Id, request.Email.Trim().ToLower(), usuario.Papel);

        return Ok(new TokenResponse
        {
            Token = token,
            ExpiraEm = expiraEm,
            Usuario = new UsuarioTokenResponse
            {
                Id = usuario.Id,
                Nome = usuario.Nome,
                Email = request.Email.Trim().ToLower(),
                Papel = usuario.Papel
            }
        });
    }
}
