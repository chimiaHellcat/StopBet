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
    private readonly EstadoPausaVerificacao _estadoPausa;

    public ServicoVerificacaoDominio(
        IRepositorioDominioBloqueado repositorio,
        IServicoClassificacaoUrl servicoClassificacao,
        EstadoPausaVerificacao estadoPausa,
        IServicoBloqueioDominio? servicoBloqueio = null)
    {
        _repositorio = repositorio;
        _servicoClassificacao = servicoClassificacao;
        _estadoPausa = estadoPausa;
        _servicoBloqueio = servicoBloqueio;
    }

    public async Task<ResultadoVerificacaoDominio> VerificarDominioAsync(
        string dominio, string? html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dominio);

        var existente = await _repositorio.ObterPorNomeDominioAsync(dominio);
        if (existente is not null)
        {
            AplicarBloqueioEmSegundoPlano(dominio, cancellationToken);
            return new ResultadoVerificacaoDominio(
                DeveBloquear: true,
                Categoria: existente.Categoria,
                Origem: existente.Origem);
        }

        // Verificacao pausada: dominios ja conhecidos (acima) continuam bloqueados
        // normalmente, mas nenhum dominio novo e classificado (nem chama o Gemini)
        // enquanto pausado - deixa passar sem persistir nada.
        if (_estadoPausa.Pausado)
        {
            return new ResultadoVerificacaoDominio(
                DeveBloquear: false,
                Categoria: "Verificacao pausada",
                Origem: OrigemDominio.ClassificadoPorIA);
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
            AplicarBloqueioEmSegundoPlano(dominio, cancellationToken);
        }

        return new ResultadoVerificacaoDominio(
            DeveBloquear: classificacao.EAposta,
            Categoria: classificacao.Categoria,
            Origem: OrigemDominio.ClassificadoPorIA);
    }

    // O bloqueio via hosts (escrita em disco + "ipconfig /flushdns") existe para cobrir
    // outros navegadores/apps sem a extensao - quem chamou VerificarDominioAsync (a extensao,
    // via servidor local) ja bloqueia a aba sozinha com base no veredito retornado, sem
    // depender do hosts. Por isso essa aplicacao roda em segundo plano, sem atrasar a resposta:
    // o flushdns em especial gasta centenas de ms a mais de 1s so pelo custo de iniciar o
    // processo externo, tempo que so importa para o caminho do hosts, nao para quem esta chamando.
    private void AplicarBloqueioEmSegundoPlano(string dominio, CancellationToken cancellationToken)
    {
        if (_servicoBloqueio is null)
            return;

        _ = Task.Run(async () =>
        {
            try
            {
                await _servicoBloqueio.BloquearAsync(dominio, cancellationToken);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Falha ao aplicar bloqueio em segundo plano para {dominio}: {ex}");
            }
        }, cancellationToken);
    }
}
