using System.Text.Json.Serialization;

namespace StopBet.Core.Servicos.Classificacao.Gemini;

internal sealed class RequisicaoGerarConteudo
{
    [JsonPropertyName("contents")]
    public List<ConteudoGemini> Conteudos { get; set; } = [];

    [JsonPropertyName("generationConfig")]
    public ConfiguracaoGeracao? ConfiguracaoGeracao { get; set; }
}

internal sealed class ConteudoGemini
{
    [JsonPropertyName("parts")]
    public List<ParteConteudo> Partes { get; set; } = [];

    [JsonPropertyName("role")]
    public string? Papel { get; set; }
}

internal sealed class ParteConteudo
{
    [JsonPropertyName("text")]
    public string Texto { get; set; } = string.Empty;
}

internal sealed class ConfiguracaoGeracao
{
    [JsonPropertyName("responseMimeType")]
    public string? TipoMimeResposta { get; set; }

    [JsonPropertyName("responseSchema")]
    public EsquemaRespostaGemini? EsquemaResposta { get; set; }
}

internal sealed class EsquemaRespostaGemini
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = "OBJECT";

    [JsonPropertyName("properties")]
    public Dictionary<string, PropriedadeEsquema> Propriedades { get; set; } = [];

    [JsonPropertyName("required")]
    public List<string> Obrigatorios { get; set; } = [];
}

internal sealed class PropriedadeEsquema
{
    [JsonPropertyName("type")]
    public string Tipo { get; set; } = string.Empty;
}

internal sealed class RespostaGerarConteudo
{
    [JsonPropertyName("candidates")]
    public List<CandidatoGemini>? Candidatos { get; set; }
}

internal sealed class CandidatoGemini
{
    [JsonPropertyName("content")]
    public ConteudoGemini? Conteudo { get; set; }
}
