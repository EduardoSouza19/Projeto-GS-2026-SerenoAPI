using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SerenoAPI.Services;

/// <summary>
/// Padrão de JSON do Contrato: camelCase, enums em MAIÚSCULAS (texto) e datas ISO 8601 em UTC.
/// </summary>
public static class JsonPadrao
{
    public static readonly JsonSerializerOptions Opcoes = Configurar(new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static JsonSerializerOptions Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.Converters.Add(new JsonStringEnumConverter());
        opcoes.Converters.Add(new DataUtcConverter());
        return opcoes;
    }
}

/// <summary>
/// Sempre escreve as datas em UTC com "Z" no final. Ex.: 2026-11-10T14:30:00Z
/// </summary>
public class DataUtcConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTime().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTime.SpecifyKind(value, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
}
