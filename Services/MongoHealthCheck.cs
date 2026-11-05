using Microsoft.Extensions.Diagnostics.HealthChecks;
using SerenoAPI.Repositories;

namespace SerenoAPI.Services;

/// <summary>
/// Verifica se o MongoDB responde a um "ping".
/// </summary>
public class MongoHealthCheck : IHealthCheck
{
    private readonly MongoHistoricoRepository _repository;

    public MongoHealthCheck(MongoHistoricoRepository repository)
    {
        _repository = repository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_repository.Configurado)
        {
            return HealthCheckResult.Unhealthy("MongoDbSettings:ConnectionString não configurada.");
        }

        try
        {
            await _repository.PingAsync();
            return HealthCheckResult.Healthy("MongoDB respondendo.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("MongoDB indisponível.", ex);
        }
    }
}
