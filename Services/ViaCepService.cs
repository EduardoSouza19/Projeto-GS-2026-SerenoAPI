using SerenoAPI.Models;

namespace SerenoAPI.Services;

/// <summary>
/// Consulta o serviço externo ViaCEP (preenche o endereço sem precisar de coordenadas).
/// </summary>
public class ViaCepService
{
    private readonly HttpClient _httpClient;

    public ViaCepService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ViaCepResponse?> BuscarAsync(string cep)
    {
        var somenteNumeros = new string(cep.Where(char.IsDigit).ToArray());

        if (somenteNumeros.Length != 8)
        {
            return null;
        }

        var endereco = await _httpClient.GetFromJsonAsync<ViaCepResponse>($"ws/{somenteNumeros}/json/");

        // CEP inexistente: o ViaCEP devolve só { "erro": true }, então o Cep vem nulo
        return endereco?.Cep is null ? null : endereco;
    }

    /// <summary>
    /// Monta o endereço em uma linha. Ex.: Praça da Sé - Sé, São Paulo - SP
    /// </summary>
    public static string FormatarEndereco(ViaCepResponse e) =>
        $"{e.Logradouro} - {e.Bairro}, {e.Localidade} - {e.Uf}";
}
