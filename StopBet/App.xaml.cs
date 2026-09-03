using StopBet.Core.Dados;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;

namespace StopBet;

public partial class App : Application
{
	private readonly IRepositorioDominioBloqueado _repositorio;
	private readonly SementeDominiosConhecidos _semente;
	private readonly SincronizadorBloqueio _sincronizadorBloqueio;

	public App(IRepositorioDominioBloqueado repositorio, SementeDominiosConhecidos semente, SincronizadorBloqueio sincronizadorBloqueio)
	{
		InitializeComponent();
		_repositorio = repositorio;
		_semente = semente;
		_sincronizadorBloqueio = sincronizadorBloqueio;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}

	protected override async void OnStart()
	{
		base.OnStart();

		try
		{
			await _semente.PopularAsync(_repositorio);
			await _sincronizadorBloqueio.SincronizarAsync();
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Falha ao inicializar dominios bloqueados: {ex}");
		}
	}
}
