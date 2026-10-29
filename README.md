# IoBuild — Frontend

Plataforma de administración de condominios con medidores IoT en áreas comunes, prorrateo de gastos y cobranza.
Stack: Vue 3 · Pinia · PrimeVue · vue-i18n · Axios · Chart.js · Stripe. Estructura por contextos (`domain / application / infrastructure / presentation`).

## Contextos (`src/`)
| Carpeta | Contexto | Requisitos |
|---|---|---|
| `iam/` | Registro (administrador con RUC / residente con código de activación), login | RF-001, RF-002 |
| `profiles/` | Perfil por rol, notificaciones, seguridad | RF-003 |
| `condominiums/` | Condominios y unidades con alícuota (suma 100 %) | RF-004 a RF-006 |
| `residents/` | Propietarios/residentes y estado de cobranza | RF-006, RF-016 |
| `devices/` | Medidores de agua, sensores de corriente y gateway | RF-007 |
| `telemetry/` | Panel, monitoreo, histórico y alertas | RF-008 a RF-011 |
| `billing/` | Recibo matriz, conciliación, prorrateo y liquidación | RF-012, RF-013 |
| `payments/` | Estado de cuenta, pagos y morosidad | RF-016 |
| `audit/` | Historial inalterable y sustento de la liquidación | RF-015 |
| `subscriptions/` | Planes HaaS por condominio (Boutique, Urbano, Gran Torre) con Stripe | RF-014 |
| `shared/` | Layout, rutas, roles, validadores, constantes | transversal |

## Puesta en marcha
```bash
npm install
cp .env.example .env
npm run dev
```
Por defecto `.env` apunta al backend .NET local: `VITE_PROXY_TARGET=http://localhost:5080`. El servidor de Vite reenvía `/api/v1/*` a esa dirección.

Sin backend se puede usar el mock (json-server): en `.env` pon `VITE_PROXY_TARGET=http://localhost:3000` y ejecuta `npm run server` en otra terminal. Usuarios del mock (clave `Demo1234!`): `admin@vistaverde.pe` (administrador), `carla.f@email.com` (al día) y `miguel.v@email.com` (moroso). Códigos de activación libres: `LA-201-7K3Q`, `LA-202-9XPM`.

## Conectar al backend desplegado
La app llama a `VITE_API_URL` (en `.env.production` vale `/api/v1`) más las rutas `VITE_*_ENDPOINT_PATH`. Al desplegar el frontend:
- **Con la URL completa del backend:** define `VITE_API_URL=https://<backend>/api/v1` en las variables de entorno del hosting (Netlify o Vercel). El backend debe permitir CORS desde el dominio del frontend.
- **Con un proxy:** deja `VITE_API_URL=/api/v1` y redirige `/api/*` al backend. En Netlify basta con descomentar el bloque de `netlify.toml`; en Vercel se agrega un rewrite en `vercel.json` antes del de `index.html`.

Las variables `VITE_*` se leen al compilar: después de cambiarlas hay que volver a desplegar. Los contratos que el frontend espera del backend están en "Supuestos pendientes de confirmar con backend".

## Demo publicada sin backend
Con la variable `VITE_DEMO=true` la app responde las peticiones en el navegador con los datos de `server/db.json` (`src/shared/infrastructure/demo-api.js`), así se puede publicar en Vercel sin backend. Los cambios de cada visitante se guardan en su navegador; `window.resetIoBuildDemo()` en la consola vuelve a los datos iniciales. El pago con Stripe se simula activando el plan.
```bash
VITE_DEMO=true npm run build   # o en Vercel: variable de entorno VITE_DEMO=true
```

## Roles y vistas principales
- **Administrador**: Panel (resumen de sus condominios con departamentos, áreas comunes y estado de suscripción + consumo), Condominios (número de departamentos y áreas comunes) y Suscripción, además del resto de módulos.
- **Dueño de departamento**: Analytics (`/telemetry/analytics`), reporte del consumo de las áreas comunes de su condominio; además Mi consumo, Estado de cuenta y Auditoría.

Un medidor reporta el consumo de un área común cuando su punto de medición coincide con el tipo del área (por ejemplo, `pool` ↔ Piscina).

## Planes de suscripción (por condominio)
| Plan | Departamentos | Precio | Kit de equipos |
|---|---|---|---|
| Boutique | hasta 20 | S/ 500 | 4: 1 Gateway + 1 Caudalímetro + 2 Pinzas CT |
| Urbano | 21 a 50 | S/ 750 | 6: 1 Gateway + 2 Caudalímetros + 3 Pinzas CT |
| Gran Torre | 51 a 80 | S/ 1000 | 10: 2 Gateways + 3 Caudalímetros + 5 Pinzas CT |

Fuente única: `src/subscriptions/domain/model/plan-limits.js` (el backend puede enviar `/plans` con los mismos campos). Un condominio no puede registrar más de 80 departamentos.

## Variables de entorno
Ver `.env.example`. Tras cambiar `.env` reinicia `npm run dev`.

## Pruebas
`npm run test:unit` (reglas de negocio: prorrateo, participación 100 %, proyección, morosidad, planes, validadores) · `npm run test:e2e` (requiere backend real).

## Supuestos pendientes de confirmar con backend
- Roles en el JWT (`PropertyManager` / `administrator`, `resident`): `normalizeRole` acepta ambos.
- Endpoints: `/condominiums`, `/units`, `/devices`, `/readings`, `/alerts`, `/utility_bills`, `/settlements`, `/settlement_lines`, `/payments` (+ `/checkout-session`), `/audit_events`, `PATCH /users/{id}/password`, `GET/DELETE /sessions`.
- Cálculo oficial del prorrateo y generación de pagos al emitir: lo hace el servidor (en el mock solo se crean las líneas).
- Conversión de lecturas a m³ / kWh del panel: 1 muestra por minuto y 230 V monofásico (`telemetry/application/projection.js`).
- `/plans` con `id`, `code`, `name`, `minUnits`, `maxUnits`, `monthlyPen` y `kit` (`{ gateways, flowMeters, ctClamps }`). Si no existe, se usa el catálogo local de `plan-limits.js`.
- Suscripciones por condominio: `GET /subscriptions?administratorId=`, `POST /subscriptions/payments/sessions` (`{ administratorId, condominiumId, planId, successUrl, cancelUrl }` → `{ url }` de Stripe Checkout), `POST /subscriptions/{id}/cancel`, `GET /subscriptions/payments/invoices`.
- El condominio guarda `totalUnits` (departamentos, 1 a 80) y `commonAreas` (`[{ id, name, type }]`).
- El dueño de departamento lee `/units?residentUserId=`, `/condominiums/{id}`, `/devices?condominiumId=` y `/readings` de su condominio para Analytics.
- Un medidor mide un área común cuando `measurementPoint` del dispositivo es igual al `type` del área.
