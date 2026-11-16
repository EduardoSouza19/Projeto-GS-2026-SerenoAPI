using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SerenoAPI.Data;

/// <summary>
/// Coordenadas reais de todas as estações de Metrô e trem da Grande São Paulo
/// (a lista fica em CoordenadasEstacoes.Lista.cs).
/// O app usa latitude/longitude para achar a estação mais próxima, então ninguém precisa digitar.
/// </summary>
public static partial class CoordenadasEstacoes
{
    /// <summary>
    /// Quantidade de estações conhecidas.
    /// </summary>
    public static int Total => Conhecidas.Count;

    /// <summary>
    /// Coordenadas pelo nome da estação, com ou sem acento, hífen ou nome de patrocínio.
    /// Ex.: "Sé", "se", "Palmeiras-Barra Funda", "Barra Funda", "Estação Tatuapé". Null se não conhecer.
    /// </summary>
    public static (double Latitude, double Longitude)? Buscar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;

        var chave = Normalizar(nome);

        foreach (var tentativa in new[] { chave, SemPrefixo(chave) })
        {
            if (Conhecidas.TryGetValue(tentativa, out var c)) return c;
            if (Apelidos.TryGetValue(tentativa, out var oficial)) return Conhecidas[oficial];
        }

        return null;
    }

    /// <summary>
    /// Confere se o ponto fica na Grande São Paulo (evita valores de teste como 90/180).
    /// </summary>
    public static bool EhValida(double? latitude, double? longitude) =>
        latitude is >= -24.1 and <= -23.1 && longitude is >= -47.2 and <= -46.1;

    /// <summary>
    /// Decide as coordenadas da estação: usa as informadas se forem válidas;
    /// senão, as conhecidas pelo nome; senão, deixa vazio.
    /// </summary>
    public static (double? Latitude, double? Longitude) Resolver(string nome, double? latitude, double? longitude)
    {
        if (EhValida(latitude, longitude))
            return (latitude, longitude);

        var conhecida = Buscar(nome);

        return conhecida is null ? (null, null) : (conhecida.Value.Latitude, conhecida.Value.Longitude);
    }

    // "Palmeiras-Barra Funda" -> "PALMEIRAS BARRA FUNDA"; "Sé" -> "SE"
    private static string Normalizar(string texto)
    {
        var semAcento = new string(texto.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());

        semAcento = Regex.Replace(semAcento, @"\(.*?\)", " ");
        semAcento = Regex.Replace(semAcento, @"[^A-Za-z0-9]+", " ");

        return semAcento.Trim().ToUpperInvariant();
    }

    // "ESTACAO TATUAPE" -> "TATUAPE"
    private static string SemPrefixo(string chave) =>
        Regex.Replace(chave, @"^(ESTACAO|TERMINAL|METRO|CPTM)\s+", "");
}
