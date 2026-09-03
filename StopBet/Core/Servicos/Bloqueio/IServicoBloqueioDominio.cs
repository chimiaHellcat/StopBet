namespace StopBet.Core.Servicos.Bloqueio;

// Contrato platform-agnostico para o mecanismo de bloqueio efetivo de um dominio.
// Implementado de forma especifica por plataforma: edicao do hosts no Windows,
// VpnService no Android (pendente).
public interface IServicoBloqueioDominio
{
    Task BloquearAsync(string dominio, CancellationToken cancellationToken = default);

    Task DesbloquearAsync(string dominio, CancellationToken cancellationToken = default);
}
