using System.Diagnostics;
using System.IO.Compression;
using StopBet.Core.Servicos.ServidorLocal;

namespace StopBet;

public partial class ExtensaoPage : ContentPage
{
    private readonly IServidorLocal? _servidorLocal;
    private string? _caminhoExtensao;

    public ExtensaoPage(IServidorLocal? servidorLocal = null)
    {
        InitializeComponent();
        _servidorLocal = servidorLocal;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        var ativo = _servidorLocal?.EstaAtivo ?? false;
        StatusBolinha.Color = ativo ? (Color)Application.Current!.Resources["StopBetSucesso"] : (Color)Application.Current!.Resources["StopBetPerigo"];
        StatusLabel.Text = ativo
            ? "Servidor local ativo em http://127.0.0.1:5127 - a extensao pode se conectar"
            : "Servidor local indisponivel - a extensao nao vai conseguir verificar dominios novos";

        _caminhoExtensao = LocalizarPastaExtensao();
        CaminhoPastaLabel.Text = _caminhoExtensao ?? "Pasta 'extension' nao encontrada automaticamente - veja o README do repositorio";
    }

    // Primeiro procura a copia empacotada junto do executavel (StopBet.csproj copia
    // extension/ pra la em todo build) - funciona em qualquer instalacao, nao so no
    // ambiente de desenvolvimento. Se nao achar (build antigo sem o empacotamento),
    // cai pra busca subindo diretorios a partir do executavel, procurando o repositorio.
    private static string? LocalizarPastaExtensao()
    {
        var candidatoEmpacotado = Path.Combine(AppContext.BaseDirectory, "extension");
        if (File.Exists(Path.Combine(candidatoEmpacotado, "manifest.json")))
            return candidatoEmpacotado;

        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        for (var i = 0; i < 8 && diretorio is not null; i++, diretorio = diretorio.Parent)
        {
            var candidato = Path.Combine(diretorio.FullName, "extension");
            if (File.Exists(Path.Combine(candidato, "manifest.json")))
                return candidato;
        }

        return null;
    }

    private async void OnCopiarCaminhoClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_caminhoExtensao))
        {
            await DisplayAlertAsync("Pasta nao encontrada", "Nao foi possivel localizar a pasta da extensao automaticamente.", "OK");
            return;
        }

        await Clipboard.SetTextAsync(_caminhoExtensao);
    }

    private async void OnBaixarExtensaoClicked(object? sender, EventArgs e)
    {
        if (string.IsNullOrEmpty(_caminhoExtensao))
        {
            await DisplayAlertAsync("Pasta nao encontrada", "Nao foi possivel localizar os arquivos da extensao.", "OK");
            return;
        }

        try
        {
            var pastaDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var caminhoZip = Path.Combine(pastaDownloads, "StopBet-Extensao.zip");

            if (File.Exists(caminhoZip))
                File.Delete(caminhoZip);

            await Task.Run(() => ZipFile.CreateFromDirectory(_caminhoExtensao, caminhoZip));

            var abrirPasta = await DisplayAlertAsync(
                "Extensao baixada",
                $"Salva em:\n{caminhoZip}\n\nExtraia o .zip e selecione a pasta extraida em \"Carregar sem compactacao\".",
                "Abrir pasta",
                "OK");

            if (abrirPasta)
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{caminhoZip}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Erro ao baixar", $"Nao foi possivel gerar o arquivo: {ex.Message}", "OK");
        }
    }

    // Navegadores Chromium recusam, de proposito, abrir paginas internas
    // (edge://, chrome://) quando a URL vem por linha de comando de um processo
    // externo e ja existe uma instancia rodando - e um mecanismo de seguranca contra
    // apps maliciosos que tentassem levar o usuario direto pra uma pagina sensivel.
    // Por isso a navegacao automatica abaixo e "melhor esforco": quando o navegador
    // ja esta aberto, ela normalmente so abre uma aba em branco. Por garantia, a URL
    // tambem vai pra area de transferencia para o usuario colar manualmente.
    private async void OnAbrirPaginaExtensoesClicked(object? sender, EventArgs e)
    {
        const string url = "edge://extensions/";

        try
        {
            Process.Start(new ProcessStartInfo("msedge", url) { UseShellExecute = true });
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("chrome", "chrome://extensions/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                await DisplayAlertAsync("Nao foi possivel abrir o navegador", ex.Message, "OK");
                return;
            }
        }

        await Clipboard.SetTextAsync(url);
        await DisplayAlertAsync(
            "Pagina de extensoes",
            $"Uma aba deve ter aberto. Se nao foi direto para a pagina de extensoes, cole o endereco na barra (ja copiado):\n{url}",
            "OK");
    }
}
