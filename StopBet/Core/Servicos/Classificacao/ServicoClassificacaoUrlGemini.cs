using System.Net.Http.Json;
using System.Text.Json;
using StopBet.Core.Servicos.Classificacao.Gemini;

namespace StopBet.Core.Servicos.Classificacao;

public sealed class ServicoClassificacaoUrlGemini : IServicoClassificacaoUrl
{
    private const string Modelo = "gemini-3.5-flash-lite";
    private const string UrlBase = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly HttpClient _httpClient;
    private readonly IProvedorChaveApi _provedorChaveApi;
    private readonly ExtratorMetadadosHtml _extratorMetadados;

    public ServicoClassificacaoUrlGemini(
        HttpClient httpClient,
        IProvedorChaveApi provedorChaveApi,
        ExtratorMetadadosHtml extratorMetadados)
    {
        _httpClient = httpClient;
        _provedorChaveApi = provedorChaveApi;
        _extratorMetadados = extratorMetadados;
    }

    // Numero de tentativas extras (alem da primeira) para erros transitorios da API
    // (429 rate limit, 503 sobrecarga) - descobertos ao testar varios dominios em
    // sequencia rapida: sem retry, esses erros derrubavam a verificacao silenciosamente
    // (a extensao so loga um aviso e o dominio fica sem bloquear, sem nenhum erro visivel).
    private const int MaximoTentativas = 4;

    public async Task<ResultadoClassificacao> ClassificarAsync(string dominio, string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        var chaveApi = _provedorChaveApi.ObterChave();
        if (string.IsNullOrWhiteSpace(chaveApi))
            throw new InvalidOperationException("Chave da API do Gemini nao configurada (GEMINI_API_KEY).");

        var metadados = _extratorMetadados.Extrair(html);
        var requisicao = MontarRequisicao(dominio, metadados);
        var url = $"{UrlBase}/{Modelo}:generateContent?key={chaveApi}";

        for (var tentativa = 1; ; tentativa++)
        {
            using var resposta = await _httpClient.PostAsJsonAsync(url, requisicao, cancellationToken);

            var transitorio = resposta.StatusCode is System.Net.HttpStatusCode.TooManyRequests
                or System.Net.HttpStatusCode.ServiceUnavailable;

            if (transitorio && tentativa < MaximoTentativas)
            {
                var espera = resposta.Headers.RetryAfter?.Delta
                    ?? TimeSpan.FromSeconds(Math.Pow(2, tentativa)); // 2s, 4s, 8s...
                await Task.Delay(espera, cancellationToken);
                continue;
            }

            resposta.EnsureSuccessStatusCode();

            var corpoResposta = await resposta.Content.ReadFromJsonAsync<RespostaGerarConteudo>(cancellationToken: cancellationToken);

            var textoJson = corpoResposta?.Candidatos?.FirstOrDefault()?.Conteudo?.Partes?.FirstOrDefault()?.Texto;
            if (string.IsNullOrWhiteSpace(textoJson))
                throw new InvalidOperationException("A API do Gemini nao retornou conteudo classificavel.");

            var resultado = JsonSerializer.Deserialize<ResultadoClassificacao>(textoJson);
            if (resultado is null)
                throw new InvalidOperationException("Nao foi possivel interpretar a resposta da API do Gemini.");

            return resultado;
        }
    }

    private static RequisicaoGerarConteudo MontarRequisicao(string dominio, MetadadosPagina metadados)
    {
        var titulo = metadados.Titulo ?? "(nao disponivel)";
        var metaDescricao = metadados.MetaDescricao ?? "(nao disponivel)";

        var prompt =
            "Voce e um classificador de dominios especializado em identificar casas de apostas, " +
            "cassinos online e plataformas de jogos de azar.\n\n" +
            $"Dominio: {dominio}\n" +
            $"Titulo da pagina: {titulo}\n" +
            $"Meta descricao: {metaDescricao}\n\n" +
            "Com base nessas informacoes, determine se o dominio pertence a uma casa de apostas, " +
            "cassino online ou plataforma de apostas/jogos de azar. Responda apenas com o JSON " +
            "solicitado no schema.";

        return new RequisicaoGerarConteudo
        {
            Conteudos =
            [
                new ConteudoGemini
                {
                    Partes = [ new ParteConteudo { Texto = prompt } ]
                }
            ],
            ConfiguracaoGeracao = new ConfiguracaoGeracao
            {
                TipoMimeResposta = "application/json",
                EsquemaResposta = new EsquemaRespostaGemini
                {
                    Tipo = "OBJECT",
                    Propriedades = new Dictionary<string, PropriedadeEsquema>
                    {
                        ["e_aposta"] = new PropriedadeEsquema { Tipo = "BOOLEAN" },
                        ["categoria"] = new PropriedadeEsquema { Tipo = "STRING" },
                        ["confianca"] = new PropriedadeEsquema { Tipo = "NUMBER" }
                    },
                    Obrigatorios = ["e_aposta", "categoria", "confianca"]
                }
            }
        };
    }
}
