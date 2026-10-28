# infra — Deploy de produção

Stack da EC2 (1 GB de RAM): **Caddy** (HTTPS) + **API** + **PostgreSQL**, tudo por Docker Compose.
A EC2 **nunca compila**: o GitHub Actions publica a imagem no GHCR e o servidor só faz `pull` e `up`.

```
internet ──443──► Caddy ──► api:8080 ──► db:5432
                    │         (rede interna "dueto", sem portas publicadas)
                    └──► arquivos estáticos do Angular (/srv/web)
```

## Arquivos

| Arquivo | Para quê |
|---|---|
| `docker-compose.dev.yml` | Só o Postgres, para desenvolvimento |
| `docker-compose.prod.yml` | `caddy`, `api` e `db` em produção |
| `Caddyfile` | HTTPS automático, proxy de `/api` e `/hub` (WebSocket), SPA |
| `.env.example` | Modelo das variáveis. Copie para `infra/.env` **na EC2** |
| `backup/backup.sh` | `pg_dump` + verificação + rotação + cópia externa (rclone) |
| `backup/restore.sh` | Restaura um dump (por padrão num banco de verificação) |

## Limites de memória (pontos de partida, já medidos)

| Serviço | `mem_limit` | Uso medido em repouso |
|---|---|---|
| caddy | 64 MB | ~17 MB |
| api | 300 MB | ~64 MB |
| db | 256 MB | ~20 MB |

Medido com `docker stats` numa subida local. Meça de novo na EC2, com carga real, e ajuste.

## 1. Preparar a EC2 (uma vez)

Isto é feito por você, no console da AWS e por SSH. Eu não consigo verificar daqui.

1. **Security Group:** abra as portas **80** e **443** para todos e a **22 (SSH) só para o seu IP**. Confira que não há mais nenhuma porta aberta (principalmente a 5432).
2. **DNS:** crie um registro `A` do seu domínio apontando para o IP da EC2. Confira com `dig +short seu-dominio` (deve devolver o IP). Sem isso o Let's Encrypt não emite o certificado.
3. **Swap de 1 a 2 GB (obrigatório, §15):**
   ```bash
   sudo fallocate -l 2G /swapfile && sudo chmod 600 /swapfile
   sudo mkswap /swapfile && sudo swapon /swapfile
   echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
   ```
   Confira com `free -h`: a linha `Swap` deve mostrar 2,0G.
4. **Docker e Compose** instalados. Confira com `docker compose version`.
5. **Login no GHCR** (a imagem é privada por padrão). Crie um token clássico com o escopo `read:packages` e rode
   `echo SEU_TOKEN | docker login ghcr.io -u SEU_USUARIO --password-stdin`. Confira a mensagem `Login Succeeded`.

## 2. Primeiro deploy

Na EC2, na pasta `infra/` do repositório clonado:

```bash
cp .env.example .env
nano .env          # preencha DUETO_DOMAIN, API_TAG, POSTGRES_PASSWORD (openssl rand -hex 24) e, se quiser push, as chaves VAPID
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

**O que conferir:**
- `docker compose -f docker-compose.prod.yml ps` mostra `db`, `api` e `caddy` como `healthy`.
- `curl https://SEU-DOMINIO/health` responde `Healthy` (o primeiro acesso pode levar alguns segundos enquanto o certificado é emitido).
- `curl https://SEU-DOMINIO/api/wallpapers` devolve o catálogo (as migrations rodam sozinhas na subida da API).
- Do seu computador, `nc -zv IP-DA-EC2 5432` **deve falhar**: o banco não pode estar exposto.

`API_TAG` é a tag publicada pelo GitHub Actions (`sha-abc1234` ou `v1.0.0`). Nunca use `latest`.

## 3. Atualizar

```bash
# edite API_TAG no .env para a nova tag, depois:
docker compose -f docker-compose.prod.yml pull
docker compose -f docker-compose.prod.yml up -d
```

As migrations novas são aplicadas quando a API sobe (`Database__MigrateOnStartup=true`). **Faça um backup antes de atualizar.**

## 4. Backup (§13)

**Dump diário com cópia fora do servidor.** Perder a EC2 perde os backups locais, então a cópia externa importa.

1. Instale o [rclone](https://rclone.org/) e configure um destino gratuito, que você escolhe (`rclone config`). Confira com `rclone lsd NOME:`.
2. Teste um backup manual (na pasta `infra/`):
   ```bash
   RCLONE_REMOTE=NOME:dueto-backups ./backup/backup.sh
   ```
   Confira: a saída termina com `cópia externa ok` e o arquivo aparece no destino (`rclone ls NOME:dueto-backups`).
3. Agende no cron (`crontab -e`), por exemplo todo dia às 03:00:
   ```
   0 3 * * * RCLONE_REMOTE=NOME:dueto-backups /opt/dueto/infra/backup/backup.sh >> /var/log/dueto-backup.log 2>&1
   ```

O script recusa um dump vazio ou ilegível, mantém 14 dias (`RETENTION_DAYS`) e falha **alto** se `RCLONE_REMOTE` estiver definido e o `rclone` não existir.

### Testar a restauração (faça pelo menos uma vez)

```bash
# na pasta infra/, restaura num banco de VERIFICAÇÃO (dueto_restore_check), sem tocar no real
./backup/restore.sh /var/backups/dueto/dueto-AAAAMMDDTHHMMSSZ.dump
```

Confira: ao final ele lista as linhas por tabela. Compare com o banco real. Para apagar o banco de verificação depois:
`docker compose -f docker-compose.prod.yml exec -T db psql -U dueto -d postgres -c 'DROP DATABASE "dueto_restore_check"'`.

Restaurar **por cima do banco real** exige parar a API e confirmar de forma explícita:
```bash
docker compose -f docker-compose.prod.yml stop api
CONFIRM_OVERWRITE=yes ./backup/restore.sh ARQUIVO.dump dueto
docker compose -f docker-compose.prod.yml start api
```

## Segurança desta configuração

- **Banco e API sem portas publicadas.** Só o Caddy expõe 80 e 443.
- **IP real do cliente.** O Caddy sobrescreve `X-Forwarded-For` e a API só confia nesse cabeçalho vindo da sub-rede do compose (`ForwardedHeaders__KnownNetworks__0`). Sem isso, o rate limit por IP trataria todos os usuários como um só.
- **Segredos só no `.env` da EC2** (gitignored). Nunca em log: o `access_token` do hub e as chaves não aparecem nos logs.
- **Imagem sem root** (usuário `app`), sem SDK, só o runtime.
- Em `DUETO_SUBNET`: se `172.30.0.0/24` conflitar com outra rede da sua EC2, troque. A API e o compose usam o mesmo valor.

## Pendências conhecidas

- **Dados de exemplo no catálogo.** A migration `SeedSampleWallpapers` insere 3 wallpapers **fictícios** (URLs `example.com`). Como a API aplica todas as migrations na subida, eles aparecerão em produção. Antes de abrir ao público é preciso uma migration que os remova, ou carregar o catálogo real.
- **`apps/web` ainda não existe.** O Caddy serve a pasta `infra/web/` (vazia por enquanto): qualquer rota fora de `/api`, `/hub` e `/health` devolve 404 até o Angular ser copiado para lá.
- **CSP** (Content-Security-Policy) ainda não foi definida; depende do front.
- **Validação com HTTPS real e iPhone** (§19) só acontece depois do primeiro deploy.
