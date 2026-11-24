# target-selection Specification

## Purpose

Define qué aplicaciones se siguen durante una prueba: cómo se eligen antes de iniciar la sesión,
cómo se comporta el conjunto mientras la sesión está en curso y qué ocurre si una de ellas deja
de existir.

## Requirements

### Requirement: Selección de varias aplicaciones antes de iniciar

TEC SHALL permitir elegir más de una aplicación a seguir antes de iniciar la sesión, y SHALL
exigir al menos una para poder iniciarla.

#### Scenario: Se eligen dos aplicaciones

- **WHEN** el tester marca la aplicación probada y otra herramienta en la lista de aplicaciones
- **THEN** TEC muestra ambas como objetivo de la sesión y permite pulsar Iniciar

#### Scenario: No se elige ninguna

- **WHEN** el tester no marca ninguna aplicación
- **THEN** TEC no inicia la sesión y mantiene Iniciar deshabilitado

### Requirement: Aplicación lanzada desde TEC entra en el conjunto

Cuando TEC lanza una aplicación, SHALL añadirla al conjunto de aplicaciones seguidas de esa
sesión.

#### Scenario: Se lanza la aplicación probada y además se marca otra

- **WHEN** el tester lanza una aplicación desde TEC y marca además otra ya abierta
- **THEN** la sesión sigue las dos aplicaciones

### Requirement: El conjunto no cambia durante la sesión

Una vez iniciada la sesión, TEC SHALL seguir exactamente el conjunto elegido al iniciar; para
seguir otra aplicación SHALL detener la sesión e iniciar una nueva.

#### Scenario: Se abre una aplicación nueva a mitad de prueba

- **WHEN** el tester abre una aplicación que no estaba en el conjunto mientras la sesión está en
  curso
- **THEN** TEC no genera evidencias de esa aplicación

### Requirement: Aviso cuando una aplicación seguida desaparece

Si un proceso seguido termina mientras la sesión está en curso, TEC SHALL avisar (en la interfaz,
en el log y como nota en el reporte) y SHALL seguir la sesión con las aplicaciones restantes.

#### Scenario: Se cierra la herramienta usada en la prueba

- **WHEN** el tester cierra una de las aplicaciones seguidas a mitad de sesión
- **THEN** TEC muestra y registra que esa aplicación dejó de existir, el reporte lo refleja, y las
  evidencias de las aplicaciones restantes se siguen capturando en la misma numeración
