using Microsoft.EntityFrameworkCore;
using SerenoAPI.Data;
using SerenoAPI.Models;

namespace SerenoAPI.Services;

/// <summary>
/// Regra de negócio do "Elevador Funciona?":
/// o status atual do equipamento é o do reporte mais recente.
/// </summary>
public class StatusService
{
    private readonly AppDbContext _context;

    public StatusService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Recalcula o statusAtual do equipamento olhando o reporte mais recente.
    /// </summary>
    public async Task AtualizarStatusAtualAsync(int equipamentoId)
    {
        var equipamento = await _context.Equipamentos.FindAsync(equipamentoId);

        if (equipamento is null)
        {
            return;
        }

        var reportes = await _context.Reportes
            .Where(r => r.EquipamentoId == equipamentoId)
            .ToListAsync();

        var maisRecente = reportes.OrderByDescending(r => r.CriadoEm).ThenByDescending(r => r.Id).FirstOrDefault();

        equipamento.StatusAtual = CalcularStatusAtual(reportes);
        equipamento.AtualizadoEm = maisRecente?.CriadoEm ?? DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Status do reporte mais recente. Sem reportes = FUNCIONANDO.
    /// </summary>
    public static StatusEquipamento CalcularStatusAtual(IEnumerable<Reporte> reportes) =>
        reportes
            .OrderByDescending(r => r.CriadoEm)
            .ThenByDescending(r => r.Id)
            .Select(r => r.Status)
            .DefaultIfEmpty(StatusEquipamento.FUNCIONANDO)
            .First();

    /// <summary>
    /// Quantos equipamentos estão PARADO ou EM_MANUTENCAO.
    /// </summary>
    public static int ContarParados(IEnumerable<Equipamento> equipamentos) =>
        equipamentos.Count(e => e.StatusAtual != StatusEquipamento.FUNCIONANDO);

    /// <summary>
    /// ACESSIVEL (tudo funcionando), PARCIAL (algum parado), INACESSIVEL (todos parados) ou SEM_INFORMACAO.
    /// </summary>
    public static string ClassificarEstacao(IEnumerable<Equipamento> equipamentos)
    {
        var lista = equipamentos.ToList();
        var parados = ContarParados(lista);

        return lista.Count == 0 ? "SEM_INFORMACAO"
            : parados == 0 ? "ACESSIVEL"
            : parados < lista.Count ? "PARCIAL"
            : "INACESSIVEL";
    }

    /// <summary>
    /// Monta o resumo de acessibilidade de uma estação (usado na rota).
    /// </summary>
    public static AcessibilidadeEstacaoResponse ResumoEstacao(Estacao estacao)
    {
        var equipamentos = estacao.Equipamentos?.ToList() ?? new List<Equipamento>();

        return new AcessibilidadeEstacaoResponse
        {
            EstacaoId = estacao.Id,
            Estacao = estacao.Nome,
            Linha = estacao.Linha,
            Classificacao = ClassificarEstacao(equipamentos),
            EquipamentosParados = ContarParados(equipamentos),
            Avisos = equipamentos
                .Where(e => e.StatusAtual != StatusEquipamento.FUNCIONANDO)
                .Select(e => $"{e.Tipo} ({e.Localizacao}): {e.StatusAtual}")
                .ToList()
        };
    }
}
