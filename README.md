# StopBet

Sistema de auxílio no combate ao vício em apostas online (ludopatia), desenvolvido como Trabalho de Conclusão de Curso (TCC) em Ciência da Computação — URI Erechim/RS.

## Sobre o projeto

O StopBet é um sistema multiplataforma de controle de estímulos digitais, projetado para auxiliar no tratamento da ludopatia através do bloqueio de sites de apostas e cassinos online. A ferramenta atua como uma barreira tecnológica complementar ao acompanhamento terapêutico (Terapia Cognitivo-Comportamental), reduzindo a exposição do usuário a gatilhos digitais relacionados ao jogo — sem substituir o tratamento, mas dando um instante a mais de fricção entre o impulso e o acesso.

## Como funciona

O bloqueio é feito em **camadas**, cada uma cobrindo uma lacuna que a anterior deixa:

1. **Lista local (SQLite)** — todo domínio já conhecido (seed inicial ou classificado antes) é consultado primeiro, sem depender de rede.
2. **Classificação por IA (Google Gemini)** — quando o domínio é desconhecido, seu nome é enviado pra API do Gemini, que classifica se é uma casa de apostas/cassino com base no próprio domínio (e no título/meta description da página, quando disponíveis). O resultado positivo é persistido, então a mesma pergunta nunca é feita duas vezes pro mesmo domínio.
3. **Bloqueio via `hosts` (Windows)** — todo domínio conhecido é redirecionado para `127.0.0.1` no arquivo `hosts` do sistema, bloqueando o acesso em qualquer aplicação, não só navegadores.
4. **Extensão de navegador (Chromium)** — bloqueia em tempo real diretamente no navegador, cobrindo uma lacuna real do `hosts`: navegadores modernos (Chrome, Edge, Brave) usam **DNS-over-HTTPS** por padrão, resolvendo nomes num servidor criptografado sem passar pela resolução do sistema operacional — nesse caso o `hosts` é simplesmente ignorado, e o site abriria normalmente apesar do bloqueio estar "ativo". A extensão intercepta a navegação no próprio navegador (via `declarativeNetRequest`/`webNavigation`), então funciona independente de como o DNS foi resolvido.

`hosts` e extensão são complementares, não redundantes: o primeiro cobre qualquer aplicação mas pode ser contornado por DoH; a segunda não é afetada por DoH mas só cobre o navegador em que está instalada. Foi uma decisão tomada durante o desenvolvimento, indo além do escopo originalmente proposto no TCC (`VpnService` no Android + edição do `hosts` no Windows), especificamente pra eliminar essa lacuna.

Também é possível **pausar temporariamente** a verificação de domínios novos pelo Painel do app (por exemplo, pra navegar sem interrupções durante uma atividade específica) — domínios já bloqueados continuam protegidos normalmente durante a pausa; só a classificação de domínios nunca vistos é suspensa, e a proteção nunca fica pausada sem o usuário perceber depois de reabrir o app (o estado não persiste entre execuções).

## Interface desktop

Navegação lateral fixa com três seções, tema escuro consistente com a página de bloqueio da extensão:

- **Painel** — contadores de domínios por origem (lista fixa vs. classificados por IA), status de cada camada de proteção (lista local, hosts, servidor local da extensão) e o botão de pausar/retomar a verificação.
- **Domínios** — lista completa com busca, adicionar um domínio manualmente (aceita uma URL completa colada, extrai só o hostname) e remover, com bloqueio/desbloqueio aplicado imediatamente.
- **Extensão** — status do servidor local, download da extensão em `.zip` e atalho pra página de extensões do navegador.

## Extensão de navegador

Extensão Manifest V3 para Chromium (Edge, Chrome, Brave, Opera), empacotada junto do executável do app (não depende do repositório estar presente, funciona em qualquer instalação):

- **Domínios conhecidos** são bloqueados instantaneamente via `declarativeNetRequest`, sem depender de rede.
- **Domínios desconhecidos** são verificados ao vivo contra o servidor local do app (`http://127.0.0.1:5127`), tanto na navegação direta (`webNavigation.onBeforeNavigate`, URL original) quanto após redirecionamentos HTTP (`webNavigation.onCommitted`, URL final) — sem o segundo listener, um clique em anúncio/link de afiliado que passa por um domínio de rastreamento antes do destino final passaria batido, já que o primeiro listener só enxerga a URL antes do redirecionamento.
- **Reconciliação de regras**: a cada sincronização (na inicialização e a cada 2 minutos, via `chrome.alarms`), as regras de bloqueio são comparadas com a lista atual do app — domínios removidos no app têm sua regra apagada na extensão também, não só adicionada quando surgem.
- **Limpeza de cache/Service Worker**: sites que registram Service Worker próprio (PWAs, comuns em redes sociais) podem continuar servindo páginas do cache deles mesmo depois de bloqueados, porque nem `hosts` nem `declarativeNetRequest` interceptam uma resposta que nunca vira uma requisição de rede real. Por isso, ao bloquear um domínio pela primeira vez, seu cache e Service Worker são limpos (`chrome.browsingData.remove`).
- Página de bloqueio própria, com mensagem de apoio inspirada em TCC ("uma recaída não é um fracasso").

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
│   ├── PainelPage / DominiosPage /
│   │   ExtensaoPage / AdicionarDominioPage   # Telas da interface desktop
│   └── Resources/                   # Icones, fontes, estilos
└── extension/                       # Extensao de navegador (Manifest V3, Chromium)
```

## Status atual

**Implementado e testado:**

- [x] Camada de negócio compartilhada (`Core/`): modelos, repositório SQLite assíncrono, seed inicial com 10 domínios de apostas conhecidos (`.bet.br`, origem `ListaFixa`)
- [x] Classificação de domínios via Gemini API (resposta JSON estruturada: `e_aposta`, `categoria`, `confianca`), com retry e backoff exponencial para erros transitórios (429 rate limit, 503 sobrecarga) — sem isso, esses erros derrubavam a classificação silenciosamente, sem nenhum aviso visível
- [x] Serviço de verificação/decisão (`ServicoVerificacaoDominio`, o "cérebro") — lista local primeiro, IA como fallback, persistindo classificações positivas e aplicando o bloqueio em todas as camadas disponíveis; pode ser pausado temporariamente pelo Painel
- [x] Interface desktop completa (Painel, Domínios, Extensão) — ver seções acima
- [x] Bloqueio via edição do arquivo `hosts` (Windows), idempotente, cobrindo domínio e variante `www.` como par, aplicado automaticamente a cada decisão positiva e sincronizado no início do app; testado contra o hosts real com elevação (`requireAdministrator`)
- [x] Extensão de navegador (Chromium) com bloqueio em tempo real — ver seção acima. Aplicação do bloqueio desacoplada da resposta pra extensão (escrita no `hosts` e `ipconfig /flushdns` rodam em segundo plano, sem atrasar o veredito)
- [x] **Teste de acurácia em lote**: 68 domínios de apostas reais e nunca cadastrados (marcas licenciadas `.bet.br` + operadoras internacionais não autorizadas no Brasil), incluindo rajadas propositalmente pesadas para forçar rate limiting — **68/68 classificados corretamente como aposta, 0 falso negativo**
- [x] Testes reais no navegador: domínio conhecido, domínio desconhecido via digitação direta, e domínio desconhecido via cadeia de redirecionamento (simulando clique em anúncio/link de afiliado) — capturando inclusive subdomínios nunca vistos antes (ex.: `casino.bet365.bet.br`), todos bloqueados corretamente

**Ainda não implementado:**

- [ ] Bloqueio via `VpnService` (Android) — aguardando emulador/dispositivo físico disponível
- [ ] Extensão para Firefox (API similar à do Chromium, mas manifest e alguns detalhes diferentes)
- [ ] Sincronização entre dispositivos via Supabase

## Limitações conhecidas

- **Latência na primeira verificação de um domínio desconhecido**: entre ~1s e ~6s na prática, tempo da chamada de rede à API do Gemini — durante essa janela o site pode chegar a carregar brevemente antes do redirecionamento. Domínios já conhecidos continuam bloqueados instantaneamente. Decisão tomada durante o desenvolvimento: aceitar essa variância como custo do modelo em tempo real, em vez de introduzir heurísticas locais que fugiriam do escopo de "classificação via IA" proposto no TCC.
- **Cota da API do Gemini sob carga pesada**: a chave usada em desenvolvimento é de camada gratuita, com limite de requisições por minuto — sob rajadas pesadas (vários domínios em sequência rápida) a API responde 429, mitigado por retry automático mas não eliminado sob carga muito pesada e sustentada. Em uso normal isso dificilmente é atingido, mas vale notar que **todo** domínio novo visitado (não só os suspeitos) passa por esse mesmo caminho, já que não há como saber de antemão que um domínio é seguro sem perguntar à IA.
- **Cor de fundo da barra lateral no Windows**: o menu lateral (flyout) do Shell renderiza com fundo preto em vez da paleta escura usada no resto do app — o Windows aplica um material próprio (Mica) na área de navegação do WinUI3 que não é totalmente sobrescrito pelas propriedades `Shell.FlyoutBackgroundColor`/`Shell.FlyoutBackdrop`. Puramente estético, não afeta funcionalidade.

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

A extensão é empacotada junto do executável. Pela aba **Extensão** do app, dá pra baixar um `.zip` com os arquivos ou abrir a página de extensões do navegador diretamente. Depois, com o app StopBet rodando (ele expõe a ponte local em `http://127.0.0.1:5127`):

1. Abra `edge://extensions` (ou `chrome://extensions` no Chrome) — a aba **Extensão** do app faz isso por você, com uma ressalva: o Chromium bloqueia por segurança que um programa externo navegue direto pra uma página interna quando o navegador já está aberto, então às vezes só abre uma aba em branco — nesse caso o endereço já foi copiado pra área de transferência, é só colar
2. Ative o **Modo de desenvolvedor**
3. Clique em **Carregar sem compactação** e selecione a pasta `extension/` deste repositório (ou a pasta onde você extraiu o `.zip` baixado pelo app)
4. Pronto — domínios já conhecidos são bloqueados na hora; domínios novos são verificados em tempo real na primeira tentativa de acesso

## Autor

Vítor Hugo Balke Nodari — Ciência da Computação, URI Erechim/RS
Orientador: Hercio Menegotto
