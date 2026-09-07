// Extensao StopBet - bloqueio de dominios de apostas em tempo real.
//
// Estrategia: dominios ja conhecidos ganham uma regra estatica no
// declarativeNetRequest assim que a extensao inicia (bloqueio instantaneo e
// confiavel, no nivel do proprio motor do navegador). Dominios desconhecidos
// sao verificados consultando o app StopBet (que classifica via Gemini); se for
// aposta, a aba e redirecionada e uma regra e criada para que futuras tentativas
// sejam instantaneas.
//
// A verificacao roda em DOIS momentos, nao so um:
//   - onBeforeNavigate: URL original, antes de qualquer redirecionamento. Cobre
//     o caso comum de digitar/colar o dominio direto na barra de enderecos.
//   - onCommitted: URL final, ja resolvida apos redirecionamentos HTTP. Cobre
//     links de anuncios/afiliados (ex.: clique num ad que passa por um dominio
//     de rastreamento antes de cair no site de apostas de verdade) - sem isso,
//     onBeforeNavigate sozinho so enxerga o dominio do redirecionador, nunca o
//     destino final, e o site de apostas passa batido.
//
// As regras do declarativeNetRequest sao reconciliadas com a lista do app (nao so
// preenchidas): dominios removidos no app (ex.: desbloqueados manualmente) tem sua
// regra apagada aqui tambem, nao so adicionada quando surgem. Sem isso, uma vez
// bloqueado, o dominio ficaria bloqueado na extensao para sempre, mesmo depois de
// removido do app - so o hosts do Windows refletiria a remocao.

const SERVIDOR_LOCAL = "http://127.0.0.1:5127";
const dominiosVerificadosNestaSessao = new Set();

function extrairDominio(url) {
  try {
    return new URL(url).hostname;
  } catch {
    return null;
  }
}

function urlPaginaBloqueio(dominio) {
  return chrome.runtime.getURL(`bloqueado.html?dominio=${encodeURIComponent(dominio)}`);
}

function dominioDaRegra(regra) {
  const filtro = regra.condition && regra.condition.urlFilter;
  if (!filtro || !filtro.startsWith("||") || !filtro.endsWith("^")) return null;
  return filtro.slice(2, -1);
}

async function jaTemRegra(dominio) {
  const regras = await chrome.declarativeNetRequest.getDynamicRules();
  return regras.some((r) => dominioDaRegra(r) === dominio);
}

async function proximoIdRegra() {
  const regras = await chrome.declarativeNetRequest.getDynamicRules();
  const maiorId = regras.reduce((max, r) => Math.max(max, r.id), 0);
  return maiorId + 1;
}

async function adicionarRegraBloqueio(dominio) {
  if (await jaTemRegra(dominio)) return;

  const id = await proximoIdRegra();

  await chrome.declarativeNetRequest.updateDynamicRules({
    addRules: [
      {
        id,
        priority: 1,
        action: { type: "redirect", redirect: { url: urlPaginaBloqueio(dominio) } },
        condition: { urlFilter: `||${dominio}^`, resourceTypes: ["main_frame"] },
      },
    ],
  });
}

// Reconcilia as regras do DNR com a lista atual do app: adiciona o que falta E
// remove regras de dominios que nao estao mais na lista (foram desbloqueados/removidos
// no app). Chamada na inicializacao e periodicamente (ver chrome.alarms mais abaixo).
async function sincronizarRegras() {
  try {
    const resposta = await fetch(`${SERVIDOR_LOCAL}/lista`);
    const dados = await resposta.json();
    const dominiosConhecidos = new Set(dados.dominios ?? []);

    const regras = await chrome.declarativeNetRequest.getDynamicRules();
    const idsParaRemover = regras
      .filter((r) => {
        const dominio = dominioDaRegra(r);
        return dominio && !dominiosConhecidos.has(dominio);
      })
      .map((r) => r.id);

    if (idsParaRemover.length > 0) {
      await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: idsParaRemover });
    }

    for (const dominio of dominiosConhecidos) {
      await adicionarRegraBloqueio(dominio);
    }
  } catch (erro) {
    console.warn("StopBet: nao foi possivel sincronizar a lista local (o app StopBet esta aberto?)", erro);
  }
}

async function verificarEBloquearSeNecessario(url, tabId) {
  const dominio = extrairDominio(url);
  if (!dominio || dominiosVerificadosNestaSessao.has(dominio)) return;
  if (await jaTemRegra(dominio)) return; // ja coberto por uma regra existente

  try {
    const resposta = await fetch(`${SERVIDOR_LOCAL}/verificar?dominio=${encodeURIComponent(dominio)}`);
    const dados = await resposta.json();

    if (dados.bloquear) {
      await adicionarRegraBloqueio(dominio);
      chrome.tabs.update(tabId, { url: urlPaginaBloqueio(dominio) });
    } else {
      dominiosVerificadosNestaSessao.add(dominio);
    }
  } catch (erro) {
    console.warn("StopBet: falha ao verificar dominio (o app StopBet esta aberto?)", erro);
  }
}

chrome.webNavigation.onBeforeNavigate.addListener((detalhe) => {
  if (detalhe.frameId !== 0) return; // so navegacao principal da aba, nao iframes/recursos
  verificarEBloquearSeNecessario(detalhe.url, detalhe.tabId);
});

chrome.webNavigation.onCommitted.addListener((detalhe) => {
  if (detalhe.frameId !== 0) return;
  verificarEBloquearSeNecessario(detalhe.url, detalhe.tabId);
});

const ALARME_SINCRONIZACAO = "stopbet-sincronizar";

chrome.runtime.onInstalled.addListener(() => {
  sincronizarRegras();
  chrome.alarms.create(ALARME_SINCRONIZACAO, { periodInMinutes: 2 });
});
chrome.runtime.onStartup.addListener(() => {
  sincronizarRegras();
  chrome.alarms.create(ALARME_SINCRONIZACAO, { periodInMinutes: 2 });
});
chrome.alarms.onAlarm.addListener((alarme) => {
  if (alarme.name === ALARME_SINCRONIZACAO) sincronizarRegras();
});

sincronizarRegras();
chrome.alarms.create(ALARME_SINCRONIZACAO, { periodInMinutes: 2 });
