using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StopBet.Core.Modelos;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;

namespace StopBet;

public partial class DominiosPage : ContentPage
{
    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoBloqueioDominio? _servicoBloqueio;
    private List<ItemDominioExibicao> _todos = [];

    public DominiosPage(IRepositorioDominioBloqueado repositorio, IServicoBloqueioDominio? servicoBloqueio = null)
    {
        InitializeComponent();
        _repositorio = repositorio;
        _servicoBloqueio = servicoBloqueio;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarDominiosAsync();
    }

    private async Task CarregarDominiosAsync()
    {
        ContadorLabel.Text = "Carregando...";

        var dominios = await _repositorio.ObterTodosAsync();

        _todos = dominios
            .OrderByDescending(d => d.DataInclusao)
            .Select(CriarItem)
            .ToList();

        AplicarFiltro(BuscaSearchBar.Text);
        _ = ResolverIpsEmSegundoPlanoAsync(_todos);
    }

    private ItemDominioExibicao CriarItem(DominioBloqueado d) => new()
    {
        Id = d.Id,
        NomeDominio = d.NomeDominio,
        Categoria = d.Categoria,
        OrigemEnum = d.Origem,
        DataInclusao = d.DataInclusao,
        Ip = "resolvendo...",
        RemoverCommand = new Command(async () => await RemoverDominioAsync(d.Id, d.NomeDominio))
    };

    private void AplicarFiltro(string? termo)
    {
        var filtrados = string.IsNullOrWhiteSpace(termo)
            ? _todos
            : _todos.Where(i =>
                i.NomeDominio.Contains(termo, StringComparison.OrdinalIgnoreCase) ||
                i.Categoria.Contains(termo, StringComparison.OrdinalIgnoreCase))
              .ToList();

        DominiosCollectionView.ItemsSource = filtrados;
        ContadorLabel.Text = $"{_todos.Count} dominio(s) em cache" +
            (filtrados.Count != _todos.Count ? $" - {filtrados.Count} exibido(s)" : string.Empty);
    }

    private void OnBuscaTextChanged(object? sender, TextChangedEventArgs e) => AplicarFiltro(e.NewTextValue);

    private async void OnAdicionarClicked(object? sender, EventArgs e)
    {
        await Navigation.PushModalAsync(new NavigationPage(new AdicionarDominioPage(_repositorio, _servicoBloqueio)));
    }

    private async Task RemoverDominioAsync(int id, string nomeDominio)
    {
        var confirmar = await DisplayAlertAsync(
            "Remover dominio",
            $"Remover \"{nomeDominio}\" da lista de bloqueio? O site voltara a ficar acessivel.",
            "Remover",
            "Cancelar");

        if (!confirmar)
            return;

        await _repositorio.RemoverAsync(id);

        if (_servicoBloqueio is not null)
            await _servicoBloqueio.DesbloquearAsync(nomeDominio);

        await CarregarDominiosAsync();
    }

    private static async Task ResolverIpsEmSegundoPlanoAsync(List<ItemDominioExibicao> itens)
    {
        using var limitador = new SemaphoreSlim(5);

        await Task.WhenAll(itens.Select(async item =>
        {
            await limitador.WaitAsync();
            try
            {
                item.Ip = await ResolverIpAsync(item.NomeDominio);
            }
            finally
            {
                limitador.Release();
            }
        }));
    }

    private static async Task<string> ResolverIpAsync(string nomeDominio)
    {
        try
        {
            var enderecos = await Dns.GetHostAddressesAsync(nomeDominio);
            var ipv4 = enderecos.FirstOrDefault(e => e.AddressFamily == AddressFamily.InterNetwork);
            return (ipv4 ?? enderecos.FirstOrDefault())?.ToString() ?? "nao resolvido";
        }
        catch
        {
            return "nao resolvido";
        }
    }
}

public sealed class ItemDominioExibicao : INotifyPropertyChanged
{
    private string _ip = string.Empty;

    public int Id { get; set; }
    public string NomeDominio { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public OrigemDominio OrigemEnum { get; set; }
    public DateTime DataInclusao { get; set; }
    public ICommand RemoverCommand { get; set; } = null!;

    public string Origem => OrigemEnum == OrigemDominio.ListaFixa ? "Lista fixa" : "Classificado por IA";

    public Color CorOrigem => OrigemEnum == OrigemDominio.ListaFixa
        ? Color.FromArgb("#6366F1")
        : Color.FromArgb("#FBBF24");

    public string DataInclusaoTexto => DataInclusao.ToLocalTime().ToString("dd/MM/yyyy");

    public string Ip
    {
        get => _ip;
        set
        {
            if (_ip == value) return;
            _ip = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? nome = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nome));
}
