namespace StopBet.Core.Servicos.Verificacao;

public interface IServicoVerificacaoDominio
{
    Task<ResultadoVerificacaoDominio> VerificarDominioAsync(string dominio, string? html, CancellationToken cancellationToken = default);
}
