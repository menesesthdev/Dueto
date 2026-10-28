#!/usr/bin/env bash
# Restaura um dump do Dueto.
#
# Uso:   restore.sh <arquivo.dump> [banco-destino]
#
# Sem banco-destino, restaura em "<banco>_restore_check": um banco de VERIFICAÇÃO, separado do real.
# É assim que se testa se o backup presta, sem risco. Restaurar por cima do banco real exige
# CONFIRM_OVERWRITE=yes e a API parada (docker compose stop api), pois o banco de destino é apagado.
#
# Variáveis: COMPOSE_FILE, ENV_FILE, DB_SERVICE, POSTGRES_USER, POSTGRES_DB (mesmas do backup.sh).
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$HERE/../docker-compose.prod.yml}"
ENV_FILE="${ENV_FILE:-$HERE/../.env}"
DB_SERVICE="${DB_SERVICE:-db}"
POSTGRES_USER="${POSTGRES_USER:-dueto}"
POSTGRES_DB="${POSTGRES_DB:-dueto}"

log() { printf '%s restore: %s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$*"; }
die() { log "ERRO: $*" >&2; exit 1; }

dump="${1:-}"
target="${2:-${POSTGRES_DB}_restore_check}"

[[ -n "$dump" ]] || die "uso: restore.sh <arquivo.dump> [banco-destino]"
[[ -f "$dump" ]] || die "arquivo não encontrado: $dump"
# O nome entra em SQL: só letras, números e sublinhado.
[[ "$target" =~ ^[A-Za-z0-9_]+$ ]] || die "nome de banco inválido: $target"
[[ "$POSTGRES_DB" =~ ^[A-Za-z0-9_]+$ ]] || die "POSTGRES_DB inválido"

if [[ "$target" == "$POSTGRES_DB" && "${CONFIRM_OVERWRITE:-}" != "yes" ]]; then
  die "isto APAGA e recria o banco real '$POSTGRES_DB'. Pare a API e rode de novo com CONFIRM_OVERWRITE=yes"
fi

compose=(docker compose -f "$COMPOSE_FILE")
[[ -f "$ENV_FILE" ]] && compose=(docker compose --env-file "$ENV_FILE" -f "$COMPOSE_FILE")
psql_admin=("${compose[@]}" exec -T "$DB_SERVICE" psql -U "$POSTGRES_USER" -d postgres -v ON_ERROR_STOP=1 -q)

"${compose[@]}" exec -T "$DB_SERVICE" pg_restore --list < "$dump" > /dev/null || die "o dump está corrompido ou não é um dump do pg_dump"

log "recriando o banco '$target'"
"${psql_admin[@]}" -c "DROP DATABASE IF EXISTS \"$target\" WITH (FORCE)"
"${psql_admin[@]}" -c "CREATE DATABASE \"$target\""

log "restaurando $dump em '$target'"
"${compose[@]}" exec -T "$DB_SERVICE" pg_restore -U "$POSTGRES_USER" -d "$target" --no-owner --exit-on-error < "$dump"

log "conferência (linhas por tabela):"
"${compose[@]}" exec -T "$DB_SERVICE" psql -U "$POSTGRES_USER" -d "$target" -At -F ' = ' -c \
  "SELECT 'wallpapers', count(*) FROM wallpapers UNION ALL SELECT 'pairs', count(*) FROM pairs UNION ALL SELECT 'devices', count(*) FROM devices UNION ALL SELECT 'reminders', count(*) FROM reminders"

log "restauração concluída em '$target'"
