using StopBet.Core.Dados;
using StopBet.Core.Modelos;

namespace StopBet.Core.Repositorios;

public sealed class RepositorioDominioBloqueado : IRepositorioDominioBloqueado
{
    private readonly ProvedorConexaoSqlite _provedorConexao;

    public RepositorioDominioBloqueado(ProvedorConexaoSqlite provedorConexao)
    {
        _provedorConexao = provedorConexao;
    }

    public async Task<List<DominioBloqueado>> ObterTodosAsync()
    {
        var conexao = await _provedorConexao.ObterConexaoAsync();
        return await conexao.Table<DominioBloqueado>().ToListAsync();
    }

    public async Task<DominioBloqueado?> ObterPorIdAsync(int id)
    {
        var conexao = await _provedorConexao.ObterConexaoAsync();
        return await conexao.Table<DominioBloqueado>()
            .Where(d => d.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<DominioBloqueado?> ObterPorNomeDominioAsync(string nomeDominio)
    {
        var conexao = await _provedorConexao.ObterConexaoAsync();
        return await conexao.Table<DominioBloqueado>()
            .Where(d => d.NomeDominio == nomeDominio)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> ExisteAsync(string nomeDominio)
    {
        return await ObterPorNomeDominioAsync(nomeDominio) is not null;
    }

    public async Task<int> AdicionarAsync(DominioBloqueado dominio)
    {
        var conexao = await _provedorConexao.ObterConexaoAsync();
        return await conexao.InsertAsync(dominio);
    }

    public async Task<int> AtualizarAsync(DominioBloqueado dominio)
    {
        var conexao = await _provedorConexao.ObterConexaoAsync();
        return await conexao.UpdateAsync(dominio);
    }

    public async Task<int> RemoverAsync(int id)
    {
        var conexao = await _provedorConexao.ObterConexaoAsync();
        var dominio = await ObterPorIdAsync(id);
        return dominio is null ? 0 : await conexao.DeleteAsync(dominio);
    }
}
