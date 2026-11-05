using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Models;

namespace SerenoAPI.Controllers;

/// <summary>
/// Base dos controllers: devolve erros e lê o usuário do token.
/// </summary>
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>
    /// ID do usuário logado (claim "sub" do token).
    /// </summary>
    protected int UsuarioId => int.TryParse(User.FindFirst("sub")?.Value, out var id) ? id : 0;

    /// <summary>
    /// true quando o papel do token é GESTOR.
    /// </summary>
    protected bool EhGestor => User.IsInRole("GESTOR");

    /// <summary>
    /// Monta a resposta de erro padrão: { status, erro, mensagem, campos, timestamp, path }.
    /// </summary>
    protected ObjectResult Erro(int status, string erro, string mensagem, List<CampoErro>? campos = null) =>
        StatusCode(status, ErroResponse.Criar(status, erro, mensagem, HttpContext.Request.Path, campos));

    protected ObjectResult NaoEncontrado(string mensagem) =>
        Erro(StatusCodes.Status404NotFound, "NAO_ENCONTRADO", mensagem);

    protected ObjectResult AcessoNegado(string mensagem) =>
        Erro(StatusCodes.Status403Forbidden, "ACESSO_NEGADO", mensagem);

    protected ObjectResult ErroValidacao(string campo, string mensagem) =>
        Erro(StatusCodes.Status400BadRequest, "VALIDACAO", "Dados inválidos",
            new List<CampoErro> { new() { Campo = campo, Mensagem = mensagem } });

    /// <summary>
    /// Página começa em 0 e o tamanho vai de 1 a 50.
    /// </summary>
    protected static (int Page, int Size) AjustarPaginacao(int page, int size) =>
        (Math.Max(0, page), Math.Clamp(size, 1, 50));
}
