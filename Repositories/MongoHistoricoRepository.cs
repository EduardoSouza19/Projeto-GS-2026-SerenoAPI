using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using SerenoAPI.Models;
using SerenoAPI.Settings;

namespace SerenoAPI.Repositories;

/// <summary>
/// Histórico dos reportes no MongoDB (database "sereno", collection "historico_reportes").
/// Se o MongoDB não estiver configurado, a API continua funcionando normalmente.
/// </summary>
public class MongoHistoricoRepository
{
    private readonly IMongoCollection<HistoricoReporte>? _historico;
    private readonly IMongoDatabase? _database;
    private readonly ILogger<MongoHistoricoRepository> _logger;

    public bool Configurado => _database is not null;

    public MongoHistoricoRepository(IOptions<MongoDbSettings> options, ILogger<MongoHistoricoRepository> logger)
    {
        _logger = logger;
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            return;
        }

        try
        {
            var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);

            _database = new MongoClient(clientSettings).GetDatabase(settings.DatabaseName);
            _historico = _database.GetCollection<HistoricoReporte>("historico_reportes");
        }
        catch (Exception ex)
        {
            _logger.LogError("Connection string do MongoDB inválida: {Erro}", ex.Message);
        }
    }

    public async Task<List<HistoricoReporte>> GetAllAsync(int? equipamentoId)
    {
        if (_historico is null) return new List<HistoricoReporte>();

        var filtro = equipamentoId is null
            ? Builders<HistoricoReporte>.Filter.Empty
            : Builders<HistoricoReporte>.Filter.Eq(h => h.EquipamentoId, equipamentoId.Value);

        return await _historico.Find(filtro)
            .SortByDescending(h => h.Data)
            .Limit(100)
            .ToListAsync();
    }

    /// <summary>
    /// Registra uma ação. Falha no Mongo só vai para o log (o reporte já está salvo no Oracle).
    /// </summary>
    public async Task RegistrarAsync(Reporte reporte, string acao)
    {
        if (_historico is null) return;

        try
        {
            await _historico.InsertOneAsync(new HistoricoReporte
            {
                ReporteId = reporte.Id,
                EquipamentoId = reporte.EquipamentoId,
                Acao = acao,
                Status = reporte.Status.ToString(),
                Observacao = reporte.Observacao,
                UsuarioId = reporte.UsuarioId
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Não foi possível gravar o histórico no MongoDB: {Erro}", ex.Message);
        }
    }

    public async Task PingAsync()
    {
        if (_database is null) throw new InvalidOperationException("MongoDB não configurado.");

        await _database.RunCommandAsync(new BsonDocumentCommand<BsonDocument>(new BsonDocument("ping", 1)));
    }
}
