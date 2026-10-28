#!/usr/bin/env bash
# Backup do Postgres do Dueto: pg_dump (formato custom, comprimido), verificação, rotação local
# e cópia opcional para fora do servidor via rclone.
#
# Agende no cron da EC2 (ex.: todo dia às 03:00):
#   0 3 * * * /opt/dueto/infra/backup/backup.sh >> /var/log/dueto-backup.log 2>&1
#
# Variáveis (todas opcionais):
#   COMPOSE_FILE    compose do banco            (padrão: ../docker-compose.prod.yml)
#   ENV_FILE        .env do compose             (padrão: ../.env, se existir)
#   DB_SERVICE      nome do serviço do banco    (padrão: db)
#   POSTGRES_USER / POSTGRES_DB                 (padrão: dueto)
#   BACKUP_DIR      pasta dos dumps locais      (padrão: /var/backups/dueto)
#   RETENTION_DAYS  dias de retenção            (padrão: 14)
#   RCLONE_REMOTE   destino externo, ex.: gdrive:dueto-backups (vazio = sem cópia externa)
set -euo pipefail
umask 077

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$HERE/../docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-$HERE/../.env}"
DB_SERVICE="${DB_SERVICE:-db}"
POSTGRES_USER="${POSTGRES_USER:-dueto}"
POSTGRES_DB="${POSTGRES_DB:-dueto}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/dueto}"
RETENTION_DAYS="${RETENTION_DAYS:-14}"
RCLONE_REMOTE="${RCLONE_REMOTE:-}"

log() { printf '%s backup: %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
die() { log "ERRO: $*" >&2; exit 1; }

[[ "$RETENTION_DAYS" =~ ^[0-9]+$ ]] || die "RETENTION_DAYS deve ser um número inteiro"
[[ -f "$COMPOSE_FILE" ]] || die "compose não encontrado: $COMPOSE_FILE"

compose=(docker compose -f "$COMPOSE_FILE")
[[ -f "$ENV_FILE" ]] && compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")

# Falha cedo se a cópia externa foi pedida mas o rclone não existe: melhor que perder o backup em silêncio.
if [[ -n "$RCLONE_REMOTE" ]] && ! command -v rclone >/dev/null 2>&1; then
  die "RCLONE_REMOTE definido, mas o rclone não está instalado"
fi

mkdir -p "$BACKUP_DIR"
chmod 700 "$BACKUP_DIR"

stamp="$(date -u +%Y%m%dT%H%M%SZ)"
final="$BACKUP_DIR/dueto-$stamp.dump"
partial="$final.partial"
trap 'rm -f "$partial"' EXIT

log "iniciando dump de $POSTGRES_DB"
"${compose[@]}" exec -T "$DB_SERVICE" pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner > "$partial"

[[ -s "$partial" ]] || die "o dump saiu vazio"

# Um dump que não consegue ser lido de volta não é backup. O pg_restore --list lê o índice do arquivo.
"${compose[@]}" exec -T "$DB_SERVICE" pg_restore --list < "$partial" > /dev/null || die "o dump gerado está corrompido"

mv "$partial" "$final"
log "dump ok: $final ($(du -h "$final" | cut -f1))"

find "$BACKUP_DIR" -maxdepth 1 -name 'dueto-*.dump' -type f -mtime "+$RETENTION_DAYS" -print -delete \
  | sed 's/^/removido (retenção): /' || true

if [[ -n "$RCLONE_REMOTE" ]]; then
  log "copiando para $RCLONE_REMOTE"
  rclone copyto "$final" "$RCLONE_REMOTE/$(basename "$final")"
  rclone delete "$RCLONE_REMOTE" --include 'dueto-*.dump' --min-age "${RETENTION_DAYS}d"
  log "cópia externa ok"
else
  log "AVISO: RCLONE_REMOTE vazio, sem cópia fora do servidor (perder a EC2 perde os backups)"
fi
