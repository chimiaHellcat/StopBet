using Android.Content;

namespace StopBet.Platforms.Android;

// Implementacao Android do contrato de bloqueio (StopBet.Core.Servicos.Bloqueio.IServicoBloqueioDominio),
// equivalente ao ServicoBloqueioDominioWindows. Aqui "bloquear um dominio" nao edita um arquivo -
// atualiza a lista em memoria que o VpnService (ServicoVpnStopBet) consulta a cada pacote DNS
// interceptado, e garante que a VPN esteja rodando.
//
// Prototipo (Semana "risco critico" do plano): a lista comeca vazia e so cresce via BloquearAsync.
// Proximo passo depois de validar que a interceptacao funciona: carregar a lista a partir do
// IRepositorioDominioBloqueado (mesma fonte que Windows/extensao usam) na inicializacao, em vez
// de depender so de chamadas futuras - assim os tres mecanismos ficam consistentes desde o boot.
public sealed class ServicoBloqueioDominioAndroid : StopBet.Core.Servicos.Bloqueio.IServicoBloqueioDominio
{
    private static readonly HashSet<string> _dominiosBloqueados = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _trava = new();

    public static bool EstaBloqueado(string dominio)
    {
        lock (_trava)
        {
            if (_dominiosBloqueados.Contains(dominio)) return true;

            // mesma logica de variante www. do ServicoBloqueioDominioWindows
            return dominio.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
                ? _dominiosBloqueados.Contains(dominio["www.".Length..])
                : _dominiosBloqueados.Contains($"www.{dominio}");
        }
    }

    public Task BloquearAsync(string dominio, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        lock (_trava) { _dominiosBloqueados.Add(dominio); }
        GarantirVpnRodando();

        return Task.CompletedTask;
    }

    public Task DesbloquearAsync(string dominio, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        lock (_trava) { _dominiosBloqueados.Remove(dominio); }

        return Task.CompletedTask;
    }

    private static void GarantirVpnRodando()
    {
        var contexto = global::Android.App.Application.Context;
        var intent = new Intent(contexto, typeof(ServicoVpnStopBet)).SetAction(ServicoVpnStopBet.AcaoIniciar);

        // StartForegroundService (nao StartService): a partir do Android 8, um servico que vai
        // se promover a foreground (ServicoVpnStopBet chama StartForeground logo depois de
        // Establish()) precisa ser iniciado assim - com StartService(), se BloquearAsync for
        // chamado com o app em background, o sistema lanca IllegalStateException.
        contexto.StartForegroundService(intent);
    }
}
