using Microsoft.Extensions.Logging;
using StopBet.Core.Dados;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;
using StopBet.Core.Servicos.Classificacao;
using StopBet.Core.Servicos.Verificacao;

namespace StopBet;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		builder.Services.AddSingleton<ProvedorConexaoSqlite>();
		builder.Services.AddSingleton<IRepositorioDominioBloqueado, RepositorioDominioBloqueado>();
		builder.Services.AddSingleton<SementeDominiosConhecidos>();
		builder.Services.AddSingleton<IProvedorChaveApi, ProvedorChaveApiVariavelAmbiente>();
		builder.Services.AddSingleton<ExtratorMetadadosHtml>();
		builder.Services.AddHttpClient<IServicoClassificacaoUrl, ServicoClassificacaoUrlGemini>();
		builder.Services.AddSingleton<IServicoVerificacaoDominio, ServicoVerificacaoDominio>();

#if WINDOWS
		builder.Services.AddSingleton<IServicoBloqueioDominio, StopBet.WinUI.ServicoBloqueioDominioWindows>();
#endif

		builder.Services.AddTransient<MainPage>();

		return builder.Build();
	}
}
