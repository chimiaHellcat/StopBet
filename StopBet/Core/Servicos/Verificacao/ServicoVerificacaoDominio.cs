using StopBet.Core.Modelos;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Classificacao;

namespace StopBet.Core.Servicos.Verificacao;

// "Cerebro" da decisao de bloqueio: checa a lista local primeiro e so aciona a IA
// quando o dominio e desconhecido, persistindo o resultado positivo para consultas futuras.
// Usado tanto pelo VpnService (Android) quanto pela edicao do hosts (Windows).
public sealed class ServicoVerificacaoDominio : IServicoVerificacaoDominio
{
    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoClassificacaoUrl _servicoClassificacao;

    public ServicoVerificacaoDominio(
        IRepositorioDominioBloqueado repositorio,
        IServicoClassificacaoUrl servicoClassificacao)
    {
        _repositorio = repositorio;
        _servicoClassificacao = servicoClassificacao;
    }

    public async Task<ResultadoVerificacaoDominio> VerificarDominioAsync(
        string dominio, string? html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        var existente = await _repositorio.ObterPorNomeDominioAsync(dominio);
        if (existente is not null)
        {
            return new ResultadoVerificacaoDominio(
                DeveBloquear: true,
                Categoria: existente.Categoria,
                Origem: existente.Origem);
        }

        var classificacao = await _servicoClassificacao.ClassificarAsync(dominio, html ?? string.Empty, cancellationToken);

        if (classificacao.EAposta)
        {
            await _repositorio.AdicionarAsync(new DominioBloqueado
            {
                NomeDominio = dominio,
                Categoria = classificacao.Categoria,
                Origem = OrigemDominio.ClassificadoPorIA,
                DataInclusao = DateTime.UtcNow
            });
        }

        return new ResultadoVerificacaoDominio(
            DeveBloquear: classificacao.EAposta,
            Categoria: classificacao.Categoria,
            Origem: OrigemDominio.ClassificadoPorIA);
    }
}
