# TEC — Test Evidence Collector

TEC recolecta evidencia durante una prueba manual. Se elige la aplicación a probar,
se presiona **Iniciar** y TEC toma una captura por cada interacción:

| Interacción | Cómo se detecta | Qué se resalta |
|---|---|---|
| Click (izquierdo, derecho, central, doble) | Hook de mouse de bajo nivel (`WH_MOUSE_LL`), al presionar el botón | Control bajo el puntero (UI Automation) + marcador en el punto exacto |
| Comando de teclado (Ctrl/Alt/Win + tecla, F1–F24, Enter, Esc, Supr, doble pulsación de Shift/Ctrl/Alt) | Hook de teclado de bajo nivel (`WH_KEYBOARD_LL`) | Control con el foco en ese instante |
| Texto ingresado | Ráfaga de escritura (termina tras 1.2 s sin teclear, con Tab o con un comando) | Campo editado; el valor se lee con UI Automation (las contraseñas se enmascaran) |
| Ventana mostrada (espere, diálogos, resultados, MessageBox) | WinEvent `EVENT_OBJECT_SHOW` | La ventana nueva |
| Cambio en pantalla (overlay de "cargando", panel o resultados que aparecen en la misma ventana) | Comparación periódica (4 por segundo) del contenido de la ventana activa de la app | La zona que cambió |

Cada imagen lleva una cabecera con el número, el tipo de interacción, el elemento, la
hora, la ventana y la posición, y un **recuadro rojo**; si el fondo alrededor del recuadro
es rojo (o parecido) se usa otro color que contraste (azul, magenta, verde…).

Por cada sesión se crea una carpeta `Documentos\TEC\Evidencias\<fecha>_<app>\` con:

- `001_click.png`, `002_ventana.png`, … — capturas anotadas.
- `reporte.html` — reporte con todas las evidencias en orden.
- `evidencias.jsonl` — metadatos de cada evidencia.

Solo se capturan las interacciones con el proceso elegido; las interacciones con TEC se ignoran.

## Uso

```powershell
dotnet build
dotnet run --project src/Tec.App              # TEC
dotnet run --project samples/Tec.SampleApp    # app WPF de ejemplo para probar TEC
dotnet test
```

La app de ejemplo tiene un botón **Procesar** que muestra una ventana "espere" sin
bordes y luego una ventana de resultado, un atajo **Ctrl+S**, un campo de contraseña
y una zona con fondo rojo.

Requisitos: Windows 10/11 y .NET 10 SDK.

## Logs

TEC escribe un log por día en `%LOCALAPPDATA%\TEC\logs\tec-AAAA-MM-DD.log` (se conservan
14 días; el botón **Ver logs** abre la carpeta). Incluye el arranque (versión, Windows,
si corre como administrador), cada acción del usuario, la configuración de cada sesión,
cada interacción detectada y guardada, y todos los errores con su stack trace completo,
también los que ocurren dentro de los hooks y los que no se atrapan.

## Arquitectura

```
Tec.Core      (net10.0, sin dependencias de plataforma)
  Model         Interaction, InteractionKind, ScreenRect, RasterImage, Evidence, ...
  Abstractions  IInteractionSource, IInteractionFilter, IInteractionEnricher,
                ICaptureRegionProvider, IScreenCapturer, IHighlightColorSelector,
                IInteractionCaptionFormatter, IFrameAnnotator, IImageEncoder, IEvidenceSink
  Services      EvidenceCollector (orquestador), InteractionSnapshotter, EvidenceComposer,
                ContrastingHighlightColorSelector, SpanishCaptionFormatter, VisualChangeDetector, filtros
  Reporting     ImageFileEvidenceSink, HtmlReportSink, JsonLinesEvidenceLogSink

Tec.Windows   (net10.0-windows: Win32, UI Automation, GDI+)
  Sources       MouseClickSource, KeyboardSource, WindowShownSource, ContentChangeSource
  Uia           UiaElementEnricher, FocusTracker
  Capture       Window/Monitor/VirtualScreen RegionProvider, WindowInfoEnricher
  Imaging       GdiScreenCapturer, PrintWindowContentCapturer, GdiFrameAnnotator, PngImageEncoder

Tec.App       (WPF, MVVM) interfaz + composition root (EvidenceSessionFactory)
```

Flujo de una interacción:

```
Fuente ──► Filtro ──► [espera CaptureDelay] ──► Región ──► Captura ──► Enriquecedores
                                                   (InteractionSnapshotter)
     ──► Color de resaltado ──► Leyenda ──► Anotación ──► Destinos (PNG, HTML, JSONL)
                          (EvidenceComposer)
```

La captura se toma **antes** de consultar UI Automation, para que la imagen muestre el
estado del instante de la interacción. Las capturas corren en paralelo a la detección y la
composición/escritura en un único consumidor, así que los hooks nunca se bloquean.

### Principios SOLID

- **Responsabilidad única**: detectar, capturar, enriquecer, elegir color, redactar,
  anotar y guardar son clases distintas.
- **Abierto/cerrado**: `InteractionKind` es un tipo abierto; nuevas fuentes,
  enriquecedores o destinos se registran en `EvidenceSessionFactory` sin tocar el núcleo.
- **Sustitución de Liskov**: cualquier `IInteractionSource`, `IEvidenceSink`, etc. es
  intercambiable (las pruebas usan dobles en memoria).
- **Segregación de interfaces**: interfaces pequeñas, de un solo método en su mayoría.
- **Inversión de dependencias**: `Tec.Core` solo conoce abstracciones; las
  implementaciones concretas se conectan en el composition root.

### Cómo extender

| Necesidad | Qué implementar |
|---|---|
| Otra tecnología (WinForms, web, Java) | `IInteractionSource` y/o `IInteractionEnricher` |
| Ajustar la sensibilidad de los cambios en pantalla | `VisualChangeOptions` (umbral, tiempo de estabilización, animaciones) |
| Exportar a Word/PDF o subir a Jira/Azure DevOps | `IEvidenceSink` |
| Otro idioma o formato de leyenda | `IInteractionCaptionFormatter` |
| Otro estilo de anotación | `IFrameAnnotator` o `AnnotationStyle` |
| Otra regla de qué capturar | `IInteractionFilter` |

### Detección de cambios en pantalla

`ContentChangeSource` captura la ventana activa de la app con `PrintWindow`. Así obtiene
solo el contenido de la ventana, sin el cursor, los tooltips ni los diálogos que tenga
encima. `VisualChangeDetector` (en `Tec.Core`) la divide en bloques de 24 px y la compara
con la captura anterior:

- Los cambios pequeños (cursor de texto, escritura, hover) se absorben sin generar evidencia.
- Un cambio grande (por defecto, al menos el 2 % de la ventana) se reporta **una sola vez**,
  cuando la pantalla deja de cambiar durante 350 ms, o a los 2 s como máximo.
- Los bloques que cambian continuamente (spinners, barras de progreso) se tratan como
  animación y no generan evidencias repetidas.
- Se guarda un detector por ventana: al cerrar un diálogo se detecta lo que cambió en la
  ventana principal mientras tanto.

## Limitaciones conocidas

- TEC debe ejecutarse con el mismo nivel de privilegios que la app probada: si la app
  corre como administrador, TEC también (restricción de UIPI de Windows).
- Las capturas usan GDI: no funcionan con la sesión bloqueada ni capturan contenido
  protegido (DRM).
- Si el hilo de UI de la app está bloqueado, UI Automation no responde; la evidencia
  se guarda igual, con una nota y resaltando la ventana completa.
- Los cambios en pantalla solo se observan en la ventana activa de la app. El
  desplazamiento de listas grandes también cuenta como cambio.
