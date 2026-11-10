# IoBuild Propietario — App Móvil Android (Kotlin + Jetpack Compose + BFF)

Aplicación móvil nativa en **Android (Kotlin + Jetpack Compose + Material 3)** diseñada **exclusivamente para el rol Propietario / Residente (`Resident`)** del ecosistema **IoBuild**, conectada al mismo backend de `app-web` aplicando el patrón **Backend for Frontend (BFF)**.

---

## Arquitectura Backend for Frontend (BFF)

En lugar de exponer al cliente móvil todas las rutas administrativas de `app-web` o forzar al teléfono a realizar entre 5 y 7 peticiones HTTP secuenciales por pantalla, se implementó un **módulo BFF exclusivo para el Propietario** bajo el prefijo `/api/v1/mobile/resident`:

- **Backend .NET 9 (`IoBuild.Api`)**: `app-web/backend/src/IoBuild.Api/Shared/Interfaces/REST/MobileResidentBffEndpoints.cs`
- **Servidor Mock Node (`mock-auth.cjs`)**: `app-web/server/mock-auth.cjs`
- **Fallback BFF en Cliente (`ResidentBffRepository.kt`)**: Si el backend aún no ha sido reconstruido en Docker, la app Android compone automáticamente las vistas desde los endpoints REST base (`/api/v1/units`, `/api/v1/condominiums`, `/api/v1/payments`, `/api/v1/settlement_lines`, `/api/v1/profiles`).

### Endpoints BFF del Propietario (`/api/v1/mobile/resident`)

| Endpoint BFF | Vista Móvil | Bounded Contexts Agregados en 1 sola llamada |
|---|---|---|
| `GET /api/v1/mobile/resident/overview` | **1. Mi Consumo** (`MyConsumptionScreen.kt`) | `Units` + `Condominiums` + `Publishing (Payments)` + `Proration (SettlementLines)` + `Monitoring (CommonAreas, Devices, Readings)` + `Analytics (Series diarias y Proyección de fin de mes)` |
| `GET /api/v1/mobile/resident/account-statement` | **2. Estado de Cuenta** (`AccountStatementScreen.kt`) | `Units` (aislado por Ley N.° 27157) + `Condominiums` + `Publishing (Payments)` + `Profiles` + `Proration` |
| `GET /api/v1/mobile/resident/fee-explanation?lineId=` | **3. Mi Cuota Actual** (`FeeExplanationScreen.kt`) | `Proration (FeeExplanation 3 pasos)` + `UtilityBills (Recibos matriz escaneados)` + `Alerts (Sobrecosto en áreas comunes)` + `Publishing` |
| `GET /api/v1/mobile/resident/profile` | **4. Mi Perfil** (`ProfileScreen.kt`) | `IAM (User)` + `Profiles (Profile)` + `Units` + `Condominiums` |
| `POST /api/v1/mobile/resident/ai-explain` | **Asistente IA ("Pregúntale a IoBuild")** | `ConsumptionFactsBuilder` + `Analytics` contextualizado automáticamente al condominio y unidad del propietario |

---

## Seguridad y Aislamiento del Propietario (Ley N.° 27157)

1. **Bloqueo de Rol Administrativo en la App Móvil**: `ResidentBffRepository.loginResident()` valida que el rol devuelto por `/api/v1/authentication/sign-in` sea `resident` / `owner`. Si un administrador intenta ingresar en la app móvil, se bloquea el acceso indicando que la app móvil es exclusiva para propietarios.
2. **Aislamiento de Unidad Inmobiliaria**: Todos los endpoints `/api/v1/mobile/resident/*` resuelven la unidad asignada al JWT/correo del propietario autenticado, impidiendo consultar recibos, estados de cuenta o desgloses de departamentos ajenos.
3. **Flujo de Registro en 2 Pasos para Propietarios Pre-invitados**: Incluye verificación en `/api/v1/authentication/check-invitation` antes de completar el alta con DNI y teléfono.

---

## Estructura del Proyecto Android (`app-mobile`)

```text
app-mobile/
├── app/
│   ├── build.gradle.kts
│   └── src/main/
│       ├── AndroidManifest.xml
│       ├── java/pe/iobuild/resident/
│       │   ├── MainActivity.kt                      # Shell con TopAppBar + NavigationBar (4 tabs)
│       │   ├── data/
│       │   │   ├── local/SessionManager.kt          # Persistencia JWT + URL del servidor BFF
│       │   │   ├── model/ResidentBffModels.kt       # DTOs de los 5 endpoints BFF y modelos base
│       │   │   ├── remote/
│       │   │   │   ├── ApiClientProvider.kt         # OkHttp + Interceptor Bearer JWT + Retrofit
│       │   │   │   └── IoBuildMobileBffApi.kt       # Interfaz Retrofit BFF + endpoints de respaldo
│       │   │   └── repository/
│       │   │       └── ResidentBffRepository.kt     # Orquestador BFF + fallback automático
│       │   └── ui/
│       │       ├── screens/
│       │       │   ├── AuthScreens.kt               # Login + Registro de Propietario en 2 pasos
│       │       │   ├── MyConsumptionScreen.kt       # Tab 1: Mi Consumo + Áreas Comunes + IA
│       │       │   ├── AccountStatementScreen.kt    # Tab 2: Estado de Cuenta + Stripe + Comprobante
│       │       │   ├── FeeExplanationScreen.kt      # Tab 3: ¿Cómo se calculó mi cuota? (3 pasos)
│       │       │   └── ProfileScreen.kt             # Tab 4: Perfil + Notificaciones + Sesiones
│       │       ├── theme/IoBuildTheme.kt            # Paleta oficial IoBuild (Navy, Blue, Sky)
│       │       ├── util/FormatUtils.kt              # Formateadores S/, m³, kWh, % alícuota y fechas
│       │       └── viewmodel/ResidentViewModel.kt   # Estado reactivo Compose para las 4 vistas
```

---

## Cómo Ejecutar y Probar

### 1. Levantar el Backend (`app-web`)

Puedes usar cualquiera de los dos servidores incluidos en `app-web`:

- **Opción A — Backend .NET 9 + MySQL (Docker Compose)**:
  ```bash
  cd ../app-web
  docker compose up -d --build
  ```
  - El API .NET 9 queda expuesto en `http://localhost:8080/api/v1/` (y vía Nginx en `http://localhost:80/api/v1/`).
  - Credenciales de propietario pre-sembradas en `.NET 9`:
    - **Correo**: `cflores@gmail.com`
    - **Contraseña**: `Resident123!`
    - **Unidad asignada**: `Dpto. 402` (3.8500% de alícuota legal — Condominio Las Palmas)

- **Opción B — Servidor Mock Local (`npm run server`)**:
  ```bash
  cd ../app-web
  npm run server
  ```
  - Expuesto en `http://localhost:3000/api/v1/`.
  - Credenciales de propietario en `db.json`:
    - **Correo**: `carla.f@email.com`
    - **Contraseña**: `Demo1234!`

### 2. Abrir `app-mobile` en Android Studio

1. Abre **Android Studio** y selecciona **Open** -> elige la carpeta `app-web/app-mobile` (la versionada en git).
2. Espera a que sincronice Gradle (usa el wrapper incluido) y ejecuta la configuración `app` en un emulador o celular.
3. **Servidor**: la app usa producción (`https://iobuild.me/api/v1/`) por defecto, tanto en debug como en release.
   - Para compilar contra el backend local desde la terminal:
     `./gradlew assembleDebug -PiobuildApiUrl=http://10.0.2.2:8080/api/v1/` (en el emulador, `10.0.2.2` es tu PC).
   - En una compilación **debug** también puedes mantener presionado el logo de la pantalla de login para elegir otro servidor de prueba. Las versiones release ignoran ese ajuste.
4. Inicia sesión con una cuenta de propietario. En el backend local sembrado: `cflores@gmail.com` / `Resident123!` (Dpto. 402).
