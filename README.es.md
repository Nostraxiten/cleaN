<div align="center">

<img src="src/Assets/logo.svg" alt="cleaN" width="88" height="88">

# cleaN

**Un limpiador de sistema de código abierto para Windows.**

[Read in English](README.md) · [Licencia MIT](LICENSE) · Windows 10/11 · .NET 8 + WPF

</div>

---

## Qué hace cleaN

cleaN libera espacio en disco y ordena Windows sin decidir por ti. Primero analiza, después
te enseña exactamente lo que ha encontrado, y sólo borra cuando le das el visto bueno. El
modo vista previa viene activado de fábrica, así que una instalación recién hecha no puede
borrar nada hasta que tú lo desactives a propósito.

Lo que lo diferencia de los limpiadores habituales es el **detector de aplicaciones sin
uso**: en vez de traer una lista de programas conocidos mantenida a mano, cleaN lee el
registro de desinstalación de Windows y cruza cada entrada con el historial de ejecuciones
que Windows ya guarda (Prefetch y los registros UserAssist del Explorador). El resultado
funciona con lo que sea que tengas instalado, incluido software que no conoce nadie.

### Funcionalidades

| Sección | Qué limpia |
| --- | --- |
| **Archivos temporales** | `%TEMP%`, `C:\Windows\Temp`, la caché de descargas de Windows Update, las cachés de miniaturas e iconos, los informes de error de Windows (WER) y los logs antiguos de `C:\Windows\Logs` |
| **Caché de navegador** | Caché, cookies e historial de todos los perfiles de cada navegador instalado: Chrome, Edge, Brave, Vivaldi, Opera, Chromium, Yandex, Firefox, Waterfox y LibreWolf. Cookies e historial son opcionales y nunca vienen marcados |
| **Carpetas vacías** | Escaneo recursivo de carpetas sin ningún archivo a ninguna profundidad, presentadas como una lista que confirmas antes de borrar nada |
| **Aplicaciones sin uso** | Todo lo instalado, ordenado por el tiempo que lleva sin usarse. cleaN nunca desinstala nada por su cuenta: le pasa a Windows el desinstalador de la propia aplicación cuando se lo pides |
| **Papelera de reciclaje** | Se vacía a través de la API del shell de Windows, en todas las unidades |
| **Informe y logs** | Un log en texto plano de cada limpieza: cada archivo eliminado y el espacio total recuperado |

### Cómo te protege cleaN

- **Vista previa por defecto.** Cada ejecución informa de lo que *borraría* y no toca nada
  hasta que tú desactivas el modo.
- **Lista blanca, no lista negra.** Cada módulo declara las carpetas en las que tiene
  permiso para trabajar. Una ruta sólo se borra si está *estrictamente por debajo* de una de
  ellas, queda fuera de todas las zonas protegidas y está al menos dos niveles por debajo de
  la raíz de la unidad. Si falla una sola comprobación, el elemento se omite y se registra;
  nunca se borra.
- **Las zonas protegidas son intocables.** `System32`, `WinSxS`, `Program Files`, las
  carpetas del shell, las raíces de unidad, `$Recycle.Bin`, `System Volume Information` y tus
  carpetas de OneDrive están vetadas. Dentro de `C:\Windows` la regla se invierte: sólo se
  pueden limpiar `Temp`, `Logs`, `SoftwareDistribution\Download` y `Downloaded Program
  Files`, y nada más.
- **Nunca se siguen uniones ni enlaces simbólicos**, así que un punto de reanálisis no puede
  llevar un escaneo a donde no debe.
- **Los temporales de menos de 24 horas se dejan en paz**, porque los instaladores en marcha
  guardan ahí datos en uso.
- **Cada ejecución queda registrada**, para que siempre puedas auditar lo que pasó.

## Capturas de pantalla

Se añadirán aquí cuando la interfaz esté cerrada. Mientras tanto: tema blanco y minimalista
por defecto, tema oscuro a un clic en la esquina superior derecha, y un menú lateral con las
seis secciones de la tabla de arriba.

## Instalación rápida

1. Descarga [`release/cleaN.exe`](release/cleaN.exe).
2. Ejecútalo. Windows pedirá permisos de administrador, que cleaN necesita para leer
   `C:\Windows\Prefetch` y para limpiar las ubicaciones de todo el equipo. Sin ellos también
   funciona, pero con resultados incompletos.

No hay nada más que instalar: el runtime de .NET 8 va dentro del ejecutable.

## Cómo compilar desde el código fuente

Necesitas el [SDK de .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0) en Windows.

```powershell
cd src
./build.ps1
```

El script publica una compilación self-contained de un solo archivo y la deja en
`release/cleaN.exe`. Opciones:

```powershell
./build.ps1 -Runtime win-arm64      # compilar para equipos ARM64
./build.ps1 -Output ../compilado    # dejar el binario en otro sitio
```

Por debajo es un `dotnet publish` normal, así que `src/cleaN.sln` se abre directamente en
Visual Studio o Rider si prefieres trabajar desde ahí.

El icono de la aplicación se genera a partir de la misma geometría que el logo:

```powershell
python3 src/Assets/generate-icon.py
```

## Estructura del repositorio

```
cleaN/
├── src/                        Código fuente completo de la aplicación .NET 8 / WPF
│   ├── cleaN.sln
│   ├── cleaN.csproj
│   ├── build.ps1               Script de compilación (ver arriba)
│   ├── app.manifest            Pide administrador, DPI por monitor y rutas largas
│   ├── Assets/                 Logo, icono generado y los temas claro y oscuro
│   ├── Core/                   Reglas de seguridad, motor de borrado, logs y ajustes
│   ├── Modules/                Un módulo por tipo de limpieza
│   ├── Apps/                   Aplicaciones instaladas y su historial de ejecución
│   ├── Interop/                Las pocas llamadas a la API de Windows que hacen falta
│   ├── ViewModels/             Lógica de la aplicación, sin ningún tipo de la interfaz
│   └── Views/                  Ventanas, controles de usuario y servicios de WPF
├── release/                    El binario compilado, listo para descargar y usar
│   └── cleaN.exe
├── docs/screenshots/           Capturas para este README
├── README.md                   Versión en inglés
├── README.es.md                Este archivo
└── LICENSE                     MIT
```

Los archivos interesantes, si quieres leer el código:

- `src/Core/SafetyGuard.cs` — las reglas que deciden si una ruta se puede borrar.
- `src/Core/FileSweeper.cs` — el único sitio de cleaN donde se borran archivos de verdad.
- `src/Apps/UsageAnalyzer.cs` — cómo se responde a "¿cuándo se usó esto por última vez?".

## Aviso legal

cleaN se ofrece **tal cual, sin garantía de ningún tipo**. Borra archivos, y borrar archivos
tiene riesgo. Eres responsable de lo que limpies en tu propio sistema. Lee la lista de la
vista previa antes de desactivar el modo vista previa.

cleaN **no contiene código de CCleaner ni de ningún otro limpiador propietario**, y no está
afiliado, avalado ni derivado de ninguno de ellos. CCleaner es de código cerrado, así que no
hay nada suyo que copiar ni siquiera en teoría. Todo esto está escrito a partir de la
documentación pública de Microsoft sobre las ubicaciones de archivos, cachés y registro de
Windows, y de lo que limpiadores de código abierto como BleachBit llevan años documentando
abiertamente. Los nombres de producto pertenecen a sus dueños y se usan sólo para indicar
qué software puede limpiar cleaN.

## Licencia

[MIT](LICENSE). Úsalo, modifícalo, distribúyelo — sólo mantén el aviso de copyright.
