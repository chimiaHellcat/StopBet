# StopBet

Sistema de auxílio no combate ao vício em apostas online (ludopatia), desenvolvido como Trabalho de Conclusão de Curso (TCC) em Ciência da Computação — URI Erechim/RS.

## Sobre o projeto

O StopBet é um sistema multiplataforma de controle de estímulos digitais, projetado para auxiliar no tratamento da ludopatia através do bloqueio de sites de apostas e cassinos online. A ferramenta atua como uma barreira tecnológica complementar ao acompanhamento terapêutico (Terapia Cognitivo-Comportamental), reduzindo a exposição do usuário a gatilhos digitais relacionados ao jogo.

O sistema combina:

- Uma lista local de domínios conhecidos (cache SQLite), para bloqueio imediato;
- Classificação dinâmica de domínios desconhecidos via Google Gemini API, com o resultado persistido para consultas futuras;
- Bloqueio efetivo no Windows via edição do arquivo `hosts`, aplicado automaticamente a cada decisão positiva;
- Bloqueio em tempo real no navegador (Edge/Chrome) via extensão própria, inclusive para domínios nunca vistos antes;
- *(planejado)* Interceptação de tráfego DNS via `VpnService` no Android;
- *(planejado)* Sincronização de domínios bloqueados entre dispositivos via Supabase.

## Por que hosts *e* extensão de navegador?

O bloqueio via edição do arquivo `hosts` do Windows tem uma limitação importante que só ficou evidente durante o desenvolvimento: navegadores modernos (Chrome, Edge, Brave) vêm com **DNS-over-HTTPS (DoH / "Secure DNS")** ativado por padrão, resolvendo domínios diretamente em um servidor DNS criptografado (ex.: Google, Cloudflare) sem passar pela resolução de nomes do sistema operacional. Como o `hosts` só é consultado pelo resolvedor do SO, um navegador com DoH ativo pode simplesmente ignorá-lo — o site de apostas abriria normalmente apesar do bloqueio estar "ativo", criando uma falha silenciosa: o usuário confiaria em uma proteção que, na prática, não estaria funcionando.

A extensão de navegador fecha exatamente essa lacuna, atuando em uma camada diferente: em vez de depender da resolução de nomes, ela intercepta a navegação diretamente no navegador (via `declarativeNetRequest`/`webNavigation`), funcionando independentemente de como — ou onde — o DNS foi resolvido.

As duas abordagens são complementares, não redundantes:

- **`hosts`** — nível do sistema operacional; cobre qualquer aplicação, não só navegadores; mas pode ser contornado por DoH
- **extensão de navegador** — nível do navegador; não é afetada por DoH; mas cobre apenas o navegador em que está instalada

Essa combinação foi uma decisão técnica tomada durante o desenvolvimento, indo além do escopo originalmente proposto (`VpnService` no Android + edição do `hosts` no Windows), especificamente para eliminar essa lacuna de contorno via DoH — uma camada adicional de defesa em profundidade, e não uma solução redundante.

## Tecnologias

- **.NET MAUI (C#)** — aplicação multiplataforma (Android e Windows)
- **SQLite** (via `sqlite-net-pcl`) — cache local de domínios bloqueados
- **Google Gemini API** (`gemini-3.5-flash-lite`) — classificação de domínios desconhecidos, com resposta em JSON estruturado
- **HtmlAgilityPack** — extração de `<title>` e meta description do HTML analisado
- **Extensão de navegador (Manifest V3)** — bloqueio em tempo real no Chromium (Edge/Chrome/Brave/Opera), via `declarativeNetRequest` e `webNavigation`

## Estrutura do projeto

```
StopBet/                             # Solucao
├── StopBet/                         # Projeto .NET MAUI
│   ├── Core/                        # Logica de negocio, sem codigo especifico de plataforma
│   │   ├── Modelos/                 # DominioBloqueado, OrigemDominio
│   │   ├── Dados/                   # Conexao SQLite, seed de dominios conhecidos
│   │   ├── Repositorios/            # CRUD assincrono sobre DominioBloqueado
│   │   └── Servicos/
│   │       ├── Classificacao/       # Extracao de metadados HTML + classificacao via Gemini
│   │       ├── Verificacao/         # Orquestracao: lista local -> IA -> persistencia -> bloqueio
│   │       ├── Bloqueio/            # Contrato de bloqueio + sincronizador
│   │       └── ServidorLocal/       # Contrato da ponte HTTP com a extensao de navegador
│   ├── Platforms/                   # Codigo especifico de plataforma
│   │   └── Windows/                 # Edicao do hosts, servidor local (ponte com a extensao)
│   └── Resources/                   # Icones, fontes, estilos
└── extension/                       # Extensao de navegador (Manifest V3, Chromium)
```

## Status atual

**Implementado e testado:**

- [x] Camada de negócio compartilhada (`Core/`)
- [x] Repositório SQLite com CRUD assíncrono
- [x] Classificação de domínios via Gemini API (resposta JSON estruturada: `e_aposta`, `categoria`, `confianca`)
- [x] Seed inicial com 10 domínios de apostas conhecidos (`.bet.br`, origem `ListaFixa`)
- [x] Serviço de verificação/decisão (`ServicoVerificacaoDominio`) — lista local primeiro, IA como fallback, persistindo classificações positivas
- [x] Tela de teste (`MainPage`) listando os domínios em cache, com IP resolvido via DNS
- [x] Bloqueio via edição do arquivo `hosts` (Windows) — `IServicoBloqueioDominio` / `ServicoBloqueioDominioWindows`, idempotente, testado contra o hosts real com elevação (`requireAdministrator`)
- [x] Aplicação em tempo real: `ServicoVerificacaoDominio` aciona o bloqueio imediatamente a cada decisão positiva, e `SincronizadorBloqueio` aplica no hosts tudo que já está na lista local assim que o app inicia. Testado no navegador de verdade: domínios reais de apostas (seed + `1xbet.com` classificado pela IA) resultaram em `ERR_CONNECTION_REFUSED`, incluindo a variante `www.` (bug real encontrado e corrigido: uma entrada de hosts não cobre a outra)
- [x] Extensão de navegador (Chromium) com bloqueio em tempo real: domínios conhecidos via `declarativeNetRequest` (instantâneo), domínios desconhecidos verificados ao vivo tanto na navegação direta (`webNavigation.onBeforeNavigate`, URL original) quanto após redirecionamentos HTTP (`webNavigation.onCommitted`, URL final) → servidor local → Gemini. Página de bloqueio própria com mensagem motivacional. Testado com domínio conhecido (`bet365.bet.br`), desconhecido via digitação direta (`sportingbet.com`) e desconhecido via cadeia de redirecionamento simulando um clique em anúncio/link de afiliado (`brazino777.bet.br`), todos bloqueados corretamente
- [x] Resposta da verificação em tempo real desacoplada da aplicação do bloqueio no `hosts`: a extensão recebe o veredito assim que a classificação termina, sem esperar a escrita no `hosts` nem o `ipconfig /flushdns` (que sozinho custa ~1s de spawn de processo) — essa aplicação roda em segundo plano, já que o bloqueio da aba pela extensão não depende do `hosts`

**Ainda não implementado:**

- [ ] Bloqueio via `VpnService` (Android) — aguardando emulador/dispositivo físico disponível
- [ ] Extensão para Firefox (API similar à do Chromium, mas manifest e alguns detalhes diferentes)
- [ ] Sincronização entre dispositivos via Supabase
- [ ] Interface final do usuário (a tela atual é apenas para testes)

## Limitações conhecidas

- **Latência na primeira verificação de um domínio desconhecido**: entre ~1s e ~4s, na prática, entre a navegação e o bloqueio efetivo — tempo integralmente da chamada de rede à API do Gemini (inferência do modelo), já que toda a aplicação do bloqueio local (regra do `declarativeNetRequest`, escrita no `hosts`) roda de forma desacoplada e não soma a esse tempo. Durante essa janela, o site pode chegar a carregar brevemente antes do redirecionamento. É uma limitação inerente a depender de uma IA externa para classificar domínios nunca vistos antes — domínios já conhecidos (seed ou já classificados antes) continuam bloqueados instantaneamente, sem essa espera. Decisão tomada durante o desenvolvimento: aceitar essa variância como o custo do modelo em tempo real, em vez de trocar de modelo de IA ou introduzir heurísticas locais adicionais (nome do domínio, por exemplo) que fugiriam do escopo de "classificação via IA" proposto no TCC.

## Como rodar

### Pré-requisitos

- .NET SDK 10 com os workloads `android` e `maui-windows` instalados
- Uma chave de API do Google Gemini ([Google AI Studio](https://aistudio.google.com/))

### Configuração

Defina a variável de ambiente `GEMINI_API_KEY` com sua chave:

```powershell
setx GEMINI_API_KEY "sua-chave-aqui"
```

> No Windows, reabra o terminal/IDE depois de rodar o `setx` para a variável ser reconhecida no novo processo. No Android essa abordagem ainda não funciona — apps não herdam variáveis de ambiente do shell — e fica pendente de outra implementação (por exemplo, `SecureStorage`).

### Build e execução

```bash
dotnet build StopBet/StopBet.csproj -f net10.0-windows10.0.19041.0
dotnet run --project StopBet/StopBet.csproj -f net10.0-windows10.0.19041.0
```

> O app pede elevação (UAC) ao abrir — é necessário para editar o arquivo `hosts`. Ao rodar com o depurador do VS Code/Visual Studio isso pode falhar (um processo não elevado não consegue anexar um depurador a um processo elevado); prefira rodar o `.exe` compilado diretamente (`StopBet/bin/Debug/net10.0-windows10.0.19041.0/win-x64/StopBet.exe`).

### Instalar a extensão de navegador (opcional, para bloqueio em tempo real)

Com o app StopBet rodando (ele expõe a ponte local em `http://127.0.0.1:5127`):

1. Abra `edge://extensions` (ou `chrome://extensions` no Chrome)
2. Ative o **Modo de desenvolvedor**
3. Clique em **Carregar sem compactação** e selecione a pasta `extension/` deste repositório
4. Pronto — domínios já conhecidos são bloqueados na hora; domínios novos são verificados em tempo real na primeira tentativa de acesso

## Autor

Vítor Hugo Balke Nodari — Ciência da Computação, URI Erechim/RS
Orientador: Hercio Menegotto
