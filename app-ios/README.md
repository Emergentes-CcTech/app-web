# IoBuild para iOS (propietarios)

App nativa en SwiftUI para el propietario de departamento: el mismo alcance que la app Android (`../app-mobile`) y el mismo backend, el BFF móvil `/api/v1/mobile/resident/*`.

| Pestaña | Qué muestra |
|---|---|
| **Consumo** | Liquidación vigente, consumo de agua y energía de las áreas comunes (con gráfico diario), detalle por área, asistente IA y últimas liquidaciones |
| **Pagos** | Cuota pendiente con pago por Stripe, historial y comprobantes para compartir |
| **Mi cuota** | Explicación en tres pasos, comparación con el mes anterior, alertas con costo, recibos y glosario |
| **Perfil** | Datos del propietario, avisos, idioma y cierre de sesión |

También incluye el inicio de sesión y el registro en dos pasos para propietarios invitados por la administración.

## Requisitos

- Una **Mac con Xcode 16 o superior**: Apple solo permite compilar e instalar apps de iOS desde Xcode.
- iPhone con **iOS 17 o superior**.
- Una cuenta de Apple. La gratuita sirve para instalar en tu propio iPhone; esa instalación vence a los 7 días y se renueva volviendo a ejecutar desde Xcode.

## Instalar en tu iPhone

1. Abre `IoBuildResident.xcodeproj` en Xcode.
2. En el proyecto, ve al target **IoBuildResident → Signing & Capabilities** y elige tu equipo en **Team**. Si Xcode dice que el *bundle identifier* ya existe, cámbialo, por ejemplo a `pe.iobuild.resident.tunombre`.
3. Conecta el iPhone por cable, desbloquéalo y acepta **Confiar en esta computadora**. En el iPhone activa **Ajustes → Privacidad y seguridad → Modo de desarrollador** (se reinicia).
4. Elige tu iPhone arriba en Xcode y pulsa **Run** (⌘R).
5. La primera vez, en el iPhone ve a **Ajustes → General → VPN y gestión de dispositivos** y confía en tu certificado de desarrollador.

La app usa producción (`https://iobuild.me/api/v1/`). En las compilaciones **Debug**, el texto pequeño al pie del inicio de sesión permite apuntarla a otro servidor, por ejemplo tu PC en la red local: `http://192.168.1.20:8080/api/v1/`. Las compilaciones **Release** usan siempre producción.

## Diferencias con la versión Android

- **El token se guarda en el Llavero de iOS**, no en preferencias en texto plano, y nunca se escribe en los registros.
- **El pago con Stripe se abre dentro de la app.** Cuando Stripe vuelve a la página de retorno de IoBuild, la hoja se cierra sola y la app confirma el pago en el backend (`/payments/{id}/checkout-session/confirm`). También lo confirma si cierras la hoja a mano o vuelves a la app.
- **El idioma (español o inglés) se elige en Ajustes › IoBuild › Idioma**, como manda iOS; el perfil tiene un acceso directo.
- **No incluye la carga de respaldo** que la versión Android hacía contra los endpoints antiguos: el iPhone usa directamente el BFF.

## Estructura

```
IoBuildResident/
  IoBuildResidentApp.swift   Punto de entrada
  Core/                      Configuración, Llavero, cliente HTTP, modelos, formatos y AppModel (estado y llamadas al backend)
  UI/                        Pantallas (Auth, Consumo, Pagos, Mi cuota, Perfil), hoja de pago de Stripe y componentes
  Localizable.xcstrings      Textos en español e inglés (mismas claves que Android)
  Assets.xcassets            Ícono y color de acento
```

El proyecto usa carpetas sincronizadas de Xcode 16: cualquier archivo que agregues dentro de `IoBuildResident/` entra solo al proyecto.
