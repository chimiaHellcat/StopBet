using StopBet.Core.Modelos;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;

namespace StopBet;

public partial class AdicionarDominioPage : ContentPage
{
    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoBloqueioDominio? _servicoBloqueio;

    public AdicionarDominioPage(IRepositorioDominioBloqueado repositorio, IServicoBloqueioDominio? servicoBloqueio = null)
    {
        InitializeComponent();
        _repositorio = repositorio;
        _servicoBloqueio = servicoBloqueio;
    }

    private async void OnAdicionarClicked(object? sender, EventArgs e)
    {
        var dominio = NormalizarDominio(DominioEntry.Text);
        var categoria = string.IsNullOrWhiteSpace(CategoriaEntry.Text) ? "Casa de Apostas" : CategoriaEntry.Text.Trim();

        if (string.IsNullOrWhiteSpace(dominio) || !dominio.Contains('.'))
        {
            MostrarErro("Informe um dominio valido (ex: exemplo.bet.br).");
            return;
        }

        AdicionarBtn.IsEnabled = false;
        AdicionarBtn.Text = "Adicionando...";

        try
        {
            if (await _repositorio.ExisteAsync(dominio))
            {
                MostrarErro("Esse dominio ja esta na lista de bloqueio.");
                return;
            }

            await _repositorio.AdicionarAsync(new DominioBloqueado
            {
                NomeDominio = dominio,
                Categoria = categoria,
                Origem = OrigemDominio.ListaFixa,
                DataInclusao = DateTime.UtcNow
            });

            if (_servicoBloqueio is not null)
                await _servicoBloqueio.BloquearAsync(dominio);

            await Navigation.PopModalAsync();
        }
        finally
        {
            AdicionarBtn.IsEnabled = true;
            AdicionarBtn.Text = "Adicionar";
        }
    }

    // Aceita tanto "exemplo.bet.br" quanto uma URL completa colada
    // ("https://exemplo.bet.br/pagina?x=1") - extrai so o hostname nesse caso,
    // senao o valor seria salvo como esta e nunca bateria com nada de verdade
    // (nem hosts, nem a extensao reconhecem uma URL inteira como dominio).
    private static string NormalizarDominio(string? texto)
    {
        var valor = texto?.Trim() ?? string.Empty;
        if (valor.Length == 0) return valor;

        if (!valor.Contains("://", StringComparison.Ordinal))
            valor = "http://" + valor;

        return Uri.TryCreate(valor, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host)
            ? uri.Host.ToLowerInvariant()
            : texto?.Trim().ToLowerInvariant() ?? string.Empty;
    }

    private void MostrarErro(string mensagem)
    {
        ErroLabel.Text = mensagem;
        ErroLabel.IsVisible = true;
    }

    private async void OnCancelarClicked(object? sender, EventArgs e)
    {
        await Navigation.PopModalAsync();
    }
}
