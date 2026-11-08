#!/usr/bin/env bash
# Restaura la base MySQL desde un respaldo:  ./deploy/restore.sh backups/20261107-030000
# Reemplaza los datos actuales de la base iobuild.
set -euo pipefail
cd "$(dirname "$0")/.."
[ -f "${1:-}/iobuild.sql.gz" ] || { echo "Uso: $0 backups/<fecha>"; exit 1; }
set -a; . ./.env; set +a
read -r -p "Esto reemplaza la base iobuild con $1. ¿Continuar? (si/no) " ok
[ "$ok" = "si" ] || exit 1
gunzip -c "$1/iobuild.sql.gz" | docker compose -f docker-compose.prod.yml exec -T -e MYSQL_PWD="$DB_ROOT_PASSWORD" mysql-monolith mysql -uroot iobuild
echo "Base restaurada desde $1"
