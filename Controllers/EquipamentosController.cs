using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Data;
using SerenoAPI.Models;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/equipamentos")]
[Authorize]
public class EquipamentosController : ApiControllerBase
{
    private readonly AppDbContext _context;

    public EquipamentosController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Busca um equipamento pelo ID (com o statusAtual).
    /// </summary>
    /// <response code="200">Equipamento encontrado.</response>
    /// <response code="404">Equipamento não encontrado.</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<Equipamento>> GetEquipamentoById(int id)
    {
        var equipamento = await _context.Equipamentos.FindAsync(id);

        return equipamento is null ? NaoEncontrado("Equipamento não encontrado.") : Ok(equipamento);
    }

    /// <summary>
    /// Cadastra um equipamento numa estação (somente GESTOR). Começa como FUNCIONANDO.
    /// </summary>
    /// <response code="201">Equipamento criado.</response>
    /// <response code="400">Dados inválidos.</response>
    /// <response code="403">Somente GESTOR.</response>
    /// <response code="422">A estação informada não existe.</response>
    [HttpPost]
    [Authorize(Policy = "Gestor")]
    public async Task<ActionResult<Equipamento>> PostEquipamento(EquipamentoRequest request)
    {
        if (await _context.Estacoes.FindAsync(request.EstacaoId) is null)
        {
            return Erro(StatusCodes.Status422UnprocessableEntity, "REFERENCIA_INVALIDA", "A estação informada não existe.");
        }

        var equipamento = new Equipamento
        {
            EstacaoId = request.EstacaoId,
            Tipo = request.Tipo!.Value,
            Localizacao = request.Localizacao.Trim(),
            StatusAtual = StatusEquipamento.FUNCIONANDO,
            AtualizadoEm = DateTime.UtcNow
        };

        _context.Equipamentos.Add(equipamento);

        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetEquipamentoById), new { id = equipamento.Id }, equipamento);
    }

    /// <summary>
    /// Atualiza um equipamento (somente GESTOR). O status muda pelos reportes.
    /// </summary>
    /// <response code="200">Equipamento atualizado.</response>
    /// <response code="404">Equipamento não encontrado.</response>
    /// <response code="422">A estação informada não existe.</response>
    [HttpPut("{id}")]
    [Authorize(Policy = "Gestor")]
    public async Task<ActionResult<Equipamento>> PutEquipamento(int id, EquipamentoRequest request)
    {
        var equipamento = await _context.Equipamentos.FindAsync(id);

        if (equipamento is null)
        {
            return NaoEncontrado("Equipamento não encontrado.");
        }

        if (await _context.Estacoes.FindAsync(request.EstacaoId) is null)
        {
            return Erro(StatusCodes.Status422UnprocessableEntity, "REFERENCIA_INVALIDA", "A estação informada não existe.");
        }

        equipamento.EstacaoId = request.EstacaoId;
        equipamento.Tipo = request.Tipo!.Value;
        equipamento.Localizacao = request.Localizacao.Trim();

        await _context.SaveChangesAsync();

        return Ok(equipamento);
    }

    /// <summary>
    /// Remove um equipamento e os reportes dele (somente GESTOR).
    /// </summary>
    /// <response code="204">Equipamento removido.</response>
    /// <response code="404">Equipamento não encontrado.</response>
    [HttpDelete("{id}")]
    [Authorize(Policy = "Gestor")]
    public async Task<IActionResult> DeleteEquipamento(int id)
    {
        var equipamento = await _context.Equipamentos.FindAsync(id);

        if (equipamento is null)
        {
            return NaoEncontrado("Equipamento não encontrado.");
        }

        _context.Equipamentos.Remove(equipamento);

        await _context.SaveChangesAsync();

        return NoContent();
    }
}
