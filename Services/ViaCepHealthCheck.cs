using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace SerenoAPI.Services;

/// <summary>
/// Verifica o serviço externo ViaCEP (usado para preencher o endereço da estação pelo CEP).
/// Se ele cair, a API continua funcionando, então o status fica Degraded, não Unhealthy.
/// </summary>
public class ViaCepHealthCheck : IHealthCheck
{
    private readonly ViaCepService _viaCep;

    public ViaCepHealthCheck(ViaCepService viaCep)
    {
        _viaCep = viaCep;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var endereco = await _viaCep.BuscarAsync("01001000");

            return endereco is null
                ? HealthCheckResult.Degraded("ViaCEP respondeu sem dados.")
                : HealthCheckResult.Healthy("ViaCEP respondendo.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("ViaCEP indisponível (o cadastro por CEP fica sem endereço automático).", ex);
        }
    }
}
