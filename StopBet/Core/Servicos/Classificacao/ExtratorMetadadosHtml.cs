using HtmlAgilityPack;

namespace StopBet.Core.Servicos.Classificacao;

public sealed class ExtratorMetadadosHtml
{
    public MetadadosPagina Extrair(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return new MetadadosPagina(null, null);

        var documento = new HtmlDocument();
        documento.LoadHtml(html);

        var titulo = documento.DocumentNode
            .SelectSingleNode("//title")?
            .InnerText?
            .Trim();

        var nodoMeta = documento.DocumentNode.SelectSingleNode(
            "//meta[translate(@name,\"ABCDEFGHIJKLMNOPQRSTUVWXYZ\",\"abcdefghijklmnopqrstuvwxyz\")=\"description\"]");
        var metaDescricao = nodoMeta?.GetAttributeValue("content", string.Empty).Trim();

        return new MetadadosPagina(
            string.IsNullOrWhiteSpace(titulo) ? null : titulo,
            string.IsNullOrWhiteSpace(metaDescricao) ? null : metaDescricao);
    }
}
