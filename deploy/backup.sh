#!/usr/bin/env bash
# Respaldo diario de IoBuild: MySQL (datos del negocio) e InfluxDB (telemetría).
# Uso: ./deploy/backup.sh            (desde la raíz del proyecto)
# Cron (todos los días 03:00):  0 3 * * * cd /opt/iobuild && ./deploy/backup.sh >> backups/backup.log 2>&1
set -euo pipefail
cd "$(dirname "$0")/.."
set -a; . ./.env; set +a

COMPOSE="docker compose -f docker-compose.prod.yml"
KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"
STAMP="$(date +%Y%m%d-%H%M%S)"
DIR="backups/$STAMP"
mkdir -p "$DIR"

# --single-transaction: copia consistente sin bloquear la app
$COMPOSE exec -T -e MYSQL_PWD="$DB_ROOT_PASSWORD" mysql-monolith \
  mysqldump -uroot --single-transaction --routines --triggers iobuild | gzip > "$DIR/iobuild.sql.gz"

$COMPOSE exec -T influxdb sh -c "rm -rf /tmp/bk && influx backup /tmp/bk -t \"\$DOCKER_INFLUXDB_INIT_ADMIN_TOKEN\" >/dev/null && tar -C /tmp -czf - bk" > "$DIR/influx.tar.gz"

chmod -R go-rwx backups
find backups -mindepth 1 -maxdepth 1 -type d -mtime +"$KEEP_DAYS" -exec rm -rf {} +
echo "$(date -Is) respaldo listo en $DIR ($(du -sh "$DIR" | cut -f1))"
