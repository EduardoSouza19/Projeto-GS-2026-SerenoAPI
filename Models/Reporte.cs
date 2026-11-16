using System.ComponentModel.DataAnnotations.Schema;
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
    /// Tipo do equipamento (vem do equipamento, não é gravado na tabela).
    /// </summary>
    [NotMapped]
    public TipoEquipamento? Tipo => Equipamento?.Tipo;

    /// <summary>
    /// Localização do equipamento dentro da estação.
    /// </summary>
    [NotMapped]
    public string? Localizacao => Equipamento?.Localizacao;

    /// <summary>
    /// ID da estação do equipamento.
    /// </summary>
    [NotMapped]
    public int? EstacaoId => Equipamento?.EstacaoId;

    /// <summary>
    /// Nome da estação do equipamento.
    /// </summary>
    [NotMapped]
    public string? EstacaoNome => Equipamento?.Estacao?.Nome;

    /// <summary>
    /// Equipamento reportado.
    /// </summary>
    [JsonIgnore]
    public Equipamento? Equipamento { get; set; }
}
