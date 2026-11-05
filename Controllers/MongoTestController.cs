using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Repositories;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[AllowAnonymous]
public class MongoTestController : ApiControllerBase
{
    private readonly MongoHistoricoRepository _repository;

    public MongoTestController(MongoHistoricoRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Testa a conexão com o MongoDB (database "sereno").
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestConnection()
    {
        try
        {
            await _repository.PingAsync();
            return Ok(new { message = "Conexão com MongoDB realizada com sucesso!" });
        }
        catch (Exception ex)
        {
            return Erro(StatusCodes.Status503ServiceUnavailable, "SERVICO_INDISPONIVEL", $"MongoDB indisponível: {ex.Message}");
        }
    }
}
