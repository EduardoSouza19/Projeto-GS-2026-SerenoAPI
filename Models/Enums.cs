namespace SerenoAPI.Models;

/// <summary>
/// Tipo do equipamento de acessibilidade da estação (igual ao Contrato).
/// </summary>
public enum TipoEquipamento
{
    ELEVADOR,
    ESCADA_ROLANTE,
    RAMPA,
    PLATAFORMA
}

/// <summary>
/// Situação do equipamento (igual ao Contrato).
/// </summary>
public enum StatusEquipamento
{
    FUNCIONANDO,
    PARADO,
    EM_MANUTENCAO
}
