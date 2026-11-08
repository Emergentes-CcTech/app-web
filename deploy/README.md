# Despliegue de IoBuild en producción

Servidor: laptop con Debian 13. La app queda publicada con HTTPS en `https://<equipo>.<tailnet>.ts.net` mediante **Tailscale Funnel**, sin abrir puertos del router y sin comprar dominio.

```
Internet ──HTTPS──▶ Tailscale Funnel ──▶ 127.0.0.1:8080 (nginx) ──▶ frontend / API
                                                                  └─ MySQL, InfluxDB, MQTT, simulador (red interna de Docker)
```

Solo nginx escucha, y solo en la propia laptop. MySQL, InfluxDB, el broker MQTT y la API no se pueden alcanzar desde fuera.

## 1. Requisitos de la laptop

- Debian 13, al menos 4 GB de RAM (8 GB recomendado) y 15 GB libres.
- Conectada a la corriente y por cable o con Wi‑Fi estable.
- Una cuenta de Tailscale (el plan personal gratuito alcanza).

### Que no se suspenda al cerrar la tapa

```bash
sudo sed -i 's/^#\?HandleLidSwitch=.*/HandleLidSwitch=ignore/; s/^#\?HandleLidSwitchExternalPower=.*/HandleLidSwitchExternalPower=ignore/' /etc/systemd/logind.conf
sudo systemctl mask sleep.target suspend.target hibernate.target hybrid-sleep.target
sudo systemctl restart systemd-logind
```

## 2. Instalar Docker

```bash
sudo apt update && sudo apt install -y ca-certificates curl git
sudo install -m 0755 -d /etc/apt/keyrings
sudo curl -fsSL https://download.docker.com/linux/debian/gpg -o /etc/apt/keyrings/docker.asc
echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/debian $(. /etc/os-release && echo $VERSION_CODENAME) stable" | sudo tee /etc/apt/sources.list.d/docker.list
sudo apt update && sudo apt install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
sudo systemctl enable --now docker
sudo usermod -aG docker $USER   # cerrar sesión y volver a entrar
```

## 3. Descargar el proyecto y configurar los secretos

```bash
sudo mkdir -p /opt/iobuild && sudo chown $USER /opt/iobuild
git clone -b develop <url-del-repositorio> /opt/iobuild
cd /opt/iobuild
cp deploy/.env.prod.example .env
chmod 600 .env
```

Generar un valor distinto para cada secreto (`JWT_SECRET`, `DB_ROOT_PASSWORD`, `DB_PASSWORD`, `INFLUX_PASSWORD`, `INFLUX_TOKEN`, `DEVICE_INGEST_KEY`):

```bash
for v in JWT_SECRET DB_ROOT_PASSWORD DB_PASSWORD INFLUX_PASSWORD INFLUX_TOKEN DEVICE_INGEST_KEY; do
  sed -i "s|^$v=.*|$v=$(openssl rand -base64 48 | tr -d '/+=' | cut -c1-40)|" .env
done
```

Luego completar con `nano .env`: `GEMINI_API_KEY`, Cloudinary y, si se usa, Stripe en modo prueba. `PUBLIC_URL` se completa en el paso 4.

> Las contraseñas de MySQL e InfluxDB se fijan la **primera vez** que se crean sus volúmenes. Si se cambian después, hay que cambiarlas también dentro de cada base.

## 4. Publicar con Tailscale Funnel

```bash
curl -fsSL https://tailscale.com/install.sh | sh
sudo tailscale up
sudo tailscale set --hostname=iobuild
```

En la consola de Tailscale (https://login.tailscale.com/admin/dns) activar **HTTPS Certificates**. La primera vez que se ejecute `funnel`, Tailscale muestra un enlace para habilitar Funnel en este equipo:

```bash
sudo tailscale funnel --bg 8080
tailscale funnel status          # muestra la dirección pública
```

Copiar esa dirección (por ejemplo `https://iobuild.tail1234.ts.net`) en `PUBLIC_URL` dentro de `.env`. La configuración `--bg` se mantiene después de reiniciar la laptop.

## 5. Levantar la aplicación

```bash
docker compose -f docker-compose.prod.yml up -d --build
docker compose -f docker-compose.prod.yml ps
curl -s http://127.0.0.1:8080/health
```

La primera construcción tarda varios minutos. La API aplica las migraciones y carga los datos de ejemplo al iniciar. Abrir `PUBLIC_URL` en el navegador.

Cuentas de demostración: las del seed (administrador y residente). Sus contraseñas son públicas en el repositorio: sirven para la demo, pero no se deben usar con datos reales de vecinos.

## 6. Respaldos

```bash
chmod +x deploy/backup.sh deploy/restore.sh
./deploy/backup.sh                       # prueba manual
(crontab -l 2>/dev/null; echo "0 3 * * * cd /opt/iobuild && ./deploy/backup.sh >> backups/backup.log 2>&1") | crontab -
```

Cada respaldo queda en `backups/<fecha>/` (MySQL comprimido y telemetría de InfluxDB) y se guardan 14 días. Conviene copiar la carpeta a otro equipo o a la nube de vez en cuando: si el disco de la laptop falla, se pierde todo lo que solo está ahí.

Restaurar la base: `./deploy/restore.sh backups/<fecha>`.

## 7. Actualizar a una nueva versión

```bash
cd /opt/iobuild
./deploy/backup.sh
git pull
docker compose -f docker-compose.prod.yml up -d --build
docker image prune -f
```

## Operación diaria

| Tarea | Comando |
|---|---|
| Ver estado | `docker compose -f docker-compose.prod.yml ps` |
| Ver registros de la API | `docker compose -f docker-compose.prod.yml logs -f --tail 100 iobuild-api` |
| Reiniciar un servicio | `docker compose -f docker-compose.prod.yml restart iobuild-api` |
| Apagar todo | `docker compose -f docker-compose.prod.yml down` (los datos se conservan) |
| Quitar la publicación | `sudo tailscale funnel --https=443 off` |

## Qué cambia respecto al entorno de desarrollo

- Sin contraseñas por defecto: si falta un secreto en `.env`, `docker compose` se detiene y dice cuál.
- La API se conecta a MySQL con el usuario `iobuild`, no con `root`.
- CORS solo permite `PUBLIC_URL`.
- `POST /api/v1/devices/telemetry` exige la cabecera `X-Device-Key` con `DEVICE_INGEST_KEY`. El simulador envía por MQTT dentro de la red interna, así que no la necesita.
- Los registros de cada contenedor rotan (3 archivos de 10 MB).

## Límites de este montaje

- La disponibilidad depende de la laptop: si se apaga, se cae la app.
- Funnel tiene límites de ancho de banda: alcanza para una demo y un piloto pequeño, no para muchos edificios.
- Swagger (`/swagger`) queda visible. Muestra la forma de la API, pero no permite llamar a los endpoints protegidos sin token.
