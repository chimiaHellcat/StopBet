using System.Text.Json.Serialization;

namespace StopBet.Core.Servicos.Classificacao;

public sealed record ResultadoClassificacao(
    [property: JsonPropertyName("e_aposta")] bool EAposta,
    [property: JsonPropertyName("categoria")] string Categoria,
    [property: JsonPropertyName("confianca")] double Confianca);
