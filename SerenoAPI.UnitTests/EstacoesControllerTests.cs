using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SerenoAPI.Controllers;
using SerenoAPI.Data;
using SerenoAPI.Models;
using SerenoAPI.Services;
using Xunit;

namespace SerenoAPI.UnitTests;

public class EstacoesControllerTests
{
    private static EstacoesController CriarController(AppDbContext context) =>
        new(context, new ViaCepService(new HttpClient()))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

    [Fact]
    public async Task GetEstacaoById_ShouldReturnStationWithStoppedEquipmentCount()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(GetEstacaoById_ShouldReturnStationWithStoppedEquipmentCount));
        context.Estacoes.Add(new Estacao
        {
            Id = 1, Nome = "Sé", Linha = "1-Azul", Endereco = "Praça da Sé",
            Equipamentos = new List<Equipamento>
            {
                new() { Tipo = TipoEquipamento.ELEVADOR, Localizacao = "Acesso", StatusAtual = StatusEquipamento.PARADO },
                new() { Tipo = TipoEquipamento.RAMPA, Localizacao = "Saída", StatusAtual = StatusEquipamento.FUNCIONANDO }
            }
        });
        await context.SaveChangesAsync();
        var controller = CriarController(context);

        // Act
        var result = await controller.GetEstacaoById(1);

        // Assert
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var estacao = Assert.IsType<Estacao>(ok.Value);
        Assert.Equal(1, estacao.EquipamentosParados);
        Assert.Equal(2, estacao.Equipamentos!.Count);
    }

    [Fact]
    public async Task GetEstacaoById_ShouldReturnNotFound_WhenStationDoesNotExist()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(GetEstacaoById_ShouldReturnNotFound_WhenStationDoesNotExist));
        var controller = CriarController(context);

        // Act
        var result = await controller.GetEstacaoById(999);

        // Assert
        var erro = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(404, erro.StatusCode);
        Assert.Equal("NAO_ENCONTRADO", Assert.IsType<ErroResponse>(erro.Value).Erro);
    }

    [Fact]
    public async Task PostEstacao_ShouldCreateStation_WithoutLatitudeAndLongitude()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(PostEstacao_ShouldCreateStation_WithoutLatitudeAndLongitude));
        var controller = CriarController(context);
        var request = new EstacaoRequest { Nome = "Luz", Linha = "1-Azul", Endereco = "Luz, São Paulo - SP" };

        // Act
        var result = await controller.PostEstacao(request);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Null(context.Estacoes.Single().Latitude);
    }

    [Fact]
    public async Task PostEstacao_ShouldReturnBadRequest_WithoutAddressAndCep()
    {
        // Arrange
        await using var context = TestDbContextFactory.Create(nameof(PostEstacao_ShouldReturnBadRequest_WithoutAddressAndCep));
        var controller = CriarController(context);

        // Act
        var result = await controller.PostEstacao(new EstacaoRequest { Nome = "Luz", Linha = "1-Azul" });

        // Assert
        var erro = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(400, erro.StatusCode);
        Assert.Empty(context.Estacoes);
    }
}
