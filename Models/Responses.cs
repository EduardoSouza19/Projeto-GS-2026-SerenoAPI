namespace SerenoAPI.Models;

/// <summary>
/// Paginação : { items, page, size, totalItems, totalPages }.
/// </summary>
public class PaginaResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int Size { get; set; }
    public int TotalItems { get; set; }
    public int TotalPages => Size == 0 ? 0 : (int)Math.Ceiling(TotalItems / (double)Size);
}

/// <summary>
/// Erro : { status, erro, mensagem, campos, timestamp, path }.
/// </summary>
public class ErroResponse
{
    public int Status { get; set; }
    public string Erro { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
    public List<CampoErro> Campos { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Path { get; set; } = string.Empty;

    public static ErroResponse Criar(int status, string erro, string mensagem, string path, List<CampoErro>? campos = null) =>
        new() { Status = status, Erro = erro, Mensagem = mensagem, Path = path, Campos = campos ?? new() };
}

/// <summary>
/// Campo inválido dentro do erro de validação.
/// </summary>
public class CampoErro
{
    public string Campo { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
}

/// <summary>
/// Resultado da verificação de acessibilidade de uma estação.
/// </summary>
public class AcessibilidadeEstacaoResponse
{
    public int EstacaoId { get; set; }
    public string Estacao { get; set; } = string.Empty;
    public string Linha { get; set; } = string.Empty;
    public string Classificacao { get; set; } = string.Empty;
    public int EquipamentosParados { get; set; }
    public List<string> Avisos { get; set; } = new();
}

/// <summary>
/// Resultado da rota entre duas estações (sem coordenadas: só pelo nome).
/// </summary>
public class RotaAcessivelResponse
{
    public AcessibilidadeEstacaoResponse Origem { get; set; } = new();
    public AcessibilidadeEstacaoResponse Destino { get; set; } = new();
    public string Resultado { get; set; } = string.Empty;
    public string Mensagem { get; set; } = string.Empty;
}

/// <summary>
/// Endereço retornado pelo ViaCEP.
/// </summary>
public class ViaCepResponse
{
    public string? Cep { get; set; }
    public string? Logradouro { get; set; }
    public string? Bairro { get; set; }
    public string? Localidade { get; set; }
    public string? Uf { get; set; }
}
