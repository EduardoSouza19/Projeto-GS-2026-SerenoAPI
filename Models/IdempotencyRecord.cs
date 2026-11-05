namespace SerenoAPI.Models;

/// <summary>
/// Guarda a resposta de um POST /api/reportes para não duplicar quando o app reenviar.
/// </summary>
public class IdempotencyRecord
{
    public int Id { get; set; }
    public string Chave { get; set; } = string.Empty;
    public int UsuarioId { get; set; }
    public string HashDoCorpo { get; set; } = string.Empty;
    public int Status { get; set; }
    public string CorpoDaResposta { get; set; } = string.Empty;
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
