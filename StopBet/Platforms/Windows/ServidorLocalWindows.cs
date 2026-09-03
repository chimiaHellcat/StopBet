using System.Net;
using System.Text;
using System.Text.Json;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.ServidorLocal;
using StopBet.Core.Servicos.Verificacao;

namespace StopBet.WinUI;

// Ponte HTTP local (127.0.0.1, sem exposicao na rede) entre a extensao de navegador
// e o "cerebro" de decisao do app. Expoe:
//   GET /lista                    -> todos os dominios ja conhecidos, para a extensao
//                                     pre-carregar regras de bloqueio ao iniciar
//   GET /verificar?dominio=X      -> classifica um dominio desconhecido em tempo real
//                                     (chama ServicoVerificacaoDominio, que usa o Gemini)
public sealed class ServidorLocalWindows : IServidorLocal, IDisposable
{
    private const int Porta = 5127;

    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoVerificacaoDominio _servicoVerificacao;
    private readonly HttpListener _listener = new();
    private CancellationTokenSource? _cts;

    public ServidorLocalWindows(IRepositorioDominioBloqueado repositorio, IServicoVerificacaoDominio servicoVerificacao)
    {
        _repositorio = repositorio;
        _servicoVerificacao = servicoVerificacao;
        _listener.Prefixes.Add($"http://127.0.0.1:{Porta}/");
    }

    public void Iniciar()
    {
        if (_listener.IsListening)
            return;

        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = ProcessarRequisicoesAsync(_cts.Token);
    }

    public void Parar()
    {
        _cts?.Cancel();
        if (_listener.IsListening)
            _listener.Stop();
    }

    private async Task ProcessarRequisicoesAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener.IsListening)
        {
            HttpListenerContext contexto;
            try
            {
                contexto = await _listener.GetContextAsync();
            }
            catch when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                continue;
            }

            _ = TratarRequisicaoAsync(contexto, cancellationToken);
        }
    }

    private async Task TratarRequisicaoAsync(HttpListenerContext contexto, CancellationToken cancellationToken)
    {
        var resposta = contexto.Response;
        resposta.Headers.Add("Access-Control-Allow-Origin", "*");
        resposta.ContentType = "application/json; charset=utf-8";

        try
        {
            var caminho = contexto.Request.Url?.AbsolutePath ?? string.Empty;

            object corpo = caminho switch
            {
                "/lista" => await TratarListaAsync(),
                "/verificar" => await TratarVerificarAsync(contexto.Request.QueryString["dominio"], cancellationToken),
                _ => new { erro = "rota desconhecida" }
            };

            var json = JsonSerializer.Serialize(corpo);
            var bytes = Encoding.UTF8.GetBytes(json);
            resposta.ContentLength64 = bytes.Length;
            await resposta.OutputStream.WriteAsync(bytes, cancellationToken);
        }
        catch (Exception ex)
        {
            resposta.StatusCode = 500;
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { erro = ex.Message }));
            await resposta.OutputStream.WriteAsync(bytes, cancellationToken);
        }
        finally
        {
            resposta.OutputStream.Close();
        }
    }

    private async Task<object> TratarListaAsync()
    {
        var todos = await _repositorio.ObterTodosAsync();
        return new { dominios = todos.Select(d => d.NomeDominio).ToArray() };
    }

    private async Task<object> TratarVerificarAsync(string? dominio, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(dominio))
            return new { erro = "parametro 'dominio' e obrigatorio" };

        var resultado = await _servicoVerificacao.VerificarDominioAsync(dominio, null, cancellationToken);
        return new
        {
            bloquear = resultado.DeveBloquear,
            categoria = resultado.Categoria,
            origem = resultado.Origem.ToString()
        };
    }

    public void Dispose()
    {
        Parar();
        _listener.Close();
    }
}
