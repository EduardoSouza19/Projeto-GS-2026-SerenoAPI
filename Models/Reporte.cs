using System.Text.Json.Serialization;

namespace SerenoAPI.Models;

/// <summary>
/// Representa um aviso do cidadão sobre o status de um equipamento.
/// </summary>
public class Reporte
{
    /// <summary>
    /// Identificador único do reporte.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID do equipamento reportado.
    /// </summary>
    public int EquipamentoId { get; set; }

    /// <summary>
    /// ID do usuário que reportou (claim "sub" do token). Nulo na carga inicial.
    /// </summary>
    public int? UsuarioId { get; set; }

    /// <summary>
    /// Status informado: FUNCIONANDO, PARADO ou EM_MANUTENCAO.
    /// </summary>
    public StatusEquipamento Status { get; set; }

    /// <summary>
    /// Observação opcional (até 300 caracteres).
    /// </summary>
    public string? Observacao { get; set; }

    /// <summary>
    /// Data do reporte (UTC).
    /// </summary>
    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Equipamento reportado.
    /// </summary>
    [JsonIgnore]
    public Equipamento? Equipamento { get; set; }
}
