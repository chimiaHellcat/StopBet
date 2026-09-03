using StopBet.Core.Modelos;
using StopBet.Core.Repositorios;

namespace StopBet.Core.Dados;

// Lista inicial de dominios de apostas conhecidos, usada para popular a base local
// na primeira execucao. Deve ser revisada periodicamente: no Brasil, operadoras
// autorizadas usam dominios oficiais no formato "<marca>.bet.br" desde que a
// regulamentacao (Lei 14.790/2023) tornou isso obrigatorio a partir de 01/01/2025.
public sealed class SementeDominiosConhecidos
{
    private const string CategoriaPadrao = "Casa de Apostas";

    private static readonly string[] Dominios =
    [
        "bet365.bet.br",
        "betano.bet.br",
        "betnacional.bet.br",
        "superbet.bet.br",
        "kto.bet.br",
        "blaze.bet.br",
        "betboom.bet.br",
        "pixbet.bet.br",
        "betfair.bet.br",
        "novibet.bet.br"
    ];

    public async Task PopularAsync(IRepositorioDominioBloqueado repositorio)
    {
        foreach (var nomeDominio in Dominios)
        {
            if (await repositorio.ExisteAsync(nomeDominio))
                continue;

            await repositorio.AdicionarAsync(new DominioBloqueado
            {
                NomeDominio = nomeDominio,
                Categoria = CategoriaPadrao,
                Origem = OrigemDominio.ListaFixa,
                DataInclusao = DateTime.UtcNow
            });
        }
    }
}
