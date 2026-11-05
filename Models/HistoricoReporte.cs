using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace SerenoAPI.Models;

/// <summary>
/// Documento do MongoDB: histórico (auditoria) de tudo que acontece com os reportes.
/// </summary>
public class HistoricoReporte
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public int ReporteId { get; set; }
    public int EquipamentoId { get; set; }
    public string Acao { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Observacao { get; set; }
    public int? UsuarioId { get; set; }
    public DateTime Data { get; set; } = DateTime.UtcNow;
}
