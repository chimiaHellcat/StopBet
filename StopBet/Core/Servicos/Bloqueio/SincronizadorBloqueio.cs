using StopBet.Core.Repositorios;

namespace StopBet.Core.Servicos.Bloqueio;

// Fecha o gap entre "sabido" (dominio existe no repositorio local) e "aplicado"
// (efetivamente bloqueado na plataforma atual - hosts no Windows, VpnService no
// Android quando existir). Chamado na inicializacao do app para garantir que tudo
// que ja esta na lista local seja imediatamente aplicado, sem depender de o usuario
// tentar acessar o dominio primeiro.
public sealed class SincronizadorBloqueio
{
    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoBloqueioDominio? _servicoBloqueio;

    public SincronizadorBloqueio(IRepositorioDominioBloqueado repositorio, IServicoBloqueioDominio? servicoBloqueio = null)
    {
        _repositorio = repositorio;
        _servicoBloqueio = servicoBloqueio;
    }

    public async Task SincronizarAsync(CancellationToken cancellationToken = default)
    {
        if (_servicoBloqueio is null)
            return; // nenhum mecanismo de bloqueio disponivel ainda nesta plataforma

        var todos = await _repositorio.ObterTodosAsync();
        foreach (var dominio in todos)
            await _servicoBloqueio.BloquearAsync(dominio.NomeDominio, cancellationToken);
    }
}
