using StopBet.Core.Modelos;
using StopBet.Core.Repositorios;
using StopBet.Core.Servicos.Bloqueio;
using StopBet.Core.Servicos.Classificacao;

namespace StopBet.Core.Servicos.Verificacao;

// "Cerebro" da decisao de bloqueio: checa a lista local primeiro e so aciona a IA
// quando o dominio e desconhecido, persistindo o resultado positivo para consultas futuras.
// Toda decisao positiva e imediatamente aplicada via IServicoBloqueioDominio, quando
// disponivel na plataforma atual (hosts no Windows, VpnService no Android quando existir) -
// esse e o "cerebro" usado por ambos.
public sealed class ServicoVerificacaoDominio : IServicoVerificacaoDominio
{
    private readonly IRepositorioDominioBloqueado _repositorio;
    private readonly IServicoClassificacaoUrl _servicoClassificacao;
    private readonly IServicoBloqueioDominio? _servicoBloqueio;

    public ServicoVerificacaoDominio(
        IRepositorioDominioBloqueado repositorio,
        IServicoClassificacaoUrl servicoClassificacao,
        IServicoBloqueioDominio? servicoBloqueio = null)
    {
        _repositorio = repositorio;
        _servicoClassificacao = servicoClassificacao;
        _servicoBloqueio = servicoBloqueio;
    }

    public async Task<ResultadoVerificacaoDominio> VerificarDominioAsync(
        string dominio, string? html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        var existente = await _repositorio.ObterPorNomeDominioAsync(dominio);
        if (existente is not null)
        {
            await AplicarBloqueioAsync(dominio, cancellationToken);
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
            await AplicarBloqueioAsync(dominio, cancellationToken);
        }

        return new ResultadoVerificacaoDominio(
            DeveBloquear: classificacao.EAposta,
            Categoria: classificacao.Categoria,
            Origem: OrigemDominio.ClassificadoPorIA);
    }

    private async Task AplicarBloqueioAsync(string dominio, CancellationToken cancellationToken)
    {
        if (_servicoBloqueio is not null)
            await _servicoBloqueio.BloquearAsync(dominio, cancellationToken);
    }
}
