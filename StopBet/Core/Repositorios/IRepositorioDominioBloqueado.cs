using StopBet.Core.Modelos;

namespace StopBet.Core.Repositorios;

public interface IRepositorioDominioBloqueado
{
    Task<List<DominioBloqueado>> ObterTodosAsync();

    Task<DominioBloqueado?> ObterPorIdAsync(int id);

    Task<DominioBloqueado?> ObterPorNomeDominioAsync(string nomeDominio);

    Task<bool> ExisteAsync(string nomeDominio);

    Task<int> AdicionarAsync(DominioBloqueado dominio);

    Task<int> AtualizarAsync(DominioBloqueado dominio);

    Task<int> RemoverAsync(int id);
}
