using SerenoAPI.Models;
using SerenoAPI.Services;
using Xunit;

namespace SerenoAPI.UnitTests;

public class StatusServiceTests
{
    [Fact]
    public void CalcularStatusAtual_ShouldReturnStatusOfMostRecentReporte()
    {
        // Arrange
        var reportes = new List<Reporte>
        {
            new() { Id = 1, Status = StatusEquipamento.PARADO, CriadoEm = new DateTime(2026, 11, 10, 8, 0, 0) },
            new() { Id = 2, Status = StatusEquipamento.FUNCIONANDO, CriadoEm = new DateTime(2026, 11, 10, 12, 0, 0) },
            new() { Id = 3, Status = StatusEquipamento.EM_MANUTENCAO, CriadoEm = new DateTime(2026, 11, 10, 10, 0, 0) }
        };

        // Act
        var status = StatusService.CalcularStatusAtual(reportes);

        // Assert
        Assert.Equal(StatusEquipamento.FUNCIONANDO, status);
    }

    [Fact]
    public void CalcularStatusAtual_ShouldBeFuncionando_WhenThereAreNoReportes()
    {
        // Act
        var status = StatusService.CalcularStatusAtual(new List<Reporte>());

        // Assert
        Assert.Equal(StatusEquipamento.FUNCIONANDO, status);
    }

    [Fact]
    public void ContarParados_ShouldCountParadoAndEmManutencao()
    {
        // Arrange
        var equipamentos = new List<Equipamento>
        {
            new() { StatusAtual = StatusEquipamento.PARADO },
            new() { StatusAtual = StatusEquipamento.EM_MANUTENCAO },
            new() { StatusAtual = StatusEquipamento.FUNCIONANDO }
        };

        // Act
        var parados = StatusService.ContarParados(equipamentos);

        // Assert
        Assert.Equal(2, parados);
    }

    [Theory]
    [InlineData(0, 3, "ACESSIVEL")]
    [InlineData(1, 3, "PARCIAL")]
    [InlineData(3, 3, "INACESSIVEL")]
    [InlineData(0, 0, "SEM_INFORMACAO")]
    public void ClassificarEstacao_ShouldClassifyByStoppedEquipment(int parados, int total, string esperado)
    {
        // Arrange
        var equipamentos = Enumerable.Range(0, total)
            .Select(i => new Equipamento { StatusAtual = i < parados ? StatusEquipamento.PARADO : StatusEquipamento.FUNCIONANDO })
            .ToList();

        // Act
        var classificacao = StatusService.ClassificarEstacao(equipamentos);

        // Assert
        Assert.Equal(esperado, classificacao);
    }
}
