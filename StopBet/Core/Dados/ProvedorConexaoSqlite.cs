using Microsoft.Maui.Storage;
using SQLite;
using StopBet.Core.Modelos;

namespace StopBet.Core.Dados;

public sealed class ProvedorConexaoSqlite
{
    private const string NomeArquivoBanco = "stopbet.db3";

    private readonly SemaphoreSlim _semaforoInicializacao = new(1, 1);
    private SQLiteAsyncConnection? _conexao;

    public async Task<SQLiteAsyncConnection> ObterConexaoAsync()
    {
        if (_conexao is not null)
            return _conexao;

        await _semaforoInicializacao.WaitAsync();
        try
        {
            if (_conexao is not null)
                return _conexao;

            var caminhoBanco = Path.Combine(FileSystem.AppDataDirectory, NomeArquivoBanco);
            var conexao = new SQLiteAsyncConnection(caminhoBanco);
            await conexao.CreateTableAsync<DominioBloqueado>();

            _conexao = conexao;
            return _conexao;
        }
        finally
        {
            _semaforoInicializacao.Release();
        }
    }
}
