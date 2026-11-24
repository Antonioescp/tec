# Proposal

## Why

Hoy la carpeta y el reporte de una sesión se llaman solo con la fecha y las aplicaciones seguidas
(`20261124_165626_Notepad+Tec.SampleApp`). Cuando el tester hace varias pruebas sobre el mismo
conjunto de aplicaciones —el mismo flujo, varias versiones, o varios escenarios del mismo
requerimiento— todas las carpetas se ven igual y hay que abrir el reporte para saber qué prueba es
cada una. El tester ya sabe, antes de iniciar, qué está probando; TEC debería poder recoger ese
nombre.

## What Changes

- Un campo de texto **Nombre de la sesión (opcional)** en la pantalla principal, junto a la carpeta
  de salida. Vacío = comportamiento actual, sin cambios.
- Mientras está vacío, el campo muestra en gris el nombre que se usaría (`20261124_171500_Farmax+Postman`),
  actualizado al cambiar las aplicaciones marcadas, para que el nombre automático no sea una sorpresa.
- Con nombre escrito, la carpeta pasa a `<fecha>_<nombre>` (se conserva el prefijo de fecha para
  mantener el orden cronológico en el explorador) y el título del reporte usa ese nombre.
- El nombre se sanea con las mismas reglas que hoy: caracteres no válidos y espacios pasan a `_`,
  con el mismo tope de **40 caracteres**.
- Si la carpeta ya existe, se crea `<nombre>_2`, `<nombre>_3`, … sin avisar y sin bloquear el
  inicio: TEC no puede interrumpir una prueba a mitad.
- Las aplicaciones seguidas dejan de ser el nombre de la sesión pero siguen visibles: el reporte
  añade una línea con las aplicaciones de la sesión (hoy solo se ven en el título).
- Sin cambios en la numeración, el contenido del reporte, las capturas ni el JSONL más allá del
  título y la línea de aplicaciones.

## Capabilities

### New Capabilities

- (ninguna)

### Modified Capabilities

- `evidence-session`: el nombre de la carpeta y del reporte pasa a ser el nombre que dé el tester
  cuando lo dé (con saneado, tope y sufijo anti-colisión), y el reporte lista siempre las
  aplicaciones seguidas.

## Impact

- **Tec.Core**: `EvidenceSession` recibe el nombre opcional y decide el nombre final de la carpeta
  (sufijo `_2` si ya existe). Sigue siendo el único sitio que construye la ruta, y expone el nombre
  previsto que usa la pista del campo.
- **Tec.App**: `SessionSettings` con el nombre opcional; `MainViewModel` con la propiedad enlazada
  y el `TextBox` en `MainWindow.xaml`; claves nuevas en `Strings.resx`/`Strings.en.resx`;
  `EvidenceSessionFactory` propaga el nombre.
- **Tec.Core/Reporting**: `HtmlReportSink` usa el nombre en el `<h1>` y añade la línea de
  aplicaciones (claves nuevas en `Report.resx`/`Report.en.resx`).
- **Pruebas**: `EvidenceSessionTests` (nombre propio, saneado, tope, colisión) y
  `HtmlReportSinkTests` (título y línea de aplicaciones).
- **Documentación**: sección de carpetas del README.
- Sin cambios en el formato de `evidencias.jsonl` ni en las imágenes: lo que ya se captura no se
  toca, solo cómo se llama la carpeta y qué se lee en la cabecera del reporte.
