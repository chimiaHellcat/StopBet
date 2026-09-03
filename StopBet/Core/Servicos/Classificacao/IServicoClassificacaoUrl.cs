namespace StopBet.Core.Servicos.Classificacao;

public interface IServicoClassificacaoUrl
{
    Task<ResultadoClassificacao> ClassificarAsync(string dominio, string html, CancellationToken cancellationToken = default);
}
