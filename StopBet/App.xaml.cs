using StopBet.Core.Dados;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;
using StopBet.Core.Servicos.ServidorLocal;

namespace StopBet;

public partial class App : Application
{
	private readonly IRepositorioDominioBloqueado _repositorio;
	private readonly SementeDominiosConhecidos _semente;
	private readonly SincronizadorBloqueio _sincronizadorBloqueio;
	private readonly IServidorLocal? _servidorLocal;

	public App(
		IRepositorioDominioBloqueado repositorio,
		SementeDominiosConhecidos semente,
		SincronizadorBloqueio sincronizadorBloqueio,
		IServidorLocal? servidorLocal = null)
	{
		InitializeComponent();
		_repositorio = repositorio;
		_semente = semente;
		_sincronizadorBloqueio = sincronizadorBloqueio;
		_servidorLocal = servidorLocal;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell())
		{
			Title = "StopBet",
			Width = 1100,
			Height = 750,
			MinimumWidth = 860,
			MinimumHeight = 560
		};
	}

	protected override async void OnStart()
	{
		base.OnStart();

		try
		{
			await _semente.PopularAsync(_repositorio);
			await _sincronizadorBloqueio.SincronizarAsync();
			_servidorLocal?.Iniciar();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Falha ao inicializar dominios bloqueados: {ex}");
		}
	}
}
