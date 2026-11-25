# Spec Delta

## ADDED Requirements

### Requirement: El tester puede dar nombre a la sesión

TEC SHALL ofrecer un campo opcional para nombrar la sesión, antes de iniciarla. Si el campo está
vacío, SHALL usar el nombre generado a partir de las aplicaciones seguidas.

#### Scenario: Nombre escrito por el tester

- **WHEN** el tester escribe `Login con Postman` y inicia la sesión
- **THEN** la carpeta de evidencias y el título del reporte usan ese nombre, conservando el prefijo
  de fecha: `<fecha>_Login_con_Postman`

#### Scenario: Campo vacío

- **WHEN** el tester deja el campo vacío y inicia la sesión
- **THEN** la carpeta y el título del reporte se generan con las aplicaciones seguidas, exactamente
  como hoy

#### Scenario: El nombre no cambia lo que se captura

- **WHEN** una sesión se inicia con un nombre propio
- **THEN** la numeración, las capturas, la leyenda de cada evidencia y los metadatos de cada
  evidencia son los mismos que con el nombre generado

### Requirement: El campo de nombre anticipa el nombre automático

TEC SHALL mostrar en el campo de nombre, mientras esté vacío, el nombre de carpeta que se usaría con
las aplicaciones marcadas, y SHALL retirarlo en cuanto el tester escriba. El reloj de esa pista es
orientativo: el nombre real se fija al iniciar la sesión.

#### Scenario: Cambia el conjunto marcado

- **WHEN** el tester marca o desmarca aplicaciones con el campo vacío
- **THEN** la pista muestra el nombre completo correspondiente al conjunto recién marcado

#### Scenario: El tester empieza a escribir

- **WHEN** el tester escribe un nombre en el campo
- **THEN** la pista deja de verse y el campo muestra solo lo escrito

#### Scenario: Se inicia con la pista visible

- **WHEN** se inicia la sesión sin escribir nada
- **THEN** la carpeta usa el nombre de las aplicaciones que mostraba la pista, con la fecha y hora en
  que se inició la sesión

#### Scenario: No hay aplicaciones marcadas

- **WHEN** el campo está vacío y no hay ninguna aplicación marcada
- **THEN** la pista muestra el nombre que resultaría sin aplicaciones y *Iniciar* sigue deshabilitado

### Requirement: El nombre de sesión se ajusta para que sea un nombre de carpeta válido

TEC SHALL sustituir en el nombre escrito los caracteres no permitidos en un nombre de archivo y los
espacios por `_`, SHALL eliminar los puntos y espacios finales, y SHALL limitarlo a los mismos 40
caracteres que el nombre generado.

#### Scenario: Nombre con espacios

- **WHEN** el tester escribe `Prueba de login mal`
- **THEN** la carpeta se llama `<fecha>_Prueba_de_login_mal`

#### Scenario: Nombre demasiado largo

- **WHEN** el tester escribe un nombre de más de 40 caracteres
- **THEN** la carpeta usa los primeros 40 caracteres del nombre

#### Scenario: Nombre que termina en punto

- **WHEN** el tester escribe un nombre terminado en punto
- **THEN** la carpeta se crea sin ese punto final

#### Scenario: Nombre sin caracteres aprovechables

- **WHEN** el tester escribe solo caracteres no permitidos en un nombre de archivo
- **THEN** la carpeta usa el nombre generado con las aplicaciones seguidas

### Requirement: Un nombre de sesión repetido no impide iniciar la sesión

TEC SHALL, cuando la carpeta del nombre elegido ya exista, crear la sesión en la siguiente
numeración disponible (`_2`, `_3`, …) sin pedir confirmación ni detener la grabación.

#### Scenario: El mismo nombre dos veces

- **WHEN** se inicia una sesión con un nombre cuya carpeta ya existe
- **THEN** la sesión se crea en `<nombre>_2` y la grabación empieza sin ningún aviso

### Requirement: El reporte identifica las aplicaciones de la sesión

TEC SHALL listar en la cabecera del reporte las aplicaciones seguidas por la sesión, se haya dado o
no un nombre propio.

#### Scenario: Sesión con nombre propio

- **WHEN** se inicia una sesión llamada `Login con Postman` que sigue `Farmax` y `Postman`
- **THEN** el título del reporte es `Login con Postman` y la cabecera lista `Farmax` y `Postman`

## MODIFIED Requirements

### Requirement: Nombre de la carpeta y del reporte según el conjunto seguido

TEC SHALL nombrar la carpeta de evidencias y el título del reporte con el nombre de la sesión, que
sin nombre escrito por el tester se genera a partir de las aplicaciones seguidas: con una sola
aplicación SHALL conservar el nombre actual y con varias SHALL combinar los nombres de forma legible
y dentro del límite de longitud de nombre de archivo.

#### Scenario: Sesión con dos aplicaciones

- **WHEN** se inicia sin nombre propio una sesión que sigue `Farmax` y `Postman`
- **THEN** la carpeta es `<fecha>_Farmax+Postman` y el título del reporte lista las dos aplicaciones
