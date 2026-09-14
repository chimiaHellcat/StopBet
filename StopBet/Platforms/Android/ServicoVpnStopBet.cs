using Android.App;
using Android.Content;
using Android.Net;
using Android.OS;
using AndroidApp = Android.App;
using JavaIO = Java.IO;

namespace StopBet.Platforms.Android;

// VpnService responsavel por interceptar consultas DNS (UDP porta 53) e bloquear dominios
// conhecidos antes que o dispositivo consiga resolve-los - equivalente ao bloqueio via 'hosts'
// no Windows (StopBet/Platforms/Windows/ServicoBloqueioDominioWindows.cs), mas operando no
// nivel de pacote IP, unico mecanismo disponivel no Android sem root.
//
// Abordagem (deliberadamente minima para este prototipo de risco): NAO tunela todo o trafego
// do dispositivo - isso exigiria reimplementar NAT/roteamento completo, fora do escopo do que
// precisa ser provado aqui. Em vez disso, o StopBet se registra como servidor DNS do
// dispositivo (AddDnsServer) e so roteia para dentro da VPN o trafego destinado a esse
// "servidor" (AddRoute do proprio endereco) - na pratica, so consultas DNS passam por aqui; o
// resto do trafego (paginas, imagens, etc.) segue a rota normal, sem tocar na VPN.
//
// Mesma limitacao de DNS-over-HTTPS ja documentada no README para o bloqueio via hosts: apps e
// navegadores que usam DoH ignoram o DNS configurado no sistema e nao passam por aqui. E
// exatamente por isso que a extensao de navegador (extension/) existe - cobre essa lacuna nos
// navegadores baseados em Chromium. Este VpnService cobre o restante: apps nativos e qualquer
// coisa que respeite o DNS do sistema.
//
// Limitacao conhecida (aceita neste prototipo): o loop de pacotes processa uma consulta DNS por
// vez, de forma sequencial - se ConsultarUpstream() demorar (ate 3s de timeout), consultas
// seguintes ficam paradas ate ela terminar. Resolver isso exigiria despachar consultas ao
// upstream de forma assincrona e casar resposta<->pedido pelo ID da transacao DNS, o que fica
// para uma iteracao futura caso vire gargalo perceptivel no uso real.
[AndroidApp.Service(
    Permission = "android.permission.BIND_VPN_SERVICE",
    Exported = false,
    ForegroundServiceType = global::Android.Content.PM.ForegroundService.TypeSpecialUse)]
[AndroidApp.IntentFilter(["android.net.VpnService"])]
public sealed class ServicoVpnStopBet : VpnService
{
    private const string EnderecoTun = "10.55.0.2";
    private const int PortaDns = 53;

    // Resolvedor real para onde encaminhamos consultas de dominios NAO bloqueados.
    // Fixo por simplicidade no prototipo; poderia virar configuravel depois.
    private const string IpResolvedorUpstream = "1.1.1.1";

    private ParcelFileDescriptor? _tun;
    private CancellationTokenSource? _cts;

    public const string AcaoIniciar = "stopbet.vpn.INICIAR";
    public const string AcaoParar = "stopbet.vpn.PARAR";

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == AcaoParar)
        {
            PararVpn();
            return StartCommandResult.NotSticky;
        }

        IniciarVpn();
        return StartCommandResult.Sticky;
    }

    private void IniciarVpn()
    {
        if (_tun is not null) return; // ja rodando

        _tun = new Builder(this)
            .SetSession("StopBet")
            .AddAddress(EnderecoTun, 32)
            .AddDnsServer(EnderecoTun)
            .AddRoute(EnderecoTun, 32) // so o trafego destinado ao "servidor DNS" entra na VPN
            .Establish();

        if (_tun is null)
        {
            // Establish() retorna null se o usuario nao concedeu a permissao da VPN ainda
            // (ver VpnService.Prepare no MainActivity) - nao ha o que fazer aqui alem de parar.
            StopSelf();
            return;
        }

        IniciarNotificacaoForeground();

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _ = Task.Run(() => LoopDePacotes(token), token);
    }

    private void PararVpn()
    {
        _cts?.Cancel();
        try { _tun?.Close(); } catch { /* ja pode ter sido fechado */ }
        _tun = null;
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }

    public override void OnDestroy()
    {
        PararVpn();
        base.OnDestroy();
    }

    // ---- loop principal: le pacotes IP brutos do tun, filtra so DNS, decide bloquear ou encaminhar ----
    // Roda em background thread (Task.Run) porque JavaIO.FileInputStream.Read e bloqueante -
    // nao existe overload assincrono nativo para isso no binding Android.
    private void LoopDePacotes(CancellationToken cancellationToken)
    {
        if (_tun is null) return;

        using var entrada = new JavaIO.FileInputStream(_tun.FileDescriptor);
        using var saida = new JavaIO.FileOutputStream(_tun.FileDescriptor);
        var buffer = new byte[32767];

        while (!cancellationToken.IsCancellationRequested)
        {
            int lidos;
            try
            {
                lidos = entrada.Read(buffer, 0, buffer.Length);
            }
            catch (JavaIO.IOException)
            {
                break; // tun foi fechado (PararVpn ou app encerrado)
            }

            if (lidos <= 0) continue;

            try
            {
                ProcessarPacote(buffer, lidos, saida, cancellationToken);
            }
            catch
            {
                // um pacote malformado/inesperado nao pode derrubar o loop inteiro
            }
        }
    }

    private void ProcessarPacote(byte[] pacote, int tamanho, JavaIO.FileOutputStream saida, CancellationToken cancellationToken)
    {
        var ip = PacoteDns.TentarLerCabecalhoIPv4(pacote, tamanho);
        if (ip is null) return; // nao e IPv4 - ignora (IPv6 fica para depois)

        if (ip.Value.Protocolo != 17) return; // nao e UDP

        var udp = PacoteDns.TentarLerCabecalhoUdp(pacote, ip.Value.OffsetDados);
        if (udp is null || udp.Value.PortaDestino != PortaDns) return; // nao e consulta DNS

        var consulta = PacoteDns.TentarLerConsulta(pacote, udp.Value.OffsetDados, tamanho);
        if (consulta is null) return;

        System.Diagnostics.Debug.WriteLine($"[StopBet.Vpn] consulta DNS: {consulta.Value.Dominio}");

        var bloqueado = ServicoBloqueioDominioAndroid.EstaBloqueado(consulta.Value.Dominio);

        byte[] corpoResposta;
        if (bloqueado)
        {
            corpoResposta = PacoteDns.MontarRespostaBloqueio(pacote, udp.Value.OffsetDados, consulta.Value.TamanhoConsulta);
        }
        else
        {
            corpoResposta = ConsultarUpstream(pacote, udp.Value.OffsetDados, consulta.Value.TamanhoConsulta);
            if (corpoResposta.Length == 0) return; // upstream falhou/expirou - sem resposta, o solicitante tenta de novo
        }

        var pacoteResposta = PacoteDns.MontarPacoteResposta(ip.Value, udp.Value, corpoResposta);
        saida.Write(pacoteResposta);
    }

    private byte[] ConsultarUpstream(byte[] pacote, int offsetConsultaDns, int tamanhoConsulta)
    {
        using var socket = new System.Net.Sockets.Socket(
            System.Net.Sockets.AddressFamily.InterNetwork,
            System.Net.Sockets.SocketType.Dgram,
            System.Net.Sockets.ProtocolType.Udp);

        // Essencial: sem isso, o pacote de saida deste socket volta a entrar na propria VPN,
        // causando um loop infinito (o StopBet acabaria perguntando pra si mesmo).
        Protect((int)socket.Handle);

        try
        {
            socket.SendTimeout = 3000;
            socket.ReceiveTimeout = 3000;
            socket.Connect(IpResolvedorUpstream, PortaDns);
            socket.Send(pacote, offsetConsultaDns, tamanhoConsulta, System.Net.Sockets.SocketFlags.None);

            var respostaBuffer = new byte[4096];
            var lidos = socket.Receive(respostaBuffer);
            return respostaBuffer[..lidos];
        }
        catch
        {
            return [];
        }
    }

    private void IniciarNotificacaoForeground()
    {
        const string canalId = "stopbet_vpn";
        var manager = (AndroidApp.NotificationManager)GetSystemService(NotificationService)!;

        if (manager.GetNotificationChannel(canalId) is null)
        {
            var canal = new AndroidApp.NotificationChannel(canalId, "Protecao StopBet", AndroidApp.NotificationImportance.Low);
            manager.CreateNotificationChannel(canal);
        }

        var notificacao = new AndroidApp.Notification.Builder(this, canalId)
            .SetContentTitle("StopBet ativo")
            .SetContentText("Bloqueando o acesso a sites de apostas")
            .SetSmallIcon(global::Android.Resource.Drawable.IcLockLock)
            .SetOngoing(true)
            .Build();

        StartForeground(1, notificacao);
    }
}
