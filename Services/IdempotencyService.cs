using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SerenoAPI.Data;
using SerenoAPI.Models;

namespace SerenoAPI.Services;

/// <summary>
/// Idempotência do POST /api/reportes:
/// mesma chave + mesmo corpo = devolve a resposta original; mesma chave + corpo diferente = 409.
/// Os registros valem por 24 horas.
/// </summary>
public class IdempotencyService
{
    public static readonly TimeSpan Validade = TimeSpan.FromHours(24);

    private readonly AppDbContext _context;

    public IdempotencyService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// "Impressão digital" (SHA-256) do corpo da requisição.
    /// </summary>
    public static string GerarHash(object corpo) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(corpo, JsonPadrao.Opcoes))));

    /// <summary>
    /// Busca a chave do usuário. Registro vencido (mais de 24 h) é apagado e tratado como novo.
    /// </summary>
    public async Task<IdempotencyRecord?> BuscarAsync(string chave, int usuarioId)
    {
        var registro = await _context.IdempotencyRecords
            .FirstOrDefaultAsync(r => r.Chave == chave && r.UsuarioId == usuarioId);

        if (registro is not null && registro.CriadoEm < DateTime.UtcNow - Validade)
        {
            _context.IdempotencyRecords.Remove(registro);
            await _context.SaveChangesAsync();
            return null;
        }

        return registro;
    }

    /// <summary>
    /// Guarda a resposta que foi enviada para poder repetir depois.
    /// </summary>
    public async Task SalvarAsync(string chave, int usuarioId, string hashDoCorpo, int status, object resposta)
    {
        _context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Chave = chave,
            UsuarioId = usuarioId,
            HashDoCorpo = hashDoCorpo,
            Status = status,
            CorpoDaResposta = JsonSerializer.Serialize(resposta, JsonPadrao.Opcoes)
        });

        await _context.SaveChangesAsync();
    }
}
