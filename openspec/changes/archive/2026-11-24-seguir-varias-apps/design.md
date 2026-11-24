# Design

## Context

La motivación y el alcance están en `proposal.md`; los requisitos en `specs/`. Situación técnica
que condiciona el cómo:

- El pipeline **ya es multi-proceso**: `ProcessInteractionFilter` recibe `IEnumerable<int>`
  (`Tec.Core/Services/Basics.cs:13`) y `TargetForegroundWindowProvider` también
  (`Tec.Windows/Sources/ContentChangeSource.cs:17`).
- Cada fuente deriva el PID **de la ventana que recibió el evento**
  (`MouseClickSource.cs:72`, `WindowShownSource.cs:61`), no del foco global.
- La suposición de una sola aplicación está en tres sitios: la selección única de la interfaz
  (`MainViewModel.cs:34,88`), `SessionSettings.Target` (`EvidenceSessionFactory.cs:22`, usado en
  `:73` y `:116`) y `EvidenceSession` con una sola `TargetDescription` (`Tec.Core/Model/Evidence.cs:20-39`).
- `Tec.Core` no conoce Win32: todo lo que sea proceso/ventana vive en `Tec.Windows` o en el
  composition root de `Tec.App`.

## Goals / Non-Goals

**Goals**

- Un concepto nuevo en la interfaz y en el modelo: *conjunto de aplicaciones seguidas*.
- Numeración, carpeta y reporte únicos, con la aplicación identificada por evidencia.
- Que una sesión con **una** aplicación sea idéntica a hoy, incluido el nombre de la carpeta.

**Non-Goals**

- Vigilar ventanas en segundo plano de las aplicaciones seguidas (sigue habiendo una comparación
  por tick).
- Seguir aplicaciones por nombre de proceso, ni descubrir procesos hijos de una aplicación.
- Añadir aplicaciones a una sesión en curso, ni capturas que muestren dos aplicaciones a la vez.

## Decisions

### D1. `SessionSettings.Target` pasa a `Targets` (conjunto ordenado)

`IReadOnlyList<TargetProcessInfo> Targets`. Alternativa considerada: mantener `Target` y añadir
una lista aparte — se descarta porque deja dos fuentes de verdad y el caso de una aplicación
quedaría como rama especial en cada sitio.

### D2. Seguir por PID resuelto al iniciar, no por nombre de proceso

La selección de la interfaz da PIDs concretos. Por nombre se capturarían **todas** las instancias
del mismo ejecutable (dos ventanas de la misma app abiertas a la vez producirían evidencia), y el
tester elige a mano, así que la intención explícita gana. El riesgo del reinicio se cubre en D4.

### D3. La identidad de la aplicación se resuelve una vez, en el enriquecedor de ventana

`WindowInfoEnricher` (`Tec.Windows/Capture/WindowInfoEnricher.cs`), que ya consulta Win32 por la
ventana, rellena además `Interaction.ProcessName` a partir del PID. Así `CaptionFormatter`,
`HtmlReportSink` y `evidencias.jsonl` leen el nombre sin que `Tec.Core` necesite saber de procesos
y sin tocar cada fuente.

Alternativa considerada: pasar un diccionario PID → nombre al `CaptionFormatter` y al reporte —
descartada porque duplica la información y no llega al JSONL.

Si la ventana ya cerró antes de enriquecer, el nombre queda vacío y la leyenda omite la
aplicación, como hoy omite la ventana.

### D4. Proceso muerto: aviso, no silencio

`Tec.Windows` observa los procesos del conjunto y notifica al `Tec.App` cuándo uno desaparece:
se registra en el log, se muestra en la interfaz y `HtmlReportSink` añade una nota al reporte. La
sesión continúa con las aplicaciones restantes y la numeración no se rompe.

Alternativa considerada: re-resolver el PID del mismo nombre automáticamente — descartada por D2
(podría agarrar otra instancia no elegida).

### D5. Nombre de carpeta y título: se construyen en el composition root

`EvidenceSession.Create` sigue recibiendo **una** descripción. El composition root la arma:
una aplicación → el nombre de hoy; varias → `App1+App2`. El saneado y el corte a 40 caracteres
siguen en `Tec.Core` y se aplican al conjunto completo; si no caben todos, se mantienen los
primeros nombres y se añade `+N`.

Alternativa considerada: `<fecha>_multiapp` — descartada porque el nombre de carpeta es la forma
que tiene el tester de encontrar una prueba.

### D6. Interfaz: multi-selección en la misma lista

La lista de aplicaciones pasa a selección múltiple (marcas) y `Iniciar aplicación` añade a la
selección lo que lance. No hay nueva pantalla ni nuevo control de "modo foco": pertenecer al
conjunto es la única regla.

### D7. Cambios en pantalla: sin cambios

`TargetForegroundWindowProvider` ya devuelve la ventana en primer plano del conjunto y
`ContentChangeSource` conserva el detector por ventana (`ContentChangeSource.cs:53,119`), que es
lo que hace que al volver a una ventana se detecte lo que cambió mientras estaba en segundo plano.
Seguir N aplicaciones no añade comparaciones por tick.

## Risks / Trade-offs

- La aplicación seguida se reinicia a mitad de prueba y su PID cambia → evidencia perdida: se
  convierte en un aviso visible (D4) en lugar de un silencio.
- Postman y otras apps Electron/Chromium responden lento a UI Automation → la evidencia se guarda
  igual, con la nota y el resaltado de ventana completa que ya existen (limitación conocida del README).
- La leyenda crece (aplicación + ventana) → se usa el nombre corto del proceso, no el título.
- El reporte puede quedar con muchas aplicaciones en el título → entra en el mismo corte de
  longitud que la carpeta (D5).
- Dos sesiones iniciadas en el mismo segundo con las mismas aplicaciones → mismo nombre de carpeta
  que hoy; no lo agrava este cambio.

## Migration Plan

Sin migración de datos: `evidencias.jsonl` gana un campo (`ProcessName`), los consumidores de
`reporte.html` y de las imágenes no cambian. Con una sola aplicación elegida, el nombre de la
carpeta y el reporte son los de hoy, así que no hay compatibilidad que romper. Revertir es
deshacer el cambio: no hay formato persistente nuevo.

## Open Questions

- ¿Se recuerda el conjunto de aplicaciones entre sesiones en `settings.json`? No afecta a los
  requisitos ni a las tareas; se decide al implementar la parte de la interfaz.
