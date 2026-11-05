using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SerenoAPI.Data;
using SerenoAPI.Models;
using SerenoAPI.Services;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/estacoes")]
[Authorize]
public class EstacoesController : ApiControllerBase
{
    private readonly AppDbContext _context;
    private readonly ViaCepService _viaCepService;

    public EstacoesController(AppDbContext context, ViaCepService viaCepService)
    {
        _context = context;
        _viaCepService = viaCepService;
    }

    /// <summary>
    /// Lista as estações (paginado), com a quantidade de equipamentos parados.
    /// </summary>
    /// <param name="page">Página, começando em 0.</param>
    /// <param name="size">Itens por página (1 a 50).</param>
    /// <param name="linha">Filtro pela linha. Ex.: Azul, 3-Vermelha.</param>
    /// <param name="busca">Busca pelo nome ou endereço. Ex.: Sé.</param>
    /// <response code="200">Página de estações.</response>
    /// <response code="401">Token não informado ou inválido.</response>
    [HttpGet]
    public async Task<ActionResult<PaginaResponse<Estacao>>> GetEstacoes(
        int page = 0, int size = 10, string? linha = null, string? busca = null)
    {
        (page, size) = AjustarPaginacao(page, size);

        var consulta = _context.Estacoes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(linha))
            consulta = consulta.Where(e => e.Linha.ToUpper().Contains(linha.ToUpper()));

        if (!string.IsNullOrWhiteSpace(busca))
            consulta = consulta.Where(e => e.Nome.ToUpper().Contains(busca.ToUpper()) || e.Endereco.ToUpper().Contains(busca.ToUpper()));

        var total = await consulta.CountAsync();

        var estacoes = await consulta
            .OrderBy(e => e.Nome)
            .Skip(page * size)
            .Take(size)
            .Select(e => new
            {
                Estacao = e,
                Parados = e.Equipamentos!.Count(q => q.StatusAtual != StatusEquipamento.FUNCIONANDO)
            })
            .ToListAsync();

        // Na lista não mandamos os equipamentos, só a quantidade de parados
        estacoes.ForEach(x =>
        {
            x.Estacao.EquipamentosParados = x.Parados;
            x.Estacao.Equipamentos = null;
        });

        return Ok(new PaginaResponse<Estacao>
        {
            Items = estacoes.Select(x => x.Estacao).ToList(),
            Page = page,
            Size = size,
            TotalItems = total
        });
    }

    /// <summary>
    /// Busca uma estação com a lista de equipamentos e o statusAtual de cada um.
    /// </summary>
    /// <param name="id">ID da estação.</param>
    /// <response code="200">Estação encontrada.</response>
    /// <response code="404">Estação não encontrada.</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<Estacao>> GetEstacaoById(int id)
    {
        var estacao = await _context.Estacoes
            .Include(e => e.Equipamentos)
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id);

        if (estacao is null)
        {
            return NaoEncontrado("Estação não encontrada.");
        }

        estacao.EquipamentosParados = StatusService.ContarParados(estacao.Equipamentos!);

        return Ok(estacao);
    }

    /// <summary>
    /// Cadastra uma estação (somente GESTOR). Informe o endereço ou só o CEP.
    /// </summary>
    /// <response code="201">Estação criada.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="403">Somente GESTOR.</response>
    [HttpPost]
    [Authorize(Policy = "Gestor")]
    public async Task<ActionResult<Estacao>> PostEstacao(EstacaoRequest request)
    {
        var endereco = await MontarEnderecoAsync(request);

        if (endereco is null)
        {
            return ErroValidacao("endereco", "informe o endereço ou um CEP válido");
        }

        var estacao = new Estacao
        {
            Nome = request.Nome.Trim(),
            Linha = request.Linha.Trim(),
            Endereco = endereco,
            Latitude = request.Latitude,
            Longitude = request.Longitude
        };

        _context.Estacoes.Add(estacao);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEstacaoById), new { id = estacao.Id }, estacao);
    }

    /// <summary>
    /// Atualiza uma estação (somente GESTOR).
    /// </summary>
    /// <response code="200">Estação atualizada.</response>
    /// <response code="404">Estação não encontrada.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "Gestor")]
    public async Task<ActionResult<Estacao>> PutEstacao(int id, EstacaoRequest request)
    {
        var estacao = await _context.Estacoes.FindAsync(id);

        if (estacao is null)
        {
            return NaoEncontrado("Estação não encontrada.");
        }

        var endereco = await MontarEnderecoAsync(request);

        if (endereco is null)
        {
            return ErroValidacao("endereco", "informe o endereço ou um CEP válido");
        }

        estacao.Nome = request.Nome.Trim();
        estacao.Linha = request.Linha.Trim();
        estacao.Endereco = endereco;
        estacao.Latitude = request.Latitude;
        estacao.Longitude = request.Longitude;

        await _context.SaveChangesAsync();

        return Ok(estacao);
    }

    /// <summary>
    /// Remove uma estação e os equipamentos dela (somente GESTOR).
    /// </summary>
    /// <response code="204">Estação removida.</response>
    /// <response code="404">Estação não encontrada.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Gestor")]
    public async Task<IActionResult> DeleteEstacao(int id)
    {
        var estacao = await _context.Estacoes.FindAsync(id);

        if (estacao is null)
        {
            return NaoEncontrado("Estação não encontrada.");
        }

        _context.Estacoes.Remove(estacao);

        await _context.SaveChangesAsync();

        return NoContent();
    }

    /// <summary>
    /// Usa o endereço digitado; se vier vazio, busca pelo CEP no ViaCEP.
    /// </summary>
    private async Task<string?> MontarEnderecoAsync(EstacaoRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Endereco))
        {
            return request.Endereco.Trim();
        }

        if (string.IsNullOrWhiteSpace(request.Cep))
        {
            return null;
        }

        try
        {
            var viaCep = await _viaCepService.BuscarAsync(request.Cep);
            return viaCep is null ? null : ViaCepService.FormatarEndereco(viaCep);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
