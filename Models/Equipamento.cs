using System.Text.Json.Serialization;

namespace SerenoAPI.Models;

/// <summary>
/// Representa um elevador, escada rolante, rampa ou plataforma de uma estação.
/// </summary>
public class Equipamento
{
    /// <summary>
    /// Identificador único do equipamento.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// ID da estação do equipamento.
    /// </summary>
    public int EstacaoId { get; set; }

    /// <summary>
    /// Tipo: ELEVADOR, ESCADA_ROLANTE, RAMPA ou PLATAFORMA.
    /// </summary>
    public TipoEquipamento Tipo { get; set; }

    /// <summary>
    /// Onde fica o equipamento. Ex.: Acesso pela Praça da Sé.
    /// </summary>
    public string Localizacao { get; set; } = string.Empty;

    /// <summary>
    /// Status atual = status do reporte mais recente.
    /// </summary>
    public StatusEquipamento StatusAtual { get; set; } = StatusEquipamento.FUNCIONANDO;

    /// <summary>
    /// Data da última mudança de status (UTC).
    /// </summary>
    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Estação do equipamento.
    /// </summary>
    [JsonIgnore]
    public Estacao? Estacao { get; set; }

    /// <summary>
    /// Reportes feitos sobre o equipamento.
    /// </summary>
    [JsonIgnore]
    public ICollection<Reporte>? Reportes { get; set; }
}
