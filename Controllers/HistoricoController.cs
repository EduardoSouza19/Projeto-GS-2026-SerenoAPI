using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Models;
using SerenoAPI.Repositories;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/historico")]
[Authorize(Policy = "Gestor")]
public class HistoricoController : ApiControllerBase
{
    private readonly MongoHistoricoRepository _repository;

    public HistoricoController(MongoHistoricoRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Histórico dos reportes gravado no MongoDB (somente GESTOR). Últimos 100 registros.
    /// </summary>
    /// <param name="equipamentoId">Filtro opcional por equipamento.</param>
    /// <response code="200">Histórico encontrado.</response>
    [HttpGet]
    public async Task<ActionResult<List<HistoricoReporte>>> GetHistorico(int? equipamentoId) =>
        Ok(await _repository.GetAllAsync(equipamentoId));
}
