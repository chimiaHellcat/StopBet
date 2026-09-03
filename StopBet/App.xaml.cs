using StopBet.Core.Dados;
using StopBet.Core.Repositorios;

namespace StopBet;

public partial class App : Application
{
	private readonly IRepositorioDominioBloqueado _repositorio;
	private readonly SementeDominiosConhecidos _semente;

	public App(IRepositorioDominioBloqueado repositorio, SementeDominiosConhecidos semente)
	{
		InitializeComponent();
		_repositorio = repositorio;
		_semente = semente;
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
		}
		catch (Exception ex)
		{
			System.Diagnostics.Debug.WriteLine($"Falha ao popular lista fixa de dominios: {ex}");
		}
	}
}
