using System.Net;
using System.Text.Json;
using Xunit;

namespace SerenoAPI.IntegrationTests;

[Collection(ApiCollection.Name)]
public class SwaggerIntegrationTests
{
    private readonly ApiFactory _factory;

    public SwaggerIntegrationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Swagger_ShouldDocumentHttpResponsesOfReportes()
    {
        // Act
        var response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        var respostas = json.GetProperty("paths").GetProperty("/api/reportes").GetProperty("post").GetProperty("responses");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        foreach (var codigo in new[] { "201", "400", "401", "409", "422", "429" })
        {
            Assert.True(respostas.TryGetProperty(codigo, out _), $"POST /api/reportes sem a resposta {codigo} no Swagger");
        }
    }
}
