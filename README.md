<div align="center">

# Dueto

**Um papel de parede, dois dispositivos, em sintonia.**

Hub de wallpapers autorais (estáticos e animados) que mantém o seu PC e o seu iPhone com o mesmo visual.

![Status](https://img.shields.io/badge/status-em%20planejamento-orange)
![.NET](https://img.shields.io/badge/.NET-ASP.NET%20Core-512BD4?logo=dotnet&logoColor=white)
![Angular](https://img.shields.io/badge/Angular-PWA-DD0031?logo=angular&logoColor=white)
![PostgreSQL](https://img.shields.io/badge/PostgreSQL-16-4169E1?logo=postgresql&logoColor=white)
![Docker](https://img.shields.io/badge/Docker-Compose-2496ED?logo=docker&logoColor=white)
![Electron](https://img.shields.io/badge/Electron-desktop-47848F?logo=electron&logoColor=white)

</div>

---

## Índice

- [Sobre](#sobre)
- [Como funciona](#como-funciona)
- [Funcionalidades](#funcionalidades)
- [Arquitetura](#arquitetura)
- [Stack](#stack)
- [Limitações conhecidas](#limitações-conhecidas)
- [Estrutura do repositório](#estrutura-do-repositório)
- [Como rodar em desenvolvimento](#como-rodar-em-desenvolvimento)
- [Variáveis de ambiente](#variáveis-de-ambiente)
- [Deploy](#deploy)
- [Roadmap](#roadmap)
- [Custo](#custo)
- [Segurança](#segurança)
- [Contribuindo](#contribuindo)
- [Licença](#licença)

---

## Sobre

O Dueto é um catálogo de wallpapers com um botão **"Definir wallpaper"**. Você escolhe a arte, clica, e o sistema cuida do resto: aplica no aparelho em que você está e leva a mesma escolha para o outro.

Hoje, manter o mesmo visual no computador e no celular é um processo manual: baixar a imagem, passar para o outro aparelho, aplicar. O Dueto automatiza esse caminho nos dois sentidos.

- **No PC:** um app desktop aplica o wallpaper (estático ou animado) e fica conectado ao servidor para receber trocas feitas no celular.
- **No iPhone:** um PWA mostra o catálogo e usa o app **Atalhos** da Apple para aplicar o wallpaper, que é o único caminho que o iOS oferece.

> O Dueto é inspirado na ideia de apps como o Wallpaper Engine, mas é um projeto independente e **não tem afiliação** com ele nem com a Valve.

---

## Como funciona

### PC → iPhone

1. Você escolhe o wallpaper no app desktop. O app aplica no PC.
2. A API registra o novo estado e envia uma **notificação Web Push** para o PWA no iPhone.
3. Você toca na notificação. O PWA abre e dispara o **Atalho** do Dueto.
4. O Atalho baixa a imagem e aplica o wallpaper, e depois confirma para a API que foi aplicado.
5. Se você perder a notificação, o servidor reenvia **lembretes espaçados** (por padrão, +2 min, +15 min e +1 h) até a confirmação. Além disso, o PWA mostra um **banner** enquanto o iPhone não estiver com o wallpaper atual.

### iPhone → PC

1. Você escolhe o wallpaper no PWA. O PWA grava a escolha e dispara o Atalho para aplicar no iPhone.
2. A API avisa o app desktop em tempo real (**SignalR**), e o PC troca na hora, sem nenhuma notificação.
3. Se o PC estiver desligado ou com o app fechado, ele consulta o estado ao reconectar e aplica o que estava pendente.

### Pareamento

Sem contas e sem senha. Um dos aparelhos gera um **código curto** (válido por poucos minutos), o outro digita o código, e os dois passam a compartilhar o mesmo par. Cada aparelho recebe seu próprio token.

---

## Funcionalidades

**MVP**

- Catálogo de wallpapers estáticos
- Aplicar no PC (Windows) pelo app desktop
- Aplicar no iPhone pelo PWA e Atalhos
- Sincronização nos dois sentidos
- Notificação com lembretes e banner de pendência
- Pareamento por código

**Depois**

- Wallpapers animados no desktop (vídeo/HTML), com pausa em jogos e tela cheia
- Suporte a Linux para estáticos
- Variantes por modelo de iPhone (recorte e área segura da tela de bloqueio)
- Android, onde existe API nativa para definir wallpaper
- Atualização automática do app desktop

---

## Arquitetura

```
App desktop (Electron) ──REST + SignalR──┐
                                         ├──► Caddy (HTTPS) ──► API ASP.NET Core ──► PostgreSQL
iPhone: PWA (Angular)  ──REST────────────┘                          │
        │                                                           └──► Web Push (VAPID) ──► iPhone
        └──► Atalhos (iOS): baixa e aplica o wallpaper

Arquivos dos wallpapers: GitHub Releases (o banco guarda só a URL e o checksum)
```

| Componente | Papel |
|---|---|
| **API (ASP.NET Core)** | Catálogo, pareamento, estado atual, hub SignalR, envio de Web Push e agendador de lembretes |
| **PWA (Angular)** | Interface única do catálogo, servida no navegador e instalável no iPhone |
| **App desktop (Electron)** | Embute a interface, aplica o wallpaper, mantém a conexão em tempo real e registra o protocolo `dueto://` |
| **Atalho (iOS)** | Baixa a imagem da URL recebida e aplica como wallpaper |
| **PostgreSQL** | Catálogo, dispositivos, estado, assinaturas de push e lembretes |
| **Caddy** | HTTPS automático (Let's Encrypt) e proxy reverso |
| **GitHub Releases** | Armazenamento gratuito dos arquivos de wallpaper |

---

## Stack

| Camada | Tecnologia |
|---|---|
| Backend | .NET 10 (LTS), ASP.NET Core, SignalR, EF Core + Npgsql |
| Frontend | Angular, `@angular/pwa` (service worker e `SwPush`) |
| Desktop | Electron (TypeScript) |
| Banco | PostgreSQL 16 |
| Infra | Docker Compose, Caddy, AWS EC2 (1 GB de RAM) |
| CI/CD | GitHub Actions, GitHub Container Registry (GHCR) |

---

## Limitações conhecidas

Algumas coisas dependem do iOS e **não têm solução do nosso lado**:

- **Não existe API pública** para um app definir o wallpaper do iPhone. O Atalhos é o único caminho.
- **O iPhone não pode ser acionado em segundo plano** por um servidor. Por isso o fluxo PC → iPhone depende de uma notificação e de um toque do usuário.
- **Web Push no iOS** só funciona com o PWA **instalado na tela inicial** (iOS 16.4 ou superior).
- **Live wallpapers** não existem mais no iOS, então wallpapers animados são só para o desktop.
- O Atalhos abre rapidamente na tela a cada troca.

Pontos ainda a validar em aparelho real estão listados no roadmap, na Fase 0.

---

## Estrutura do repositório

> Estrutura planejada. Ela passa a existir a partir da Fase 1.

```
dueto/
├── api/                      # Backend .NET
│   ├── src/
│   │   ├── Dueto.Api/            # Endpoints, hub SignalR, configuração
│   │   ├── Dueto.Core/           # Domínio e casos de uso
│   │   └── Dueto.Infrastructure/ # EF Core, Web Push, agendador
│   └── tests/
├── apps/
│   ├── web/                  # Angular PWA
│   └── desktop/              # Electron
├── infra/                    # docker-compose, Caddyfile, scripts de backup
├── shortcuts/                # Documentação e link do Atalho iOS
├── docs/                     # Decisões e diagramas
├── .tool-versions            # Versões fixadas (asdf)
├── CLAUDE.md
└── README.md
```

---

## Como rodar em desenvolvimento

> Os comandos abaixo valem a partir da Fase 1, quando o esqueleto do projeto existir.

**Pré-requisitos:** Docker com Compose, .NET SDK 10, Node.js LTS. Se usa o [asdf](https://asdf-vm.com), rode `asdf install` na raiz para instalar as versões do `.tool-versions`.

### 1. Configurar o ambiente

- **Pasta:** raiz do repositório
- **Comando:** `cp .env.example .env` e depois preencha os valores
- **Verifique:** o arquivo `.env` existe e nenhuma variável obrigatória está vazia

Para gerar as chaves VAPID do Web Push: `npx web-push generate-vapid-keys` (qualquer pasta). Copie a chave pública e a privada para o `.env`.

### 2. Subir o banco

- **Pasta:** raiz do repositório
- **Comando:** `docker compose -f infra/docker-compose.dev.yml up -d db`
- **Verifique:** `docker compose -f infra/docker-compose.dev.yml ps` mostra o serviço `db` como `healthy`

### 3. Rodar a API

- **Pasta:** raiz do repositório
- **Comando:** `dotnet run --project api/src/Dueto.Api`
- **Verifique:** `curl http://localhost:5080/health` responde `Healthy`

### 4. Rodar o PWA

- **Pasta:** `apps/web`
- **Comando:** `npm install` e depois `npm start`
- **Verifique:** `http://localhost:4200` abre o catálogo

### 5. Rodar o app desktop

- **Pasta:** `apps/desktop`
- **Comando:** `npm install` e depois `npm start`
- **Verifique:** a janela abre com o mesmo catálogo e o ícone aparece na bandeja

### Testes

- **API:** na pasta `api`, rode `dotnet test`. Verifique que todos os testes passam.
- **PWA:** na pasta `apps/web`, rode `npm test`. Verifique que todos os testes passam.

---

## Variáveis de ambiente

| Variável | Descrição |
|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Credenciais do Postgres do compose |
| `ConnectionStrings__Default` | String de conexão da API com o banco |
| `Cors__AllowedOrigins` | Origens permitidas (domínio do PWA) |
| `Vapid__Subject` | Contato do servidor para o Web Push (`mailto:` ou URL) |
| `Vapid__PublicKey`, `Vapid__PrivateKey` | Chaves VAPID do Web Push |
| `Catalog__AllowedHosts` | Hosts permitidos para download dos wallpapers (GitHub Releases) |
| `DUETO_DOMAIN` | Domínio público usado pelo Caddy para emitir o certificado |

Nunca versione o `.env`. O repositório traz só o `.env.example`, sem valores reais.

---

## Deploy

O alvo é uma **EC2 de 1 GB de RAM** rodando tudo com Docker Compose.

Checklist de preparação do servidor:

- [ ] Swap de 1 a 2 GB criado
- [ ] Elastic IP associado à instância
- [ ] Domínio (ou subdomínio gratuito) apontando para o Elastic IP
- [ ] Security Group com 80 e 443 abertos, e SSH (22) só para o seu IP
- [ ] Docker e Docker Compose instalados
- [ ] `.env` de produção criado no servidor, fora do repositório

Fluxo de publicação:

1. O **GitHub Actions** compila a API e o PWA, e publica as imagens no **GHCR**. **Nada é compilado na EC2**, porque 1 GB não aguenta o build.
2. No servidor, na pasta `infra`, rode `docker compose -f docker-compose.prod.yml pull` e depois `docker compose -f docker-compose.prod.yml up -d`.
3. **Verifique:** `https://SEU_DOMINIO/health` responde `Healthy` e o certificado HTTPS é válido.

Cada serviço tem limite de memória (`mem_limit`) no compose, o banco não expõe porta para fora, e um `pg_dump` diário leva uma cópia para fora do servidor.

---

## Roadmap

- [ ] **Fase 0: Validação em aparelho real.** Atalho que aplica wallpaper (tela de bloqueio e inicial), deep link vindo do PWA, Web Push no PWA instalado
- [ ] **Fase 1: Fundação.** Repositório, compose de desenvolvimento, esqueleto da API, migrations, `/health`, CI básico
- [ ] **Fase 2: Catálogo.** Cadastro dos wallpapers, listagem na API e no PWA, arquivos no GitHub Releases
- [ ] **Fase 3: Pareamento e estado.** Código de pareamento, tokens, estado atual, hub SignalR
- [ ] **Fase 4: Desktop (estáticos).** Electron, protocolo `dueto://`, bandeja, aplicar wallpaper, reconexão automática
- [ ] **Fase 5: iPhone.** PWA instalável, Web Push, Atalho, lembretes, banner de pendência
- [ ] **Fase 6: Produção.** Deploy na EC2 com Caddy, backups, CI/CD completo
- [ ] **Fase 7: Animados.** Wallpapers animados no desktop, com pausa em jogos e tela cheia
- [ ] **Futuro:** Linux, Android, variantes por modelo de iPhone, auto-update do desktop

---

## Custo

O projeto foi desenhado para **custo zero**, sem nenhum serviço pago:

- Hospedagem na EC2 que já existe (confira no console da AWS até quando vale o seu plano gratuito)
- Arquivos no GitHub Releases
- HTTPS gratuito com Let's Encrypt
- Web Push é um protocolo aberto, sem custo por notificação
- Sem conta de desenvolvedor da Apple, porque o iPhone usa PWA e Atalhos
- O instalador do Windows não é assinado, então o SmartScreen pode exibir um aviso

---

## Segurança

- O protocolo `dueto://` aceita **apenas IDs do catálogo**, nunca URLs ou caminhos arbitrários
- Downloads só de hosts permitidos e com verificação de **checksum SHA-256**
- Tokens de dispositivo guardados como hash no banco
- CORS restrito ao domínio do PWA, HTTPS obrigatório, rate limit nos endpoints públicos
- Segredos só em variáveis de ambiente, nunca no repositório
- Electron com `contextIsolation` ativo e `nodeIntegration` desligado

Encontrou uma vulnerabilidade? Abra uma issue privada ou entre em contato diretamente, sem expor o problema publicamente.

---

## Contribuindo

O projeto está em fase de planejamento. As regras de código, commits e arquitetura estão em [`CLAUDE.md`](./CLAUDE.md) e valem para qualquer contribuição.

- Branches: `feat/...`, `fix/...`, `chore/...`
- Commits no padrão [Conventional Commits](https://www.conventionalcommits.org)
- Pull requests pequenos, com testes para qualquer regra de sincronização

---

## Licença

A definir.
