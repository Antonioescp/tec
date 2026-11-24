# Design

## Context

`EvidenceSession` (Tec.Core) es el único sitio que construye el identificador y la ruta de la sesión:
`id = $"{now:yyyyMMdd_HHmmss}_{Sanitize(targetDescription)}"` y `OutputDirectory = root/id`.
`Sanitize` ya sustituye inválidos y espacios por `_`, recorta a `MaxDescriptionLength` (40) y
recae en `sesion` cuando no queda nada. La descripción de las aplicaciones (`Farmax+Postman`, de
`DescribeTargets`) se usa para dos cosas a la vez: el nombre de la carpeta **y** el `<h1>` del
reporte (`HtmlReportSink`, `Title — TargetDescription`). Las sinks (`FileSinks`, `HtmlReportSink`)
crean la carpeta con `Directory.CreateDirectory` al arrancar la sesión; el factory todavía no toca
el disco.

Ver `proposal.md` para el motivo y `specs/evidence-session/spec.md` para los requisitos.

## Goals / Non-Goals

**Goals:**

- Un solo punto que decida el nombre final de la carpeta, de modo que `Id` y `OutputDirectory`
  sigan siendo coherentes para todas las sinks.
- Distinguir los dos conceptos que hoy están mezclados en `TargetDescription`: **cómo se llama la
  sesión** y **qué aplicaciones sigue**.
- Que una sesión sin nombre propio sea idéntica a la de hoy (carpeta, título del reporte, numeración).

**Non-Goals:**

- Renombrar o reorganizar carpetas de sesiones ya guardadas.
- Recordar el nombre entre ejecuciones de TEC (a diferencia del idioma, que sí se persiste).
- Validar el nombre en la interfaz con reglas propias: la interfaz no decide, solo ayuda.

## Decisions

**D1 — `SessionName` es un campo opcional del `SessionSettings`, no un reemplazo de la
descripción.** `SessionSettings.SessionName` (`string?`) llega al factory; si viene vacío, se usa el
nombre generado. Alternativa considerada: reutilizar `TargetDescription` y dejar de derivarla de las
aplicaciones: perdería del reporte la información de qué aplicaciones se siguieron, que ahora debe
verse siempre.

**D2 — `EvidenceSession` guarda los dos conceptos por separado.** `TargetDescription` sigue siendo el
conjunto de aplicaciones (`Farmax+Postman`); se añade `DisplayName`, el nombre tal como lo escribió
el tester, y `Name` (`DisplayName ?? TargetDescription`) es lo que se muestra. El `<h1>` del reporte
usa `Name` y se añade una línea `Aplicaciones: <TargetDescription>` con clave `Applications` en
`Report.resx`/`Report.en.resx`; la línea solo aparece cuando el nombre es propio, porque sin nombre ya
estan las aplicaciones en el título. Sin nombre propio, `Name == TargetDescription`: el título no cambia.
El nombre se sanea **solo para la carpeta** (`Login con Postman` → carpeta `Login_con_Postman`, título
`Login con Postman`), que es lo que pide el delta.

**D3 — El saneado del nombre escrito reutiliza `Sanitize`, más los puntos y espacios finales.**
Windows recorta esos caracteres al crear la carpeta, y eso rompería la igualdad entre `Id` y el
nombre real de la carpeta. Si tras sanear no queda nada, se usa el nombre generado por las
aplicaciones (no `sesion`), que es más informativo. Alternativa: rechazar el nombre con un mensaje;
se descarta porque un fallo al iniciar interrumpe una prueba.

**D4 — Un solo tope de longitud: `MaxDescriptionLength` (40).** El nombre escrito comparte constante
para que la carpeta tenga el mismo orden de tamaño con o sin nombre propio. El `TextBox` pone
`MaxLength="40"` como aviso inmediato, pero el recorte definitivo lo hace Core: la regla es la misma
si el nombre llega por otra vía.

**D5 — La colisión se resuelve en `EvidenceSession.Create` buscando la siguiente numeración libre.**
`Create` comprueba `Directory.Exists` sobre `<fecha>_<slug>` y prueba `_2`, `_3`, … hasta `_99`, y
devuelve la primera libre; si ninguna lo está, lanza y `MainViewModel` muestra `ErrorStart` (ya lo
hace). El nombre generado incluye segundos, así que esta rama solo se recorre con nombre propio.
Alternativas: resolverlo en las sinks (imposible, cada una recibe la ruta ya fijada) o preguntar al
tester (bloquea la prueba). Esto hace que `Create` consulte el disco: las pruebas nuevas usan
`Path.GetTempPath()` y la limpian, en lugar de una ruta falsa como las actuales.

En la práctica esta rama casi nunca se recorre desde la interfaz: como el nombre de carpeta lleva la
hora al segundo, dos sesiones iniciadas a mano caen en carpetas distintas. El sufijo queda como red
para dos TEC iniciando en el mismo segundo, y así lo cubre la prueba de `Tec.Core.Tests`.

**D6 — El factory reserva la carpeta nada más resolverla** con `Directory.CreateDirectory`, en lugar
de dejarlo en las sinks. Así el hueco entre "está libre" y "es mía" se cierra, y si dos TEC eligieran
el mismo nombre el segundo vería la carpeta ocupada y tomaría `_2`.

**D7 — La interfaz: `TextBox` en el grupo de carpeta de salida, con etiqueta.** Va junto a
`Examinar…`, porque el nombre y la carpeta son decisiones de destino de la sesión. Claves nuevas en
`Strings.resx`/`Strings.en.resx` (`SessionNameGroup`, `SessionNamePreviewTip`). La explicación de que
vacío = nombre automático no va en un texto aparte: la da la propia pista (D9). No se persiste ni se
rellena al iniciar.

**D8 — El nombre previsto se pide a Core, no lo recomina la interfaz.** `EvidenceSession` expone
`PreviewSessionName(targetNames, now)`, la misma ruta que usa `Create` para el `<fecha>_<slug>`, sin
tocar el disco. Así la pista y la carpeta real no pueden divergir en el saneado ni en el tope de 40.
No incluye el sufijo anti-colisión: si `<fecha>_<slug>` ya existe, la carpeta real será `_2` y eso se
ve en el estado y el log. Alternativa: un `PreviewName` propio en `MainViewModel`; se descarta porque
duplicaría `Sanitize` y `DescribeTargets`.

**D9 — La pista es una superposición dentro del `TextBox`, no su valor.** WPF no tiene placeholder
nativo: un `TextBlock` en el mismo `Grid`, con el mismo `Padding` que el campo, gris, visible solo
mientras `SessionName` está vacío (`DataTrigger`). La etiqueta del grupo dice "Nombre de la sesión
(opcional)" y el `ToolTip` del campo aclara que lo gris es lo que se usará si se deja vacío, para que
la pista no se confunda con un ejemplo de nombre. Se recalcula al cambiar el conjunto marcado
(`OnProcessSelectionChanged`) y al terminar la sesión; el reloj puede quedarse desfasado unos minutos,
y el delta lo declara orientativo. Alternativas descartadas: escribir el nombre generado como valor
del campo y limpiarlo al enfocar (un placeholder que se convierte en valor, con riesgo de grabar el
nombre automático creyendo que era propio), y un `DispatcherTimer` por minuto para refrescar la hora
(un temporador más por un detalle cosmético).

## Risks / Trade-offs

- [El nombre propio tapa qué aplicaciones se siguieron] → requisito "El reporte identifica las
  aplicaciones de la sesión" y línea `Aplicaciones` en la cabecera; la carpeta del log y el estado de
  la interfaz siguen mostrando la ruta completa.
- [`_2` es silencioso y el tester no se entera de que hay dos carpetas del mismo flujo] → el log y el
  estado registran la ruta real (`Sesión iniciada en …`), y el nombre generado nunca colisiona.
- [`Create` pasa a tocar el sistema de archivos] → el resto de `EvidenceSession` sigue siendo puro;
  la única salida al disco es la búsqueda de hueco, y las pruebas la ejercitan en una carpeta
  temporal.
- [El reloj de la pista envejece y la carpeta real lleva otra hora] → el delta declara la pista
  orientativa, y el estado y el log muestran la ruta real al iniciar.
- [El tester confunde la pista con un nombre ya escrito] → la pista no es el valor del campo: es un
  `TextBlock` gris que desaparece al teclear el primer carácter.
- [El tester escribe nombres larguísimos o con saltos de línea] → recorte a 40 y `_` por espacios; la
  carpeta nunca supera los 40 caracteres del nombre.
- [Cambiar el `<h1>` del reporte] → solo afecta a sesiones con nombre propio; `HtmlReportSinkTests`
  conserva la aserción del título actual para el caso sin nombre.

## Migration Plan

Nada que migrar: las carpetas existentes no se tocan y el JSONL, las imágenes y las leyendas no
cambian. Rollback = quitar el `TextBox` y el `SessionName`; `EvidenceSession.Create` conserva la
sobrecarga sin nombre, así que el código anterior sigue compilando.

## Open Questions

- ¿Conviene recordar el último nombre entre sesiones (como el idioma)? No afecta a los requisitos ni
  a las tareas: se decide al pulir la interfaz.
