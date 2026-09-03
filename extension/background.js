// Extensao StopBet - bloqueio de dominios de apostas em tempo real.
//
// Estrategia: dominios ja conhecidos ganham uma regra estatica no
// declarativeNetRequest assim que a extensao inicia (bloqueio instantaneo e
// confiavel, no nivel do proprio motor do navegador). Dominios desconhecidos
// sao verificados no momento da primeira navegacao via webNavigation.onBeforeNavigate,
// consultando o app StopBet (que classifica via Gemini); se for aposta, a aba e
// redirecionada e uma regra e criada para que futuras tentativas sejam instantaneas.

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

async function jaTemRegra(dominio) {
  const regras = await chrome.declarativeNetRequest.getDynamicRules();
  return regras.some((r) => r.condition && r.condition.urlFilter === `||${dominio}^`);
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

async function carregarListaInicial() {
  try {
    const resposta = await fetch(`${SERVIDOR_LOCAL}/lista`);
    const dados = await resposta.json();
    for (const dominio of dados.dominios ?? []) {
      await adicionarRegraBloqueio(dominio);
    }
  } catch (erro) {
    console.warn("StopBet: nao foi possivel carregar a lista local (o app StopBet esta aberto?)", erro);
  }
}

chrome.webNavigation.onBeforeNavigate.addListener(async (detalhe) => {
  if (detalhe.frameId !== 0) return; // so navegacao principal da aba, nao iframes/recursos

  const dominio = extrairDominio(detalhe.url);
  if (!dominio || dominiosVerificadosNestaSessao.has(dominio)) return;
  if (await jaTemRegra(dominio)) return; // ja coberto por uma regra existente

  try {
    const resposta = await fetch(`${SERVIDOR_LOCAL}/verificar?dominio=${encodeURIComponent(dominio)}`);
    const dados = await resposta.json();

    if (dados.bloquear) {
      await adicionarRegraBloqueio(dominio);
      chrome.tabs.update(detalhe.tabId, { url: urlPaginaBloqueio(dominio) });
    } else {
      dominiosVerificadosNestaSessao.add(dominio);
    }
  } catch (erro) {
    console.warn("StopBet: falha ao verificar dominio (o app StopBet esta aberto?)", erro);
  }
});

chrome.runtime.onInstalled.addListener(carregarListaInicial);
chrome.runtime.onStartup.addListener(carregarListaInicial);
carregarListaInicial();
