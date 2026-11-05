using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SerenoAPI.Models;

/// <summary>
/// Representa uma estação de metrô, trem ou terminal.
/// </summary>
public class Estacao
{
    /// <summary>
    /// Identificador único da estação.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Nome da estação. Ex.: Sé.
    /// </summary>
    public string Nome { get; set; } = string.Empty;

    /// <summary>
    /// Linha(s) que passam pela estação. Ex.: 1-Azul, 3-Vermelha.
    /// </summary>
    public string Linha { get; set; } = string.Empty;

    /// <summary>
    /// Endereço da estação.
    /// </summary>
    public string Endereco { get; set; } = string.Empty;

    /// <summary>
    /// Latitude (opcional: o cidadão não precisa saber coordenadas).
    /// </summary>
    public double? Latitude { get; set; }

    /// <summary>
    /// Longitude (opcional).
    /// </summary>
    public double? Longitude { get; set; }

    /// <summary>
    /// Quantidade de equipamentos parados ou em manutenção (calculado pela API).
    /// </summary>
    [NotMapped]
    public int EquipamentosParados { get; set; }

    /// <summary>
    /// Equipamentos da estação (aparece só no detalhe).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ICollection<Equipamento>? Equipamentos { get; set; }
}
