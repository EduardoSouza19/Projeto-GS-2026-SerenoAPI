using SerenoAPI.Models;
using SerenoAPI.Services;
using Xunit;

namespace SerenoAPI.UnitTests;

public class IdempotencyServiceTests
{
    private static ReporteRequest Reporte(StatusEquipamento status) =>
        new() { EquipamentoId = 1, Status = status, Observacao = "Elevador parado" };

    [Fact]
    public async Task BuscarAsync_ShouldReturnSavedResponse_WhenSameKeyIsUsedAgain()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(BuscarAsync_ShouldReturnSavedResponse_WhenSameKeyIsUsedAgain));
        var service = new IdempotencyService(context);
        var hash = IdempotencyService.GerarHash(Reporte(StatusEquipamento.PARADO));
        await service.SalvarAsync("chave-1", 10, hash, 201, new { id = 5 });

        // Act
        var registro = await service.BuscarAsync("chave-1", 10);

        // Assert
        Assert.NotNull(registro);
        Assert.Equal(201, registro!.Status);
        Assert.Equal(hash, registro.HashDoCorpo);
        Assert.Single(context.IdempotencyRecords);
    }

    [Fact]
    public void GerarHash_ShouldBeDifferent_WhenBodyIsDifferent()
    {
        // Act
        var parado = IdempotencyService.GerarHash(Reporte(StatusEquipamento.PARADO));
        var paradoDeNovo = IdempotencyService.GerarHash(Reporte(StatusEquipamento.PARADO));
        var funcionando = IdempotencyService.GerarHash(Reporte(StatusEquipamento.FUNCIONANDO));

        // Assert
        Assert.Equal(parado, paradoDeNovo);   // mesmo corpo = mesma resposta
        Assert.NotEqual(parado, funcionando); // corpo diferente = conflito (409)
    }

    [Fact]
    public async Task BuscarAsync_ShouldIgnoreKeyFromAnotherUser()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(BuscarAsync_ShouldIgnoreKeyFromAnotherUser));
        var service = new IdempotencyService(context);
        await service.SalvarAsync("chave-1", 10, "HASH", 201, new { id = 5 });

        // Act
        var registro = await service.BuscarAsync("chave-1", 99);

        // Assert
        Assert.Null(registro);
    }

    [Fact]
    public async Task BuscarAsync_ShouldDiscardRecordsOlderThan24Hours()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(BuscarAsync_ShouldDiscardRecordsOlderThan24Hours));
        context.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Chave = "chave-antiga", UsuarioId = 10, HashDoCorpo = "HASH", Status = 201,
            CorpoDaResposta = "{}", CriadoEm = DateTime.UtcNow.AddHours(-25)
        });
        await context.SaveChangesAsync();
        var service = new IdempotencyService(context);

        // Act
        var registro = await service.BuscarAsync("chave-antiga", 10);

        // Assert
        Assert.Null(registro);
        Assert.Empty(context.IdempotencyRecords);
    }
}
