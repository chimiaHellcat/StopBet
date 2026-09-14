using StopBet.Core.Modelos;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;
using StopBet.Core.Servicos.ServidorLocal;
using StopBet.Core.Servicos.Verificacao;

namespace StopBet;

public partial class PainelPage : ContentPage
{
    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoBloqueioDominio? _servicoBloqueio;
    private readonly IServidorLocal? _servidorLocal;
    private readonly EstadoPausaVerificacao _estadoPausa;

    public PainelPage(
        IRepositorioDominioBloqueado repositorio,
        EstadoPausaVerificacao estadoPausa,
        IServicoBloqueioDominio? servicoBloqueio = null,
        IServidorLocal? servidorLocal = null)
    {
        InitializeComponent();
        _repositorio = repositorio;
        _estadoPausa = estadoPausa;
        _servicoBloqueio = servicoBloqueio;
        _servidorLocal = servidorLocal;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        AtualizarSecaoPausa();
        await AtualizarAsync();
    }

    private async Task AtualizarAsync()
    {
        var dominios = await _repositorio.ObterTodosAsync();

        TotalLabel.Text = dominios.Count.ToString();
        ListaFixaLabel.Text = dominios.Count(d => d.Origem == OrigemDominio.ListaFixa).ToString();
        IaLabel.Text = dominios.Count(d => d.Origem == OrigemDominio.ClassificadoPorIA).ToString();

        CamadasStack.Children.Clear();

        AdicionarLinhaCamada(
            "Lista local (SQLite)",
            "Cache consultado antes de qualquer chamada a IA - sempre disponivel",
            ativo: true,
            ultima: false);

        AdicionarLinhaCamada(
            "Bloqueio via hosts (Windows)",
            _servicoBloqueio is not null
                ? "Redireciona dominios bloqueados para 127.0.0.1 no arquivo hosts"
                : "Indisponivel nesta plataforma",
            ativo: _servicoBloqueio is not null,
            ultima: false);

        AdicionarLinhaCamada(
            "Servidor local (ponte com a extensao)",
            _servidorLocal is null
                ? "Indisponivel nesta plataforma"
                : _servidorLocal.EstaAtivo
                    ? "Ativo em http://127.0.0.1:5127 - a extensao consulta o cerebro de decisao em tempo real"
                    : "Nao esta rodando",
            ativo: _servidorLocal?.EstaAtivo ?? false,
            ultima: true);
    }

    private void AdicionarLinhaCamada(string titulo, string descricao, bool ativo, bool ultima)
    {
        var linha = new Grid
        {
            Padding = new Thickness(18, 14),
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star)
            }
        };

        var bolinha = new BoxView
        {
            WidthRequest = 10,
            HeightRequest = 10,
            CornerRadius = 5,
            Color = ativo
                ? (Color)Application.Current!.Resources["StopBetSucesso"]
                : (Color)Application.Current!.Resources["StopBetTextoMuted"],
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 5, 12, 0)
        };
        linha.Add(bolinha, 0, 0);

        var textos = new VerticalStackLayout { Spacing = 2 };
        textos.Add(new Label { Text = titulo, FontAttributes = FontAttributes.Bold });
        textos.Add(new Label { Text = descricao, Style = (Style)Application.Current!.Resources["SubHeadline"], FontSize = 12 });
        linha.Add(textos, 1, 0);

        CamadasStack.Add(linha);

        if (!ultima)
        {
            CamadasStack.Add(new BoxView
            {
                HeightRequest = 1,
                Color = (Color)Application.Current!.Resources["StopBetBordaCartao"]
            });
        }
    }

    private void AtualizarSecaoPausa()
    {
        if (_estadoPausa.Pausado)
        {
            PausaBolinha.Color = (Color)Application.Current!.Resources["StopBetAlerta"];
            PausaTituloLabel.Text = "Verificacao de dominios novos pausada";
            PausaDescricaoLabel.Text = "Dominios ja bloqueados continuam protegidos, mas dominios novos nao estao sendo checados. Lembre-se de retomar depois.";
            PausaBorder.Stroke = (Color)Application.Current!.Resources["StopBetAlerta"];
            PausarBtn.Text = "Retomar";
        }
        else
        {
            PausaBolinha.Color = (Color)Application.Current!.Resources["StopBetSucesso"];
            PausaTituloLabel.Text = "Verificacao de dominios novos ativa";
            PausaDescricaoLabel.Text = "Dominios ja bloqueados continuam protegidos. Pausar so afeta a checagem de dominios novos (util pra navegar sem interrupcoes por um tempo).";
            PausaBorder.Stroke = (Color)Application.Current!.Resources["StopBetBordaCartao"];
            PausarBtn.Text = "Pausar";
        }
    }

    private void OnPausarClicked(object? sender, EventArgs e)
    {
        _estadoPausa.Pausado = !_estadoPausa.Pausado;
        AtualizarSecaoPausa();
    }

    private async void OnGerenciarDominiosClicked(object? sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("//Dominios");
    }

    private async void OnAtualizarClicked(object? sender, EventArgs e)
    {
        // AtualizarAsync sozinho e rapido demais (SQLite local) pra dar qualquer
        // feedback visual de que o clique fez algo - se os numeros nao mudarem
        // (o caso mais comum), parece que o botao nao fez nada. Por isso o texto
        // muda visivelmente em vez de só re-renderizar em silencio.
        AtualizarBtn.IsEnabled = false;
        AtualizarBtn.Text = "Atualizando...";

        await AtualizarAsync();

        AtualizarBtn.Text = "Atualizado";
        await Task.Delay(800);

        AtualizarBtn.Text = "Atualizar";
        AtualizarBtn.IsEnabled = true;
    }
}
