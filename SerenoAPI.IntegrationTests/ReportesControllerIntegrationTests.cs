using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace SerenoAPI.IntegrationTests;

[Collection(ApiCollection.Name)]
public class ReportesControllerIntegrationTests
{
    private readonly ApiFactory _factory;

    public ReportesControllerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private static object Reporte(string status = "PARADO") =>
        new { equipamentoId = 1, status, observacao = "Elevador parado desde cedo" };

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient client, object corpo, string? chave)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/reportes") { Content = JsonContent.Create(corpo) };

        if (chave is not null)
        {
            request.Headers.Add("Idempotency-Key", chave);
        }

        return client.SendAsync(request);
    }

    private static async Task<JsonElement> LerJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    [Fact]
    public async Task PostReporte_ShouldReturnUnauthorized_WithoutToken()
    {
        // Act
        var response = await EnviarAsync(_factory.CreateClient(), Reporte(), Guid.NewGuid().ToString());
        var erro = await LerJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("NAO_AUTENTICADO", erro.GetProperty("erro").GetString());
    }

    [Fact]
    public async Task PostReporte_ShouldReturnBadRequest_WithoutIdempotencyKey()
    {
        // Act
        var response = await EnviarAsync(_factory.CreateClientComToken(), Reporte(), chave: null);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostReporte_ShouldReturnCreated_WithToken()
    {
        // Act
        var response = await EnviarAsync(_factory.CreateClientComToken(), Reporte(), Guid.NewGuid().ToString());
        var reporte = await LerJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("PARADO", reporte.GetProperty("status").GetString());
    }

    [Fact]
    public async Task PostReporte_ShouldNotDuplicate_WhenSameKeyAndSameBody()
    {
        // Arrange
        var client = _factory.CreateClientComToken();
        var chave = Guid.NewGuid().ToString();

        // Act: o app reenviou porque a internet caiu
        var primeira = await EnviarAsync(client, Reporte(), chave);
        var segunda = await EnviarAsync(client, Reporte(), chave);
        var meus = await LerJsonAsync(await client.GetAsync("/api/reportes/meus"));

        // Assert
        Assert.Equal(HttpStatusCode.Created, primeira.StatusCode);
        Assert.Equal(HttpStatusCode.Created, segunda.StatusCode);
        Assert.Equal((await LerJsonAsync(primeira)).GetProperty("id").GetInt32(), (await LerJsonAsync(segunda)).GetProperty("id").GetInt32());
        Assert.Equal(1, meus.GetProperty("totalItems").GetInt32());
    }

    [Fact]
    public async Task PostReporte_ShouldReturnConflict_WhenSameKeyAndDifferentBody()
    {
        // Arrange
        var client = _factory.CreateClientComToken();
        var chave = Guid.NewGuid().ToString();
        await EnviarAsync(client, Reporte("PARADO"), chave);

        // Act
        var response = await EnviarAsync(client, Reporte("FUNCIONANDO"), chave);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PostReporte_ShouldReturnBadRequest_WhenStatusIsInvalid()
    {
        // Act
        var response = await EnviarAsync(_factory.CreateClientComToken(),
            new { equipamentoId = 1, status = "QUEBRADO" }, Guid.NewGuid().ToString());
        var erro = await LerJsonAsync(response);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("VALIDACAO", erro.GetProperty("erro").GetString());
    }

    [Fact]
    public async Task PostReporte_ShouldReturnTooManyRequests_AfterTenReportsInOneMinute()
    {
        // Arrange
        var client = _factory.CreateClientComToken();
        var respostas = new List<HttpResponseMessage>();

        // Act
        for (var i = 0; i < 11; i++)
        {
            respostas.Add(await EnviarAsync(client, Reporte(), Guid.NewGuid().ToString()));
        }

        // Assert
        Assert.All(respostas.Take(10), r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));
        Assert.Equal(HttpStatusCode.TooManyRequests, respostas[10].StatusCode);
        Assert.True(respostas[10].Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task DeleteReporte_ShouldReturnForbidden_WhenUserIsNotTheOwner()
    {
        // Arrange
        var dono = _factory.CreateClientComToken();
        var outro = _factory.CreateClientComToken();
        var criado = await LerJsonAsync(await EnviarAsync(dono, Reporte(), Guid.NewGuid().ToString()));

        // Act
        var response = await outro.DeleteAsync($"/api/reportes/{criado.GetProperty("id").GetInt32()}");

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
