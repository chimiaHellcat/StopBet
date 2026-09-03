using System.Diagnostics;
using System.Linq;
using System.Text;
using StopBet.Core.Servicos.Bloqueio;

namespace StopBet.WinUI;

// Bloqueia dominios editando o arquivo hosts do Windows, redirecionando para 127.0.0.1
// (mesma abordagem descrita no referencial teorico do TCC, secao 6.6). As entradas geridas
// pelo StopBet ficam isoladas entre marcadores, para nao mexer em nada que ja exista no
// arquivo. Escreve via arquivo temporario + File.Replace para evitar corromper o hosts
// se o processo for interrompido no meio da escrita. Requer o app rodando elevado
// (app.manifest com requireAdministrator) - sem isso, o acesso ao arquivo falha.
public sealed class ServicoBloqueioDominioWindows : IServicoBloqueioDominio
{
    private const string EnderecoBloqueio = "127.0.0.1";
    private const string MarcadorInicio = "# === StopBet: dominios bloqueados (gerenciado automaticamente, nao editar manualmente) ===";
    private const string MarcadorFim = "# === StopBet: fim ===";

    private static readonly string CaminhoHostsPadrao = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers", "etc", "hosts");

    private readonly string _caminhoHosts;
    private readonly SemaphoreSlim _semaforo = new(1, 1);

    public ServicoBloqueioDominioWindows(string? caminhoHosts = null)
    {
        _caminhoHosts = caminhoHosts ?? CaminhoHostsPadrao;
    }

    public async Task BloquearAsync(string dominio, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        await _semaforo.WaitAsync(cancellationToken);
        try
        {
            var linhas = await LerLinhasAsync(cancellationToken);
            var (indiceInicio, indiceFim, dominiosBloqueados) = ExtrairSecaoGerenciada(linhas);

            if (dominiosBloqueados.Any(d => string.Equals(d, dominio, StringComparison.OrdinalIgnoreCase)))
                return; // ja bloqueado - idempotente

            dominiosBloqueados.Add(dominio);
            var novasLinhas = ReconstruirArquivo(linhas, indiceInicio, indiceFim, dominiosBloqueados);
            await EscreverLinhasAsync(novasLinhas, cancellationToken);
        }
        finally
        {
            _semaforo.Release();
        }

        await FlushDnsAsync();
    }

    public async Task DesbloquearAsync(string dominio, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        await _semaforo.WaitAsync(cancellationToken);
        try
        {
            var linhas = await LerLinhasAsync(cancellationToken);
            var (indiceInicio, indiceFim, dominiosBloqueados) = ExtrairSecaoGerenciada(linhas);

            var removeu = dominiosBloqueados.RemoveAll(d => string.Equals(d, dominio, StringComparison.OrdinalIgnoreCase)) > 0;
            if (!removeu)
                return; // nao estava bloqueado - idempotente

            var novasLinhas = ReconstruirArquivo(linhas, indiceInicio, indiceFim, dominiosBloqueados);
            await EscreverLinhasAsync(novasLinhas, cancellationToken);
        }
        finally
        {
            _semaforo.Release();
        }

        await FlushDnsAsync();
    }

    private async Task<string[]> LerLinhasAsync(CancellationToken cancellationToken)
    {
        return File.Exists(_caminhoHosts)
            ? await File.ReadAllLinesAsync(_caminhoHosts, Encoding.UTF8, cancellationToken)
            : [];
    }

    private static (int indiceInicio, int indiceFim, List<string> dominios) ExtrairSecaoGerenciada(string[] linhas)
    {
        var indiceInicio = Array.IndexOf(linhas, MarcadorInicio);
        var indiceFim = Array.IndexOf(linhas, MarcadorFim);

        if (indiceInicio < 0 || indiceFim < 0 || indiceFim <= indiceInicio)
            return (-1, -1, []);

        var dominios = new List<string>();
        for (var i = indiceInicio + 1; i < indiceFim; i++)
        {
            var partes = linhas[i].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length == 2 && partes[0] == EnderecoBloqueio)
                dominios.Add(partes[1]);
        }

        return (indiceInicio, indiceFim, dominios);
    }

    private static List<string> ReconstruirArquivo(
        string[] linhasOriginais, int indiceInicio, int indiceFim, List<string> dominiosBloqueados)
    {
        // Sem dominios bloqueados: remove a secao gerenciada por completo (se existir),
        // sem deixar residuo de marcadores vazios no arquivo.
        if (dominiosBloqueados.Count == 0)
        {
            if (indiceInicio < 0)
                return [.. linhasOriginais];

            var semSecao = new List<string>();
            semSecao.AddRange(linhasOriginais[..indiceInicio]);
            semSecao.AddRange(linhasOriginais[(indiceFim + 1)..]);

            while (semSecao.Count > 0 && semSecao[^1].Length == 0)
                semSecao.RemoveAt(semSecao.Count - 1);

            return semSecao;
        }

        var secao = new List<string> { MarcadorInicio };
        secao.AddRange(dominiosBloqueados
            .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
            .Select(d => $"{EnderecoBloqueio} {d}"));
        secao.Add(MarcadorFim);

        var resultado = new List<string>();

        if (indiceInicio < 0)
        {
            resultado.AddRange(linhasOriginais);
            if (resultado.Count > 0 && resultado[^1].Length > 0)
                resultado.Add(string.Empty);
            resultado.AddRange(secao);
        }
        else
        {
            resultado.AddRange(linhasOriginais[..indiceInicio]);
            resultado.AddRange(secao);
            resultado.AddRange(linhasOriginais[(indiceFim + 1)..]);
        }

        return resultado;
    }

    private async Task EscreverLinhasAsync(List<string> linhas, CancellationToken cancellationToken)
    {
        var conteudo = string.Join(Environment.NewLine, linhas) + Environment.NewLine;
        var arquivoTemporario = _caminhoHosts + ".stopbet.tmp";

        await File.WriteAllTextAsync(arquivoTemporario, conteudo, Encoding.UTF8, cancellationToken);

        if (File.Exists(_caminhoHosts))
            File.Replace(arquivoTemporario, _caminhoHosts, null);
        else
            File.Move(arquivoTemporario, _caminhoHosts);
    }

    private static async Task FlushDnsAsync()
    {
        try
        {
            var psi = new ProcessStartInfo("ipconfig", "/flushdns")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var processo = Process.Start(psi);
            if (processo is not null)
                await processo.WaitForExitAsync();
        }
        catch
        {
            // flush de DNS e um "nice to have": falha aqui nao deve desfazer a alteracao ja salva no hosts.
        }
    }
}
