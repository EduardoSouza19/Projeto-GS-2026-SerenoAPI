using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SerenoAPI.Data;
using SerenoAPI.Models;
using SerenoAPI.Repositories;
using SerenoAPI.Services;
using SerenoAPI.Telemetry;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesController : ApiControllerBase
{
    private readonly AppDbContext _context;
    private readonly StatusService _statusService;
    private readonly IdempotencyService _idempotencyService;
    private readonly MongoHistoricoRepository _historico;

    public ReportesController(AppDbContext context, StatusService statusService,
        IdempotencyService idempotencyService, MongoHistoricoRepository historico)
    {
        _context = context;
        _statusService = statusService;
        _idempotencyService = idempotencyService;
        _historico = historico;
    }

    /// <summary>
    /// Reporta o status de um equipamento (elevador parado, escada em manutenção...).
    /// </summary>
    /// <remarks>
    /// O header <b>Idempotency-Key</b> é obrigatório (ex.: um UUID gerado pelo app).
    /// Reenviar com a mesma chave e o mesmo corpo devolve a resposta original, sem duplicar.
    /// Mesma chave com corpo diferente = 409. Limite: 10 reportes por minuto por usuário (429).
    /// </remarks>
    /// <param name="request">Equipamento, status e observação.</param>
    /// <param name="idempotencyKey">Chave única da requisição.</param>
    /// <response code="201">Reporte criado e statusAtual do equipamento atualizado.</response>
    /// <response code="400">Dados inválidos ou header Idempotency-Key ausente.</response>
    /// <response code="401">Token não informado ou inválido.</response>
    /// <response code="409">Mesma Idempotency-Key com corpo diferente.</response>
    /// <response code="422">O equipamento informado não existe.</response>
    /// <response code="429">Muitos reportes. Aguarde o tempo do header Retry-After.</response>
    [HttpPost]
    [EnableRateLimiting("reportes")]
    public async Task<IActionResult> PostReporte(ReporteRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
        {
            return ErroValidacao("Idempotency-Key", "header obrigatório (ex.: um UUID), com até 100 caracteres");
        }

        // 1) Idempotência: essa chave já foi usada por este usuário?
        var hash = IdempotencyService.GerarHash(request);
        var registro = await _idempotencyService.BuscarAsync(idempotencyKey, UsuarioId);

        if (registro is not null)
        {
            if (registro.HashDoCorpo != hash)
            {
                return Erro(StatusCodes.Status409Conflict, "CONFLITO_IDEMPOTENCIA",
                    "Esta Idempotency-Key já foi usada com outro conteúdo. Gere uma nova chave.");
            }

            Response.Headers["Idempotency-Replayed"] = "true";
            return new ContentResult
            {
                Content = registro.CorpoDaResposta,
                ContentType = "application/json",
                StatusCode = registro.Status
            };
        }

        // 2) Regra: o equipamento precisa existir
        if (await _context.Equipamentos.FindAsync(request.EquipamentoId) is null)
        {
            return Erro(StatusCodes.Status422UnprocessableEntity, "REFERENCIA_INVALIDA", "O equipamento informado não existe.");
        }

        // 3) Cria o reporte e atualiza o statusAtual do equipamento
        var reporte = new Reporte
        {
            EquipamentoId = request.EquipamentoId,
            UsuarioId = UsuarioId,
            Status = request.Status!.Value,
            Observacao = request.Observacao?.Trim(),
            CriadoEm = DateTime.UtcNow
        };

        _context.Reportes.Add(reporte);
        await _context.SaveChangesAsync();

        await _statusService.AtualizarStatusAtualAsync(reporte.EquipamentoId);
        await _idempotencyService.SalvarAsync(idempotencyKey, UsuarioId, hash, StatusCodes.Status201Created, reporte);
        await _historico.RegistrarAsync(reporte, "CRIADO");

        AppMetrics.ReportesCriados.Add(1);

        return CreatedAtAction(nameof(GetReporteById), new { id = reporte.Id }, reporte);
    }

    /// <summary>
    /// Meus reportes (paginado), do mais recente para o mais antigo.
    /// </summary>
    /// <param name="page">Página, começando em 0.</param>
    /// <param name="size">Itens por página (1 a 50).</param>
    /// <response code="200">Página de reportes.</response>
    [HttpGet("meus")]
    public async Task<ActionResult<PaginaResponse<Reporte>>> GetMeusReportes(int page = 0, int size = 10)
    {
        (page, size) = AjustarPaginacao(page, size);

        var consulta = _context.Reportes.Where(r => r.UsuarioId == UsuarioId);

        return Ok(new PaginaResponse<Reporte>
        {
            Items = await consulta
                .OrderByDescending(r => r.CriadoEm)
                .Skip(page * size)
                .Take(size)
                .ToListAsync(),
            Page = page,
            Size = size,
            TotalItems = await consulta.CountAsync()
        });
    }

    /// <summary>
    /// Busca um reporte pelo ID.
    /// </summary>
    /// <response code="200">Reporte encontrado.</response>
    /// <response code="404">Reporte não encontrado.</response>
    [HttpGet("{id}")]
    public async Task<ActionResult<Reporte>> GetReporteById(int id)
    {
        var reporte = await _context.Reportes.FindAsync(id);

        return reporte is null ? NaoEncontrado("Reporte não encontrado.") : Ok(reporte);
    }

    /// <summary>
    /// Edita um reporte (somente quem criou ou um GESTOR).
    /// </summary>
    /// <response code="200">Reporte atualizado.</response>
    /// <response code="403">Você não é o dono do reporte.</response>
    /// <response code="404">Reporte não encontrado.</response>
    [HttpPut("{id}")]
    public async Task<ActionResult<Reporte>> PutReporte(int id, ReporteUpdateRequest request)
    {
        var reporte = await _context.Reportes.FindAsync(id);

        if (reporte is null)
        {
            return NaoEncontrado("Reporte não encontrado.");
        }

        if (reporte.UsuarioId != UsuarioId && !EhGestor)
        {
            return AcessoNegado("Só quem criou o reporte (ou um GESTOR) pode editar.");
        }

        reporte.Status = request.Status!.Value;
        reporte.Observacao = request.Observacao?.Trim();

        await _context.SaveChangesAsync();
        await _statusService.AtualizarStatusAtualAsync(reporte.EquipamentoId);
        await _historico.RegistrarAsync(reporte, "EDITADO");

        return Ok(reporte);
    }

    /// <summary>
    /// Exclui um reporte (somente quem criou ou um GESTOR). O statusAtual é recalculado.
    /// </summary>
    /// <response code="204">Reporte excluído.</response>
    /// <response code="403">Você não é o dono do reporte.</response>
    /// <response code="404">Reporte não encontrado.</response>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteReporte(int id)
    {
        var reporte = await _context.Reportes.FindAsync(id);

        if (reporte is null)
        {
            return NaoEncontrado("Reporte não encontrado.");
        }

        if (reporte.UsuarioId != UsuarioId && !EhGestor)
        {
            return AcessoNegado("Só quem criou o reporte (ou um GESTOR) pode excluir.");
        }

        _context.Reportes.Remove(reporte);

        await _context.SaveChangesAsync();
        await _statusService.AtualizarStatusAtualAsync(reporte.EquipamentoId);
        await _historico.RegistrarAsync(reporte, "EXCLUIDO");

        return NoContent();
    }
}
