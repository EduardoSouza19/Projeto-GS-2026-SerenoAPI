using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerenoAPI.Data;
using SerenoAPI.Models;
using SerenoAPI.Services;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/rotas")]
[Authorize]
public class RotasController : ApiControllerBase
{
    private readonly AppDbContext _context;

    public RotasController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Confere se a viagem é acessível: basta digitar o NOME da estação de origem e de destino.
    /// </summary>
    /// <remarks>
    /// Sem latitude e longitude. Exemplo: origem=Sé e destino=Paulista.
    /// Cada estação recebe ACESSIVEL, PARCIAL, INACESSIVEL ou SEM_INFORMACAO.
    /// </remarks>
    /// <param name="origem">Nome (ou parte do nome) da estação onde a pessoa embarca.</param>
    /// <param name="destino">Nome (ou parte do nome) da estação onde a pessoa desembarca.</param>
    /// <response code="200">Resultado da viagem.</response>
    /// <response code="400">Origem ou destino não informados.</response>
    /// <response code="404">Estação não encontrada.</response>
    [HttpGet("acessibilidade")]
    public async Task<ActionResult<RotaAcessivelResponse>> VerificarRota(string? origem, string? destino)
    {
        if (string.IsNullOrWhiteSpace(origem))
        {
            return ErroValidacao("origem", "informe o nome da estação de origem");
        }

        if (string.IsNullOrWhiteSpace(destino))
        {
            return ErroValidacao("destino", "informe o nome da estação de destino");
        }

        var estacaoOrigem = await BuscarPorNomeAsync(origem);
        var estacaoDestino = await BuscarPorNomeAsync(destino);

        if (estacaoOrigem is null)
        {
            return NaoEncontrado($"Estação \"{origem}\" não encontrada. Confira o nome em GET /api/estacoes?busca=");
        }

        if (estacaoDestino is null)
        {
            return NaoEncontrado($"Estação \"{destino}\" não encontrada. Confira o nome em GET /api/estacoes?busca=");
        }

        var resumoOrigem = StatusService.ResumoEstacao(estacaoOrigem);
        var resumoDestino = StatusService.ResumoEstacao(estacaoDestino);
        var classificacoes = new[] { resumoOrigem.Classificacao, resumoDestino.Classificacao };

        var (resultado, mensagem) =
            classificacoes.Contains("INACESSIVEL") ? ("INACESSIVEL", "Viagem não recomendada agora: há estação sem equipamento funcionando.")
            : classificacoes.All(c => c == "ACESSIVEL") ? ("ACESSIVEL", "Pode ir tranquilo: os equipamentos das duas estações estão funcionando.")
            : ("PARCIAL", "Atenção: há equipamento parado. Veja os avisos antes de sair.");

        return Ok(new RotaAcessivelResponse
        {
            Origem = resumoOrigem,
            Destino = resumoDestino,
            Resultado = resultado,
            Mensagem = mensagem
        });
    }

    /// <summary>
    /// Procura a estação pelo nome, sem diferenciar maiúscula/minúscula. Nome exato tem preferência.
    /// </summary>
    private async Task<Estacao?> BuscarPorNomeAsync(string nome)
    {
        var termo = nome.Trim().ToUpper();

        var encontradas = await _context.Estacoes
            .Include(e => e.Equipamentos)
            .AsNoTracking()
            .Where(e => e.Nome.ToUpper().Contains(termo))
            .ToListAsync();

        return encontradas.FirstOrDefault(e => e.Nome.ToUpper() == termo) ?? encontradas.FirstOrDefault();
    }
}
