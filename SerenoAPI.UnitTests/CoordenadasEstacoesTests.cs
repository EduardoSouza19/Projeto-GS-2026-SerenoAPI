using SerenoAPI.Data;
using Xunit;

namespace SerenoAPI.UnitTests;

public class CoordenadasEstacoesTests
{
    [Fact]
    public void Total_ShouldHaveAllMetroAndTrainStations()
    {
        // Assert
        Assert.True(CoordenadasEstacoes.Total >= 180);
    }

    [Theory]
    [InlineData("Sé")]
    [InlineData("se")]
    [InlineData("Palmeiras-Barra Funda")]
    [InlineData("Barra Funda")]
    [InlineData("Estação Tatuapé")]
    [InlineData("Faria Lima")]
    public void Buscar_ShouldFindStation_WithDifferentWaysOfWritingTheName(string nome)
    {
        // Act
        var coordenadas = CoordenadasEstacoes.Buscar(nome);

        // Assert
        Assert.NotNull(coordenadas);
        Assert.True(CoordenadasEstacoes.EhValida(coordenadas.Value.Latitude, coordenadas.Value.Longitude));
    }

    [Fact]
    public void Resolver_ShouldUseKnownCoordinates_WhenRequestHasNone()
    {
        // Act
        var (latitude, longitude) = CoordenadasEstacoes.Resolver("Sé", null, null);

        // Assert
        Assert.Equal(-23.550921, latitude);
        Assert.Equal(-46.633135, longitude);
    }

    [Fact]
    public void Resolver_ShouldIgnoreTestValuesOutsideSaoPaulo()
    {
        // Act
        var (latitude, longitude) = CoordenadasEstacoes.Resolver("Brás", 90, 180);

        // Assert
        Assert.Equal(-23.547884, latitude);
        Assert.Equal(-46.615909, longitude);
    }

    [Fact]
    public void Resolver_ShouldReturnNull_WhenStationIsUnknownAndHasNoCoordinates()
    {
        // Act
        var (latitude, longitude) = CoordenadasEstacoes.Resolver("Estação Inventada", null, null);

        // Assert
        Assert.Null(latitude);
        Assert.Null(longitude);
    }
}
