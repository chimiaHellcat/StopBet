using System.Net;
using System.Net.Sockets;
using StopBet.Core.Repositorios;

namespace StopBet;

public partial class MainPage : ContentPage
{
    private readonly IRepositorioDominioBloqueado _repositorio;

    public MainPage(IRepositorioDominioBloqueado repositorio)
    {
        InitializeComponent();
        _repositorio = repositorio;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CarregarDominiosAsync();
    }

    private async void OnAtualizarClicked(object? sender, EventArgs e)
    {
        await CarregarDominiosAsync();
    }

    private async Task CarregarDominiosAsync()
    {
        ContadorLabel.Text = "Carregando...";

        var dominios = await _repositorio.ObterTodosAsync();

        var itens = await Task.WhenAll(dominios.Select(async d => new ItemDominioExibicao
        {
            NomeDominio = d.NomeDominio,
            Categoria = d.Categoria,
            Origem = d.Origem.ToString(),
            Ip = await ResolverIpAsync(d.NomeDominio)
        }));

        DominiosCollectionView.ItemsSource = itens;
        ContadorLabel.Text = $"{itens.Length} dominio(s) em cache";
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

public sealed class ItemDominioExibicao
{
    public string NomeDominio { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Origem { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
}
