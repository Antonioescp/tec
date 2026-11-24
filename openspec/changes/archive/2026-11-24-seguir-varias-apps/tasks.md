# Tasks

## 1. Modelo de sesión con varias aplicaciones (Tec.Core)

- [x] 1.1 Añadir a `Interaction` el nombre de la aplicación (`ProcessName`) y verificar con una
  prueba en `Tec.Core.Tests` que una evidencia sin nombre sigue siendo válida
- [x] 1.2 Construir en `EvidenceSession.Create` el nombre de carpeta con el conjunto
  (`App1+App2`) conservando el nombre actual con una sola aplicación, y verificar con pruebas el
  caso de 1 app, el de 2 apps y el corte a 40 caracteres con `+N`
- [x] 1.3 Incluir la aplicación en la leyenda de `CaptionFormatter` con sus claves en
  `Captions.resx` y `Captions.en.resx`, y verificar con `CaptionFormatterTests` que la leyenda
  muestra aplicación y ventana, y que sin nombre de aplicación la leyenda no rompe
- [x] 1.4 Escribir el nombre de aplicación en `evidencias.jsonl` (`FileSinks.cs`) y verificar con
  una prueba que la línea conserva `ProcessId` y añade `ProcessName`
- [x] 1.5 Poner en el reporte el conjunto de aplicaciones en el título y una nota por cada
  aplicación que dejó de existir, y verificar con una prueba sobre la salida de `HtmlReportSink`

## 2. Datos de proceso en Windows (Tec.Windows)

- [x] 2.1 Rellenar `ProcessName` en `WindowInfoEnricher` desde el PID de la ventana y verificar en
  una ejecución con `samples/Tec.SampleApp` que `evidencias.jsonl` trae el nombre del proceso
- [x] 2.2 Observar los procesos del conjunto y notificar cuándo uno termina, y verificar cerrando
  el `SampleApp` a mitad de sesión: aparece en el log y el reporte lo nota, y la sesión sigue

## 3. Selección e integración (Tec.App)

- [x] 3.1 Cambiar `SessionSettings.Target` a `Targets` y usar el conjunto en el filtro de
  interacciones y en las ventanas observadas (`EvidenceSessionFactory.cs:73,116`), y verificar que
  `dotnet build` pasa y que una sesión con dos procesos captura los dos
- [x] 3.2 Multi-selección en la lista de aplicaciones de la interfaz, con sus textos en
  `Strings.resx` y `Strings.en.resx`, y verificar seleccionando `SampleApp` y otra app: Iniciar se
  habilita con dos y se deshabilita con cero
- [x] 3.3 Añadir a la selección la aplicación lanzada desde TEC y verificar lanzando el
  `SampleApp` desde la interfaz y comprobando que entra en el conjunto de la sesión

## 4. Documentación

- [x] 4.1 Actualizar el README (selección de varias aplicaciones, nombre de la carpeta resultante,
  qué se sigue y qué no) y verificar que lo descrito coincide con el comportamiento observado en 5.1

## 5. Integración

- [x] 5.1 Ejecutar `dotnet test` y una prueba manual del flujo completo (buscar en la app, crear
  el recurso en la segunda aplicación, refrescar la app) verificando una sola carpeta, numeración
  continua en orden cronológico y aplicación identificada en cada evidencia
- [x] 5.2 Verificar la regresión de una sola aplicación: nombre de carpeta, reporte y leyendas
  como antes del cambio, y `dotnet test` en verde

## Workflow follow-up

- Archivar el change (`openspec archive seguir-varias-apps`) cuando la revisión del repo lo permita,
  lo que creará `openspec/specs/target-selection/spec.md` y `openspec/specs/evidence-session/spec.md`.
