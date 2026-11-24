# Proposal

## Why

Una prueba manual no siempre ocurre dentro de la aplicación probada: el tester sale de ella para
preparar un recurso que la app consume (por ejemplo, crear un recurso con Postman) y vuelve para
comprobar que ya aparece. Hoy TEC sigue **un único proceso**, así que lo que el tester hace fuera
de la app queda sin evidencia, o obliga a parar la sesión y volver a iniciarla, lo que rompe el
orden cronológico que es justamente el valor de la prueba.

## What Changes

- La selección de objetivo pasa de una aplicación a un **conjunto de 1..N aplicaciones**, elegido
  antes de pulsar Iniciar (multi-selección en la lista de procesos; el botón de lanzar aplicación
  añade a ese conjunto lo que lance).
- Una sesión agrupa las evidencias de todas las aplicaciones seguidas: **una sola numeración, una
  sola carpeta y un solo reporte**, en orden cronológico.
- Cada evidencia identifica **a qué aplicación pertenece**, además de la ventana, en la leyenda de
  la captura y en el reporte.
- La carpeta de evidencias y el título del reporte pasan a reflejar el conjunto seguido
  (`<fecha>_<app1>+<app2>`). Con **una sola aplicación elegida, el nombre no cambia**.
- Si un proceso seguido termina a mitad de sesión, TEC lo **avisa** (log, aviso en la interfaz y
  nota en el reporte) en lugar de dejar de capturar en silencio.
- La región de captura sigue siendo por interacción (Ventana / Monitor / Todas las pantallas). No
  se añade ninguna captura conjunta de dos aplicaciones.
- No hay modo "solo la aplicación en foco": la interacción pertenece a la ventana que la recibe,
  y eso ya separa una aplicación de otra.

## Capabilities

### New Capabilities

- `target-selection`: cómo se eligen las aplicaciones que se van a seguir antes de iniciar la
  sesión, qué pasa si el conjunto está vacío, cómo se añade una aplicación lanzada desde TEC y
  qué ocurre cuando un proceso seguido deja de existir durante la sesión.
- `evidence-session`: qué constituye una sesión y qué produce: carpeta de salida, numeración
  continua de evidencias, reporte único y cómo se identifica la aplicación de cada evidencia.

### Modified Capabilities

Ninguna: `openspec/specs/` está vacío, no hay specs que modificar.

## Impact

- **Tec.App**: `MainViewModel` (`SelectedProcess` → conjunto seleccionado, `MainViewModel.cs:34,88`),
  `SessionSettings.Target` → conjunto (`EvidenceSessionFactory.cs:22`), los sitios que hoy pasan
  `[settings.Target.ProcessId]` (`EvidenceSessionFactory.cs:73,116`).
- **Tec.Core**: `EvidenceSession.Create` (nombre de carpeta y descripción del objetivo,
  `Evidence.cs:26-39`), `CaptionFormatter` (aplicación en la leyenda, `CaptionFormatter.cs:38-41`),
  `HtmlReportSink` (título y resumen, `HtmlReportSink.cs:91`), `ProcessInteractionFilter`
  (aviso cuando un PID seguido desaparece, `Basics.cs:13`).
- **Tec.Windows**: sin cambios funcionales; `ProcessInteractionFilter` y
  `TargetForegroundWindowProvider` ya aceptan conjuntos (`Basics.cs:13`, `ContentChangeSource.cs:17`).
- **Localización**: claves nuevas en `Captions*.resx` (es/en) y `Strings*.resx` (es/en).
- **Pruebas**: `Tec.Core.Tests` (nomenclatura de la sesión con 1 y con N apps, leyendas, aviso de
  proceso muerto). `ContentChangeSource` mantiene una comparación por tick, así que no hay coste
  de CPU adicional al seguir varias aplicaciones.
