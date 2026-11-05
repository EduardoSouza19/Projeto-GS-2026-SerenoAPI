using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Models;
using SerenoAPI.Services;

namespace SerenoAPI.Controllers;

[ApiController]
[Route("api/cep")]
[AllowAnonymous]
public class CepController : ApiControllerBase
{
    private readonly ViaCepService _viaCepService;

    public CepController(ViaCepService viaCepService)
    {
        _viaCepService = viaCepService;
    }

    /// <summary>
    /// Consulta um CEP no ViaCEP (preenche o endereço sem precisar de coordenadas).
    /// </summary>
    /// <param name="cep">CEP com ou sem traço. Ex.: 01001-000</param>
    /// <response code="200">Endereço encontrado.</response>
    /// <response code="400">CEP inválido.</response>
    /// <response code="404">CEP não encontrado.</response>
    /// <response code="503">ViaCEP fora do ar.</response>
    [HttpGet("{cep}")]
    public async Task<ActionResult<ViaCepResponse>> GetEndereco(string cep)
    {
        if (cep.Count(char.IsDigit) != 8)
        {
            return ErroValidacao("cep", "deve ter 8 dígitos");
        }

        try
        {
            var endereco = await _viaCepService.BuscarAsync(cep);

            return endereco is null ? NaoEncontrado("CEP não encontrado.") : Ok(endereco);
        }
        catch (Exception)
        {
            return Erro(StatusCodes.Status503ServiceUnavailable, "SERVICO_INDISPONIVEL", "Serviço ViaCEP indisponível no momento.");
        }
    }
}
