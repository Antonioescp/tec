# Spec Delta

## Purpose

Define qué es una sesión de recolección de evidencias y qué produce: una carpeta, una numeración
continua, un único reporte y la identificación de la aplicación a la que pertenece cada evidencia.

## ADDED Requirements

### Requirement: Una sesión agrupa las evidencias de todas las aplicaciones seguidas

TEC SHALL generar una única secuencia numerada y una única carpeta para las evidencias de todas
las aplicaciones seguidas, ordenadas por instante de la interacción.

#### Scenario: Flujo que salta entre dos aplicaciones

- **WHEN** en una misma sesión el tester interactúa con la aplicación probada y con otra
  herramienta
- **THEN** las evidencias aparecen correlativas y en orden cronológico en la misma carpeta y el
  mismo reporte

#### Scenario: Evidencias de una sola aplicación

- **WHEN** la sesión sigue una sola aplicación
- **THEN** la numeración, la carpeta y el reporte son exactamente como hoy

### Requirement: Nombre de la carpeta y del reporte según el conjunto seguido

TEC SHALL nombrar la carpeta de evidencias y el título del reporte con la o las aplicaciones
seguidas; con una sola aplicación SHALL conservar el nombre actual, y con varias SHALL combinar
los nombres de forma legible y dentro del límite de longitud de nombre de archivo.

#### Scenario: Sesión con dos aplicaciones

- **WHEN** se inicia una sesión que sigue `Farmax` y `Postman`
- **THEN** la carpeta es `<fecha>_Farmax+Postman` y el título del reporte lista las dos aplicaciones

### Requirement: Cada evidencia identifica su aplicación

TEC SHALL indicar en cada evidencia a qué aplicación pertenece, además de la ventana en la que
ocurrió, en la leyenda de la captura y en el reporte.

#### Scenario: Click en la herramienta externa

- **WHEN** el tester hace click en una ventana de la segunda aplicación
- **THEN** la leyenda de esa evidencia identifica la aplicación y la ventana

### Requirement: La captura es por interacción, no por aplicación

TEC SHALL capturar, en el modo de área Ventana, la ventana que recibió la interacción, y SHALL
combinar en una misma imagen solo la ventana de la interacción y su ventana propietaria.

#### Scenario: Dos aplicaciones visibles a la vez

- **WHEN** dos aplicaciones seguidas están abiertas y el tester interactúa con una
- **THEN** la captura muestra la ventana de la interacción, sin incluir la ventana de la otra
  aplicación

### Requirement: Los cambios en pantalla se observan en la ventana en primer plano del conjunto

TEC SHALL comparar el contenido de la ventana en primer plano que pertenezca al conjunto seguido,
conservando la comparación anterior de cada ventana mientras está en segundo plano.

#### Scenario: La aplicación cambia mientras el tester está en otra

- **WHEN** el tester vuelve a la aplicación probada después de trabajar en otra aplicación seguida
- **THEN** TEC genera la evidencia del cambio que ocurrió mientras la ventana estaba en segundo
  plano
