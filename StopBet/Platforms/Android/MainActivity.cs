using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidNet = Android.Net;

namespace StopBet;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int CodigoRequisicaoPermissaoVpn = 1000;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SolicitarPermissaoVpnSeNecessario();
    }

    // VpnService.Prepare devolve um Intent quando o usuario ainda nao autorizou o StopBet a
    // atuar como VPN neste dispositivo (ou quando outro app VPN esta ativo e precisa ceder
    // lugar) - nesse caso e preciso mostrar o dialogo de confirmacao do sistema via
    // StartActivityForResult. Se devolver null, a permissao ja foi concedida antes e nao ha
    // nada a fazer; o ServicoVpnStopBet.Establish() vai funcionar normalmente quando chamado.
    private void SolicitarPermissaoVpnSeNecessario()
    {
        var intentPermissao = AndroidNet.VpnService.Prepare(this);
        if (intentPermissao is not null)
        {
            StartActivityForResult(intentPermissao, CodigoRequisicaoPermissaoVpn);
        }
    }
}
