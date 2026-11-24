# Tasks

## 1. Nombre de sesión en el modelo (Tec.Core)

- [x] 1.1 Separar en `EvidenceSession` el nombre de la sesión (`Name`) del conjunto de aplicaciones
  (`TargetDescription`) y aceptar el nombre opcional en `Create`, y verificar con pruebas que sin
  nombre `Name` es el conjunto y la carpeta es la de hoy, y que con nombre la carpeta es
  `<fecha>_<nombre>`
- [x] 1.2 Saneado del nombre escrito (`Design` D3/D4): inválidos y espacios a `_`, puntos y espacios
  finales fuera, recorte a 40 y, si no queda nada, nombre generado; y verificar con pruebas los
  cuatro casos del delta (espacios, demasiado largo, termina en punto, solo caracteres inválidos)
- [x] 1.3 Buscar la siguiente numeración libre (`_2`, `_3`, …) cuando la carpeta ya existe y
  verificarlo sobre una carpeta de `Path.GetTempPath()`: la segunda sesión con el mismo nombre cae en
  `_2` y la tercera en `_3`, sin excepción
- [x] 1.4 Cabecera del reporte: `<h1>` con `Name` y línea nueva `Aplicaciones: <conjunto>` con sus
  claves en `Report.resx` y `Report.en.resx`, y verificar con `HtmlReportSinkTests` que una sesión con
  nombre muestra nombre y aplicaciones, y que una sin nombre conserva el título actual
- [x] 1.5 Exponer `EvidenceSession.PreviewSessionName(targetNames, now)` (`Design` D8) con el mismo
  resultado que el `Id` de `Create` sin nombre propio, y verificar con pruebas que coinciden sobre una
  carpeta temporal y que el saneado y el tope de 40 se aplican igual

## 2. Campo de nombre e integración (Tec.App)

- [x] 2.1 Añadir `SessionSettings.SessionName` y propagarlo desde `MainViewModel` al factory,
  reservando la carpeta con `Directory.CreateDirectory` al crear la sesión (`Design` D6), y verificar
  con `dotnet build` y una sesión iniciada que la carpeta con el nombre elegido existe ya al empezar
  a grabar
- [x] 2.2 Campo de texto en el grupo de la carpeta de salida, con su etiqueta, su `ToolTip` y
  `MaxLength="40"`, con las claves `SessionNameGroup` y `SessionNamePreviewTip` en `Strings.resx` y
  `Strings.en.resx`, y verificar en la ventana que vacío inicia con el nombre generado y escrito inicia
  con `<fecha>_<nombre>`
- [x] 2.3 Superposición gris dentro del campo con el nombre previsto (`Design` D9), recalculada al
  cambiar las aplicaciones marcadas y al terminar la sesión, y verificar en la ventana que al marcar
  otra app cambia la pista, que al teclear desaparece y que con cero aplicaciones marcadas *Iniciar*
  sigue deshabilitado
- [x] 2.4 Nombre repetido y verificar iniciando dos sesiones seguidas con el mismo nombre desde la
  interfaz: se crean dos carpetas distintas sin ningún aviso y el estado y el log muestran la ruta real
  de cada sesión (el sufijo `_2`, probado en 1.3, queda de red para dos sesiones en el mismo segundo)

## 3. Documentación

- [x] 3.1 Actualizar el README con el campo de nombre y su pista con el nombre previsto, el formato
  `<fecha>_<nombre>`, el sufijo `_2` y la línea de aplicaciones del reporte, y verificar que lo descrito
  coincide con lo observado en 4.2

## 4. Integración

- [x] 4.1 Ejecutar `dotnet format Tec.slnx --verify-no-changes`, `dotnet build Tec.slnx -c Release` y
  `dotnet test Tec.slnx -c Release` y verificar que los tres pasan sin advertencias
- [x] 4.2 Prueba manual del flujo completo con dos aplicaciones y nombre propio: una sola carpeta con
  el nombre escrito, numeración continua en orden cronológico, título del reporte con el nombre y la
  cabecera listando las dos aplicaciones
- [x] 4.3 Verificar la regresión sin nombre propio: carpeta, título del reporte y leyendas idénticas a
  las de antes del cambio

## Workflow follow-up

- Archivar el change (`openspec archive custom-session-name`) cuando la revisión del repo lo permita,
  lo que aplicará el delta sobre `openspec/specs/evidence-session/spec.md`.
