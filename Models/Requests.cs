using System.ComponentModel.DataAnnotations;

namespace SerenoAPI.Models;

/// <summary>
/// Corpo do POST/PUT de estação.
/// Latitude e longitude são opcionais: basta o endereço ou o CEP.
/// </summary>
public class EstacaoRequest
{
    [Required(ErrorMessage = "é obrigatório")]
    [StringLength(100, ErrorMessage = "deve ter no máximo 100 caracteres")]
    public string Nome { get; set; } = string.Empty;

    [Required(ErrorMessage = "é obrigatório")]
    [StringLength(60, ErrorMessage = "deve ter no máximo 60 caracteres")]
    public string Linha { get; set; } = string.Empty;

    /// <summary>Endereço. Se vier vazio e o CEP for informado, a API preenche pelo ViaCEP.</summary>
    [StringLength(200, ErrorMessage = "deve ter no máximo 200 caracteres")]
    public string? Endereco { get; set; }

    /// <summary>CEP opcional para preencher o endereço automaticamente.</summary>
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "deve estar no formato 00000-000")]
    public string? Cep { get; set; }

    /// <summary>Opcional.</summary>
    [Range(-90, 90, ErrorMessage = "deve estar entre -90 e 90")]
    public double? Latitude { get; set; }

    /// <summary>Opcional.</summary>
    [Range(-180, 180, ErrorMessage = "deve estar entre -180 e 180")]
    public double? Longitude { get; set; }
}

/// <summary>
/// Corpo do POST/PUT de equipamento.
/// </summary>
public class EquipamentoRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "é obrigatório")]
    public int EstacaoId { get; set; }

    [Required(ErrorMessage = "é obrigatório (ELEVADOR, ESCADA_ROLANTE, RAMPA ou PLATAFORMA)")]
    [EnumDataType(typeof(TipoEquipamento), ErrorMessage = "valor inválido")]
    public TipoEquipamento? Tipo { get; set; }

    [Required(ErrorMessage = "é obrigatório")]
    [StringLength(150, ErrorMessage = "deve ter no máximo 150 caracteres")]
    public string Localizacao { get; set; } = string.Empty;
}

/// <summary>
/// Corpo do POST /api/reportes (envie também o header Idempotency-Key).
/// </summary>
public class ReporteRequest
{
    /// <example>1</example>
    [Range(1, int.MaxValue, ErrorMessage = "é obrigatório")]
    public int EquipamentoId { get; set; }

    /// <example>PARADO</example>
    [Required(ErrorMessage = "é obrigatório (FUNCIONANDO, PARADO ou EM_MANUTENCAO)")]
    [EnumDataType(typeof(StatusEquipamento), ErrorMessage = "valor inválido")]
    public StatusEquipamento? Status { get; set; }

    /// <example>Elevador parado desde cedo, com aviso na porta.</example>
    [StringLength(300, ErrorMessage = "deve ter no máximo 300 caracteres")]
    public string? Observacao { get; set; }
}

/// <summary>
/// Corpo do PUT /api/reportes/{id}.
/// </summary>
public class ReporteUpdateRequest
{
    /// <example>FUNCIONANDO</example>
    [Required(ErrorMessage = "é obrigatório (FUNCIONANDO, PARADO ou EM_MANUTENCAO)")]
    [EnumDataType(typeof(StatusEquipamento), ErrorMessage = "valor inválido")]
    public StatusEquipamento? Status { get; set; }

    /// <example>Voltou a funcionar.</example>
    [StringLength(300, ErrorMessage = "deve ter no máximo 300 caracteres")]
    public string? Observacao { get; set; }
}

/// <summary>
/// Corpo do POST /api/auth/token (somente desenvolvimento).
/// </summary>
public class TokenRequest
{
    /// <example>cidadao@sereno.app</example>
    [Required(ErrorMessage = "é obrigatório")]
    public string Email { get; set; } = string.Empty;
}
