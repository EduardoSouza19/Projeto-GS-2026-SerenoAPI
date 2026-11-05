using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SerenoAPI.IntegrationTests;

[Collection(ApiCollection.Name)]
public class EstacoesControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public EstacoesControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task GetEstacoes_ShouldReturnUnauthorized_WithoutToken()
    {
        // Act
        var response = await _factory.CreateClient().GetAsync("/api/estacoes");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetEstacoes_ShouldReturnPageInContractFormat()
    {
        // Act
        var response = await _factory.CreateClientComToken().GetAsync("/api/estacoes?page=0&size=10");
        var pagina = await LerJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(pagina.GetProperty("items").GetArrayLength() > 0);
        Assert.Equal(0, pagina.GetProperty("page").GetInt32());
        Assert.True(pagina.TryGetProperty("totalPages", out _));
        Assert.True(pagina.GetProperty("items")[0].TryGetProperty("equipamentosParados", out _));
    }

    [Fact]
    public async Task PostEstacao_ShouldReturnForbidden_ForCidadao()
    {
        // Act
        var response = await _factory.CreateClientComToken("CIDADAO")
            .PostAsJsonAsync("/api/estacoes", new { nome = "Brás", linha = "3-Vermelha", endereco = "Brás, São Paulo - SP" });

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CrudEstacao_ShouldWork_ForGestor()
    {
        // Arrange
        var client = _factory.CreateClientComToken("GESTOR");

        // Create (sem latitude e longitude)
        var criada = await client.PostAsJsonAsync("/api/estacoes",
            new { nome = "Brás", linha = "3-Vermelha", endereco = "Brás, São Paulo - SP" });
        var id = (await LerJsonAsync(criada)).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.Created, criada.StatusCode);

        // Update
        var atualizada = await client.PutAsJsonAsync($"/api/estacoes/{id}",
            new { nome = "Brás", linha = "3-Vermelha, 10-Turquesa", endereco = "Brás, São Paulo - SP" });
        Assert.Equal(HttpStatusCode.OK, atualizada.StatusCode);

        // Read / Delete
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/estacoes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/estacoes/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/estacoes/{id}")).StatusCode);
    }

    [Fact]
    public async Task VerificarRota_ShouldWorkWithStationNamesOnly()
    {
        // Act
        var response = await _factory.CreateClientComToken().GetAsync("/api/rotas/acessibilidade?origem=sé&destino=tatuapé");
        var rota = await LerJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Sé", rota.GetProperty("origem").GetProperty("estacao").GetString());
        Assert.False(string.IsNullOrWhiteSpace(rota.GetProperty("resultado").GetString()));
    }

    [Fact]
    public async Task Health_ShouldListOracleAndMongoDb()
    {
        // Act (nos testes não há Oracle real, então pode voltar 503: o importante é monitorar tudo)
        var body = await (await _factory.CreateClient().GetAsync("/health")).Content.ReadAsStringAsync();

        // Assert
        Assert.Contains("\"oracle\"", body);
        Assert.Contains("\"mongodb\"", body);
    }
}
