using Microsoft.EntityFrameworkCore;
using SerenoAPI.Models;

namespace SerenoAPI.Data;

/// <summary>
/// Cria as tabelas (se ainda não existirem), cadastra 5 estações de São Paulo com 12 equipamentos
/// e completa as coordenadas das estações conhecidas.
/// </summary>
public static class DataSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            if (context.Database.IsRelational())
            {
                // Com a pasta Migrations aplica as migrations; sem ela, cria as tabelas direto (banco novo/Docker)
                if (context.Database.GetMigrations().Any())
                    await context.Database.MigrateAsync();
                else
                    await context.Database.EnsureCreatedAsync();
            }

            if (await context.Estacoes.AnyAsync())
            {
                // Banco já tem estações: só completa as coordenadas que faltam ou estão erradas
                await CorrigirCoordenadasAsync(context, logger);
                return;
            }

            context.Estacoes.AddRange(EstacoesIniciais());
            await context.SaveChangesAsync();

            logger.LogInformation("Seed: 5 estações e 12 equipamentos cadastrados.");
        }
        catch (Exception ex)
        {
            logger.LogWarning("Seed não executado: {Erro}. Confira a ConnectionStrings:Default e as tabelas.", ex.Message);
        }
    }

    /// <summary>
    /// Preenche latitude/longitude das estações conhecidas que estão vazias ou fora de São Paulo.
    /// </summary>
    private static async Task CorrigirCoordenadasAsync(AppDbContext context, ILogger logger)
    {
        var estacoes = await context.Estacoes.ToListAsync();
        var corrigidas = 0;

        foreach (var estacao in estacoes.Where(e => !CoordenadasEstacoes.EhValida(e.Latitude, e.Longitude)))
        {
            var conhecida = CoordenadasEstacoes.Buscar(estacao.Nome);

            if (conhecida is null) continue;

            estacao.Latitude = conhecida.Value.Latitude;
            estacao.Longitude = conhecida.Value.Longitude;
            corrigidas++;
        }

        if (corrigidas > 0)
        {
            await context.SaveChangesAsync();
            logger.LogInformation("Seed: coordenadas corrigidas em {Quantidade} estação(ões).", corrigidas);
        }
    }

    private static List<Estacao> EstacoesIniciais() => new()
    {
        Nova("Sé", "1-Azul, 3-Vermelha", "Praça da Sé - Sé, São Paulo - SP",
            Equip(TipoEquipamento.ELEVADOR, "Acesso pela Praça da Sé", StatusEquipamento.PARADO),
            Equip(TipoEquipamento.ELEVADOR, "Plataforma da Linha 1-Azul"),
            Equip(TipoEquipamento.ESCADA_ROLANTE, "Mezanino para a Linha 3-Vermelha")),

        Nova("Luz", "1-Azul, 4-Amarela", "Luz, São Paulo - SP",
            Equip(TipoEquipamento.ELEVADOR, "Acesso principal"),
            Equip(TipoEquipamento.PLATAFORMA, "Transferência para a Linha 4-Amarela", StatusEquipamento.EM_MANUTENCAO),
            Equip(TipoEquipamento.ESCADA_ROLANTE, "Saída para a Pinacoteca")),

        Nova("Paulista", "4-Amarela", "Consolação, São Paulo - SP",
            Equip(TipoEquipamento.ELEVADOR, "Acesso pela Rua da Consolação"),
            Equip(TipoEquipamento.ESCADA_ROLANTE, "Plataforma sentido Luz", StatusEquipamento.PARADO)),

        Nova("Tatuapé", "3-Vermelha", "Tatuapé, São Paulo - SP",
            Equip(TipoEquipamento.ELEVADOR, "Acesso norte"),
            Equip(TipoEquipamento.RAMPA, "Ligação com o terminal de ônibus")),

        Nova("Palmeiras-Barra Funda", "3-Vermelha", "Barra Funda, São Paulo - SP",
            Equip(TipoEquipamento.ELEVADOR, "Terminal rodoviário"),
            Equip(TipoEquipamento.ESCADA_ROLANTE, "Plataforma da Linha 3-Vermelha"))
    };

    private static Estacao Nova(string nome, string linha, string endereco, params Equipamento[] equipamentos) =>
        new()
        {
            Nome = nome,
            Linha = linha,
            Endereco = endereco,
            Latitude = CoordenadasEstacoes.Buscar(nome)?.Latitude,
            Longitude = CoordenadasEstacoes.Buscar(nome)?.Longitude,
            Equipamentos = equipamentos.ToList()
        };

    private static Equipamento Equip(TipoEquipamento tipo, string localizacao, StatusEquipamento status = StatusEquipamento.FUNCIONANDO) =>
        new()
        {
            Tipo = tipo,
            Localizacao = localizacao,
            StatusAtual = status,
            Reportes = status == StatusEquipamento.FUNCIONANDO
                ? new List<Reporte>()
                : new List<Reporte> { new() { Status = status, Observacao = "Carga inicial de exemplo" } }
        };
}
