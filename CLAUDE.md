# CLAUDE.md — Dueto

Guia para qualquer agente de código (e para humanos) que trabalhe neste repositório. Leia inteiro antes de mexer. Se algo aqui conflitar com um pedido do dono do projeto, **o pedido vence**, mas avise sobre o conflito.

---

## 1. O que é o projeto

**Dueto** é um hub de wallpapers autorais (estáticos e animados) que sincroniza o papel de parede entre **PC** e **iPhone**.

- O usuário escolhe um wallpaper no catálogo e clica em "Definir wallpaper".
- No aparelho atual ele é aplicado; no outro, a troca é propagada.
- PC → iPhone: notificação Web Push, toque do usuário, Atalho do iOS aplica.
- iPhone → PC: troca instantânea e automática pelo SignalR.
- Wallpapers animados existem **só no desktop**.

Não é afiliado ao Wallpaper Engine. Nunca use o nome, a marca ou o sufixo "Engine" em nada do projeto.

---

## 2. Como trabalhar neste projeto (regras de colaboração)

1. **Idioma:** converse em português do Brasil. Código, nomes de classes, variáveis, commits e comentários técnicos em **inglês**. Documentação (README, docs) em português.
2. **Avise antes de gerar algo grande** (documento longo, vários arquivos, diagramas). Diga o que vai gerar e se é leve ou pesado, e espere a decisão do dono. Tarefas pequenas e pontuais, só faça.
3. **Ao pedir que o dono rode um comando**, sempre diga **em qual pasta** rodar e **o que ele precisa verificar** para confirmar que a etapa está correta antes de seguir.
4. **Ao explicar conceitos**, vá fundo tecnicamente, mas sem formalismo matemático: prefira intuição, analogia e exemplo concreto.
5. **Frontend é Angular.** Nunca sugira, gere ou compare com React nesta base.
6. **Não reabra decisões fechadas** (seção 3) sem o dono pedir. Se achar que uma decisão está errada, diga o motivo em poucas linhas e pergunte.
7. **Não invente comportamento do iOS.** Se algo depende do iOS (Atalhos, Web Push, deep link) e não foi validado, diga que precisa de teste em aparelho real. Veja a seção 19.
8. **Mudanças mínimas:** faça só o que foi pedido. Não refatore, renomeie ou "melhore" fora do escopo.
9. **Não adicione dependências** (NuGet ou npm) sem justificar. Uma lib nova precisa ser mantida, com licença compatível e **gratuita**.
10. **Não mexa em** `.env`, volumes do Docker, migrations já aplicadas, nem execute operações destrutivas (`docker volume rm`, `DROP`, `git reset --hard`, `git push --force`) sem confirmação explícita.
11. **Mantenha a documentação viva:** se uma decisão mudar, atualize este arquivo e o README no mesmo PR.

---

## 3. Decisões fechadas

| Tema | Decisão |
|---|---|
| Nome | **Dueto** |
| Backend | .NET (ASP.NET Core), com SignalR para tempo real |
| Frontend | Angular como PWA |
| Desktop | Electron embutindo o mesmo Angular |
| Banco | PostgreSQL em container (não usar Neon, Cloudflare, Render nem Vercel) |
| Hospedagem | EC2 de 1 GB de RAM, tudo via Docker Compose |
| HTTPS | Caddy com Let's Encrypt |
| Arquivos dos wallpapers | GitHub Releases; o banco guarda URL e checksum |
| iPhone | PWA instalado + app Atalhos. **Sem app nativo**, sem conta Apple paga |
| PC → iPhone | Web Push, com lembretes e banner de pendência |
| iPhone → PC | SignalR, automático e instantâneo |
| Pareamento | Código curto, sem contas e sem senha |
| Animados | Só desktop, Windows primeiro, **por último** no roadmap |
| Custo | **Zero.** Nenhum serviço pago |
| Python | Não faz parte da stack. Processamento de mídia é feito antes do upload, no computador do dono (ImageSharp/SkiaSharp ou `ffmpeg`) |
| Build | Nunca compilar na EC2. GitHub Actions publica imagens no GHCR |

---

## 4. Stack e versões

- **.NET 10 (LTS)**, ASP.NET Core, SignalR, EF Core + Npgsql
- **Angular** (versão estável atual, ver `apps/web/package.json`), `@angular/pwa`
- **Electron** (estável atual), TypeScript
- **PostgreSQL 16**
- **Node.js LTS**
- **Docker + Docker Compose**, **Caddy 2**
- Versões fixadas em `.tool-versions` (asdf). Instale dependências de desenvolvimento via asdf quando possível.

---

## 5. Estrutura do repositório

```
dueto/
├── api/
│   ├── src/
│   │   ├── Dueto.Api/             # Endpoints, hub SignalR, DI, configuração
│   │   ├── Dueto.Core/            # Domínio e casos de uso (sem dependência de infra)
│   │   └── Dueto.Infrastructure/  # EF Core, Web Push, agendador, acesso externo
│   └── tests/
├── apps/
│   ├── web/                       # Angular PWA
│   └── desktop/                   # Electron (main, preload, renderer)
├── infra/                         # docker-compose.*.yml, Caddyfile, scripts de backup
├── shortcuts/                     # Documentação e link do Atalho iOS
├── docs/                          # Decisões (ADRs) e diagramas
├── .tool-versions
├── CLAUDE.md
└── README.md
```

Dependências entre projetos .NET: `Api → Core`, `Infrastructure → Core`, `Api → Infrastructure` (só para registrar DI). **`Core` não referencia nada de fora.**

---

## 6. Comandos

Sempre indique a pasta ao pedir para o dono rodar.

| O quê | Pasta | Comando | Verificar |
|---|---|---|---|
| Subir o banco (dev) | raiz | `docker compose -f infra/docker-compose.dev.yml up -d db` | `ps` mostra `db` como `healthy` |
| Rodar a API | raiz | `dotnet run --project api/src/Dueto.Api` | `GET /health` responde `Healthy` |
| Testes da API | `api` | `dotnet test` | todos passam |
| Nova migration | raiz | `dotnet ef migrations add <Nome> -p api/src/Dueto.Infrastructure -s api/src/Dueto.Api` | arquivo gerado e revisado; nenhum `DROP` inesperado |
| Aplicar migrations | raiz | `dotnet ef database update -p api/src/Dueto.Infrastructure -s api/src/Dueto.Api` | tabelas criadas no banco |
| Rodar o PWA | `apps/web` | `npm start` | `http://localhost:4200` abre |
| Testes do PWA | `apps/web` | `npm test` | todos passam |
| Rodar o desktop | `apps/desktop` | `npm start` | janela abre e ícone aparece na bandeja |
| Chaves VAPID | qualquer | `npx web-push generate-vapid-keys` | par de chaves exibido |

Antes de qualquer PR: build, testes e lint passando nos três projetos.

---

## 7. Modelo de dados

Banco PostgreSQL, nomes em `snake_case`, chaves em UUID, datas em UTC (`timestamptz`).

- **`wallpapers`**: `id` (slug), `title`, `kind` (`static` | `animated`), `desktop_url`, `desktop_sha256`, `mobile_url`, `mobile_sha256`, `thumbnail_url`, `tags`, `created_at`
- **`pairs`**: `id`, `created_at`
- **`pairing_codes`**: `code`, `pair_id`, `expires_at`, `used_at`
- **`devices`**: `id`, `pair_id`, `kind` (`desktop` | `mobile`), `name`, `token_hash`, `last_seen_at`
- **`pair_state`**: `pair_id`, `wallpaper_id`, `version` (contador crescente), `origin_device_id`, `updated_at`
- **`device_applied`**: `device_id`, `wallpaper_id`, `version`, `applied_at`
- **`push_subscriptions`**: `device_id`, `endpoint`, `p256dh`, `auth`
- **`reminders`**: `id`, `device_id`, `version`, `sequence` (1 a 3), `due_at`, `status` (`pending` | `sent` | `cancelled`)

Regra: **o estado dos lembretes vive no banco**, nunca só em memória, para sobreviver a reinício do container.

---

## 8. Contratos

### REST (prefixo `/api`)

| Método e rota | Auth | Descrição |
|---|---|---|
| `GET /api/wallpapers` e `GET /api/wallpapers/{id}` | não | Catálogo |
| `POST /api/pairing/start` | não | Cria um par e devolve `{ deviceToken, code, expiresAt }`. Qualquer lado pode iniciar |
| `POST /api/pairing/complete` | não | Recebe `{ code, deviceName, kind }` e devolve `{ deviceToken }` |
| `GET /api/state` | sim | Estado atual do par: `wallpaperId`, `version`, `originDeviceId`, aplicados por dispositivo |
| `PUT /api/state` | sim | Define o wallpaper: `{ wallpaperId }`. Devolve a nova `version` |
| `POST /api/state/applied` | sim | Dispositivo confirma `{ wallpaperId, version }`. Cancela lembretes |
| `POST /api/push/subscriptions` e `DELETE` | sim (mobile) | Registra ou remove a assinatura Web Push |
| `GET /api/push/vapid-public-key` | não | Chave pública VAPID, que o PWA usa em `PushManager.subscribe`. `503` se o Web Push não estiver configurado |
| `GET /health` | não | Saúde do serviço |

- Autenticação: `Authorization: Bearer <deviceToken>`. Token aleatório de 256 bits; no banco fica só o **hash**.
- Erros no formato **ProblemDetails** (RFC 7807). Nunca vazar stack trace ou detalhe interno.
- Códigos de pareamento: curtos, de uso único, com validade de poucos minutos.

### Hub SignalR (`/hub/sync`)

- Servidor → cliente: `StateChanged { wallpaperId, version, originDeviceId }`.
- Ao conectar, o servidor envia um **snapshot** do estado atual.
- Autenticação pelo token no `access_token` da conexão (padrão do SignalR para WebSocket).

### Protocolo `dueto://` (desktop)

- Formato: `dueto://apply?id=<wallpaper-id>`.
- O `id` deve casar com `^[a-z0-9-]{1,64}$` e **existir no catálogo**. O app busca a URL do arquivo na API; **nunca** usa URL vinda do link.
- Instância única do app (`requestSingleInstanceLock`).

### Web Push

- Payload mínimo: `wallpaperId`, `version`. Sem dados sensíveis.
- `tag` fixa (`dueto-sync`) para a notificação nova substituir a anterior.
- Clique abre a rota `/apply` do PWA, que tenta abrir o Atalho. Como o iOS pode exigir um toque, a tela tem o botão **"Aplicar agora"** como alternativa.

### Atalho iOS

- Aberto por `shortcuts://run-shortcut?name=Dueto&input=text&text=<url>`.
- Passos: baixar a imagem da URL → definir wallpaper → `POST /api/state/applied` com o token do dispositivo.
- O Atalho tem **número de versão**; o PWA compara e avisa se estiver desatualizado.

---

## 9. Regras de sincronização

1. **`version` manda.** Todo `PUT /api/state` incrementa a versão do par. Um dispositivo ignora eventos com versão menor ou igual à que já aplicou.
2. **Sem eco.** O evento carrega `originDeviceId`. Quem originou a troca não reage ao próprio evento.
3. **Confirmação explícita.** Todo dispositivo chama `POST /api/state/applied` depois de aplicar, inclusive o desktop.
4. **Idempotência.** Aplicar o mesmo `(wallpaperId, version)` duas vezes não faz nada de novo.
5. **Troca originada no desktop:** o servidor marca o desktop como aplicado, envia o push ao mobile e cria os lembretes.
6. **Troca originada no mobile:** o servidor notifica o desktop pelo hub. Não cria lembretes de push, porque o usuário está com o celular na mão; o banner de pendência vale até o Atalho confirmar.
7. **Lembretes:** padrão de 3 reenvios em +2 min, +15 min e +1 h, configuráveis. Um novo `version` **cancela** os lembretes pendentes das versões anteriores. Uma confirmação cancela os restantes.
8. **Agendador:** um `BackgroundService` consulta a tabela `reminders` em intervalo curto e dispara os vencidos. Use `TimeProvider` para poder testar sem esperar.
9. **Desktop reconecta sozinho** (espera crescente entre tentativas) e, ao conectar, aplica o snapshot se for mais novo que o último aplicado.
10. **Banner no PWA:** aparece quando o estado desejado do par é diferente do último aplicado pelo celular, com botão **"Aplicar agora"**.

Toda mudança nessas regras exige teste automatizado.

---

## 10. Convenções .NET

- `Nullable` habilitado, `ImplicitUsings` conforme o padrão do template, warnings como erro no CI.
- Namespaces com escopo de arquivo; `record` para DTOs e contratos.
- **Minimal APIs** organizadas por funcionalidade (um arquivo de endpoints por feature). Endpoint só traduz HTTP; a lógica fica em `Core`.
- Assíncrono de ponta a ponta, recebendo e repassando `CancellationToken`.
- Validação explícita na borda; nunca confiar em dado do cliente.
- Configuração pelo Options pattern, lida de variáveis de ambiente. Nada de valor fixo no código.
- Tempo sempre via `TimeProvider`, nunca `DateTime.UtcNow` direto na lógica.
- EF Core: configuração por entidade (`IEntityTypeConfiguration`), índices explícitos, consultas de leitura com `AsNoTracking`. Migrations **revisadas à mão** antes de commitar.
- Logs estruturados com `ILogger`. **Nunca logar** token, código de pareamento, chave VAPID ou dado de assinatura push.
- Nomes de tipos e métodos claros; sem abreviações obscuras; sem comentário que só repete o código.

---

## 11. Convenções Angular e PWA

- **Standalone components**, `ChangeDetectionStrategy.OnPush`, **signals** para estado local, RxJS só para fluxos de verdade (eventos do SignalR, HTTP).
- TypeScript em modo `strict`. Sem `any` sem justificativa.
- Rotas com carregamento sob demanda; formulários tipados.
- Cliente HTTP centralizado, com interceptor que anexa o token e trata 401.
- A URL base da API vem do ambiente de build, nunca fixa no código.
- Service worker configurado em `ngsw-config.json`; notificações via `SwPush`.
- O mesmo código roda no navegador, no PWA e dentro do Electron. Detecte o ambiente por uma abstração (`PlatformService`), não espalhe `if (electron)` pelos componentes.
- Interface em português do Brasil, acessível (contraste, foco, rótulos) e **mobile first**.
- O PWA precisa de HTTPS; manifest com ícones e `display: standalone`.

---

## 12. Convenções Electron

- `contextIsolation: true`, `nodeIntegration: false`, `sandbox: true`.
- O `preload` expõe uma API **mínima** via `contextBridge`. Todo payload de IPC é validado no processo principal.
- CSP restritiva na janela. Nenhum conteúdo remoto carregado fora do domínio do Dueto.
- Downloads: só de hosts da allowlist (`Catalog__AllowedHosts`), com limite de tamanho, **verificação de SHA-256** e gravação na pasta de dados do app. Nunca executar o arquivo baixado.
- Aplicar wallpaper por biblioteca mantida e multiplataforma (candidata: o pacote npm `wallpaper`; validar na Fase 4). Windows primeiro; Linux como melhor esforço.
- App na bandeja, instância única, reconexão automática do SignalR.
- Animados (Fase 7): janela atrás dos ícones, **pausa** quando houver app em tela cheia ou jogo, e opção de reduzir consumo na bateria.

---

## 13. Docker e infraestrutura

- Arquivos: `infra/docker-compose.dev.yml` (só dependências) e `infra/docker-compose.prod.yml`.
- Serviços de produção: `caddy`, `api`, `db`. O Angular compilado é servido estático pelo Caddy.
- Cada serviço com **`mem_limit`**, `restart: unless-stopped` e **healthcheck**. Pontos de partida: Caddy 64 MB, API 300 MB, banco 256 MB. Ajuste depois de medir.
- O banco **não publica porta**; só a API o acessa pela rede interna.
- Volume **nomeado** para os dados do Postgres.
- Rotação de logs do Docker (`max-size`, `max-file`) para não encher o disco.
- Postgres ajustado para pouca memória: `max_connections` baixo e `shared_buffers` pequeno.
- Imagens sempre versionadas por tag do GHCR; nada de `latest` em produção sem necessidade.
- Backup: `pg_dump` diário agendado e cópia **fora do servidor**. Teste a restauração pelo menos uma vez.
- Backup: `infra/backup/backup.sh` (dump verificado, rotação, cópia externa via `rclone` para um destino gratuito à escolha do dono) e `infra/backup/restore.sh` (restaura num banco de verificação; sobrescrever o banco real exige `CONFIRM_OVERWRITE=yes`).
- A imagem da API não tem o SDK, então ela aplica as migrations ao iniciar quando `Database__MigrateOnStartup=true` (ligado só no compose de produção; pensado para uma única instância).
- Atrás do Caddy, a API só confia no `X-Forwarded-For` vindo da sub-rede fixa do compose (`ForwardedHeaders__KnownNetworks__0`). Sem isso o rate limit por IP trata todos como um só.
- O catálogo real vem de `infra/catalog.json`, importado por upsert pela API a cada subida (`Catalog__ImportFile`), com validação de slug, SHA-256, URLs `https` e hosts permitidos (`Catalog__AllowedHosts`, padrão `github.com`). Nunca remove wallpapers. Um catálogo inválido impede a API de subir. Exemplo para dev: `api/samples/catalog.sample.json`.
- Wallpaper animado é só do desktop: o celular não pode defini-lo nem confirmá-lo (`422`), e uma troca animada vinda do desktop não gera push nem lembretes.
- Guia de deploy: `infra/README.md`.

---

## 14. Segurança (inegociável)

1. `dueto://` aceita só **IDs do catálogo**. Nunca URL, caminho ou comando.
2. Downloads só de hosts da allowlist, com SHA-256 conferido.
3. Tokens de dispositivo: aleatórios (256 bits), guardados como hash, revogáveis.
4. Segredos (senhas, chaves VAPID, strings de conexão) **somente em variáveis de ambiente**. Nunca no repositório, em log ou em mensagem de erro.
5. CORS restrito ao domínio do PWA. HTTPS obrigatório; WebSocket só em `wss://`.
6. Rate limit nos endpoints públicos, principalmente `pairing/start` e `pairing/complete`.
7. Código de pareamento: uso único, expira rápido, com limite de tentativas.
8. Security Group da EC2: 80 e 443 abertos, **SSH só do IP do dono**.
9. Dependências atualizadas; sem pacote abandonado.
10. Nenhum dado pessoal além do necessário: o sistema não tem conta, e-mail nem senha.

---

## 15. Recursos limitados e custo zero

- A EC2 tem **1 GB de RAM**. Pense em memória antes de adicionar qualquer coisa ao servidor. Swap de 1 a 2 GB é obrigatório.
- **Nunca compilar na EC2.** Build e testes rodam no GitHub Actions; o servidor só faz `pull` e `up`.
- **Nenhum serviço pago.** Antes de propor qualquer serviço ou API externa, diga o custo e os limites do plano gratuito. Se pedir cartão ou tiver prazo de teste, não use.
- Vídeos são pesados: ficam no GitHub Releases e só o desktop os baixa.
- Processamento de imagem e vídeo é feito **offline**, no computador do dono, antes do upload.

---

## 16. Testes

- **API:** xUnit para regras de negócio; testes de integração com `WebApplicationFactory` e Postgres real em container.
- **Obrigatório testar:** versão e ordenação de estado, ausência de eco, idempotência, criação e cancelamento de lembretes, expiração e uso único do código de pareamento, validação do `id` do `dueto://`.
- **PWA:** testes unitários dos serviços e do banner de pendência. E2E (Playwright) a partir da Fase 5.
- **Desktop:** testes unitários do parser do protocolo e da verificação de checksum.
- Bug corrigido ganha teste que o reproduz.
- Nada de teste que dependa de internet ou de dispositivo físico no CI.

---

## 17. Git e Pull Requests

- Branch principal protegida. Trabalho em `feat/...`, `fix/...`, `chore/...`, `docs/...`.
- Commits no padrão **Conventional Commits**, em inglês (`feat(api): add pairing endpoints`).
- PRs pequenos, com um objetivo só e descrição curta do que mudou e como verificar.
- Checklist do PR: build, testes e lint passam; migrations revisadas; nenhum segredo no diff; README e CLAUDE.md atualizados se a decisão mudou.
- Merge por squash.
- CI (GitHub Actions): build + testes + lint em todo PR; publicação de imagens no GHCR só a partir da branch principal ou de tag.

---

## 18. Fases e definição de pronto

| Fase | Entrega | Pronto quando |
|---|---|---|
| 0 | Validação no iPhone | Atalho aplica wallpaper; deep link do PWA abre o Atalho; Web Push chega no PWA instalado |
| 1 | Fundação | `docker compose up` sobe o banco, a API responde `/health`, migrations aplicam, CI verde |
| 2 | Catálogo | PWA lista os wallpapers vindos da API; arquivos baixam do GitHub Releases |
| 3 | Pareamento e estado | Dois clientes pareiam por código e recebem `StateChanged` em tempo real |
| 4 | Desktop estáticos | `dueto://apply` troca o wallpaper no Windows; reconecta e aplica o pendente |
| 5 | iPhone | Troca no PC gera push; toque aplica; lembretes e banner funcionam; confirmação cancela os lembretes |
| 6 | Produção | EC2 no ar com HTTPS, backup testado, deploy por `pull` + `up` |
| 7 | Animados | Wallpaper animado no desktop, pausa em tela cheia, consumo aceitável |

Não avance de fase sem os critérios acima verificados.

---

## 19. Pontos em aberto e riscos

A validar em **aparelho real** na Fase 0:

- Comportamento atual da ação "Definir Papel de Parede" do Atalhos (tela de bloqueio versus inicial, confirmação ao rodar).
- Se o PWA consegue abrir `shortcuts://` logo após o toque na notificação, ou se exige um toque extra.
- Entrega do Web Push no iOS com Modo de Foco e economia de bateria.

Outros pontos:

- Escolha da biblioteca para aplicar wallpaper no Windows e no Linux (Fase 4).
- Como renderizar atrás dos ícones e pausar em tela cheia (Fase 7).
- Limites atuais do plano gratuito da AWS para a EC2 e o tráfego: conferir no console de Billing.
- Licença do repositório: a definir.

---

## 20. Glossário

- **Par:** conjunto formado por um desktop e um celular que compartilham o mesmo estado.
- **Estado:** o wallpaper atual desejado de um par, com sua `version`.
- **Origem:** o dispositivo que fez a troca mais recente.
- **Aplicado:** o dispositivo confirmou que o wallpaper está na tela.
- **Atalho:** o atalho do app Atalhos da Apple que baixa e aplica a imagem.
- **Lembrete:** reenvio programado da notificação enquanto o celular não confirmar.
- **Banner de pendência:** aviso no PWA de que o celular ainda não aplicou o wallpaper atual.
