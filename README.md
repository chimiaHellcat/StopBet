# StopBet

Sistema de auxílio no combate ao vício em apostas online (ludopatia), desenvolvido como Trabalho de Conclusão de Curso (TCC) em Ciência da Computação — URI Erechim/RS.

## Sobre o projeto

O StopBet é um sistema multiplataforma de controle de estímulos digitais, projetado para auxiliar no tratamento da ludopatia através do bloqueio de sites de apostas e cassinos online. A ferramenta atua como uma barreira tecnológica complementar ao acompanhamento terapêutico (Terapia Cognitivo-Comportamental), reduzindo a exposição do usuário a gatilhos digitais relacionados ao jogo.

O sistema combina:

- Uma lista local de domínios conhecidos (cache SQLite), para bloqueio imediato;
- Classificação dinâmica de domínios desconhecidos via Google Gemini API, com o resultado persistido para consultas futuras;
- *(planejado)* Interceptação de tráfego DNS via `VpnService` no Android e edição do arquivo `hosts` no Windows;
- *(planejado)* Sincronização de domínios bloqueados entre dispositivos via Supabase.

## Tecnologias

- **.NET MAUI (C#)** — aplicação multiplataforma (Android e Windows)
- **SQLite** (via `sqlite-net-pcl`) — cache local de domínios bloqueados
- **Google Gemini API** (`gemini-3.5-flash-lite`) — classificação de domínios desconhecidos, com resposta em JSON estruturado
- **HtmlAgilityPack** — extração de `<title>` e meta description do HTML analisado

## Estrutura do projeto

```
StopBet/
├── Core/                          # Logica de negocio, sem codigo especifico de plataforma
│   ├── Modelos/                   # DominioBloqueado, OrigemDominio
│   ├── Dados/                     # Conexao SQLite, seed de dominios conhecidos
│   ├── Repositorios/              # CRUD assincrono sobre DominioBloqueado
│   └── Servicos/
│       ├── Classificacao/         # Extracao de metadados HTML + classificacao via Gemini
│       └── Verificacao/           # Orquestracao: lista local -> IA -> persistencia
├── Platforms/                     # Codigo especifico de Android/Windows/iOS/MacCatalyst (gerado pelo template)
└── Resources/                     # Icones, fontes, estilos
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
- [x] Aplicação em tempo real: `ServicoVerificacaoDominio` aciona o bloqueio imediatamente a cada decisão positiva, e `SincronizadorBloqueio` aplica no hosts tudo que já está na lista local assim que o app inicia. Testado no navegador de verdade: domínios reais de apostas (seed + `1xbet.com` classificado pela IA) resultaram em `ERR_CONNECTION_REFUSED`

**Ainda não implementado:**

- [ ] Bloqueio via `VpnService` (Android) — aguardando emulador/dispositivo físico disponível
- [ ] Sincronização entre dispositivos via Supabase
- [ ] Interface final do usuário (a tela atual é apenas para testes)

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

## Autor

Vítor Hugo Balke Nodari — Ciência da Computação, URI Erechim/RS
Orientador: Hercio Menegotto
