# 0001 — Tres deployables sobre un único bounded context

- **Estado:** Aceptada
- **Fecha:** 2026-09-08

## Contexto

El enunciado pide "Backend & Microservicios (o Arquitectura Modular)" y evalúa explícitamente el
entendimiento de arquitecturas distribuidas. El sistema tiene tres responsabilidades con perfiles de
carga muy distintos:

- **Ingesta:** miles de escrituras pequeñas por segundo, latencia crítica, no puede caerse aunque
  falle la base de datos.
- **Procesamiento:** trabajo asíncrono, tolera latencia, puede reintentar.
- **Consulta:** lecturas y conexiones WebSocket persistentes, cada una con su propio consumo de
  memoria.

Con un único proceso, un pico de dashboards abiertos compite por recursos con la ingesta de GPS.

## Decisión

Desplegamos **tres servicios independientes** —`Ingestion.Api`, `Processing.Worker` y `Query.Api`—
que comparten un **único bounded context** de dominio (`Fleet Telemetry`) materializado en los
proyectos `Domain`, `Application` e `Infrastructure`.

La separación es por **perfil de escalado**, no por dominio, y así se declara.

## Alternativas consideradas

| Opción | A favor | En contra | Por qué no |
|---|---|---|---|
| Monolito modular | Un solo despliegue, transacciones locales, mucho menos trabajo de infraestructura | El circuit breaker y la cola quedan como decoración: sin frontera de red no hay fallo parcial real que tolerar | El enunciado evalúa arquitecturas distribuidas y resiliencia; sin frontera de proceso no hay nada que demostrar |
| Microservicios con dominio partido (un bounded context por servicio, cada uno con su BD) | Ortodoxia DDD, autonomía total de cada equipo | Tres esquemas, duplicación del modelo de vehículo, consistencia entre servicios para algo que es un solo concepto | Sería un dominio partido artificialmente: "vehículo" y "posición" son el mismo concepto en los tres servicios. Partirlo produciría el coste de los microservicios sin su beneficio |
| Serverless (Lambda por función) | Escalado a cero, coste bajo con tráfico irregular | Cold start en la ruta de ingesta, y WebSockets con SignalR requieren infraestructura aparte | La ingesta es de latencia sensible y sostenida; es el peor caso para cold starts |

## Consecuencias

**Positivas**
- Cada servicio escala según su propio cuello de botella.
- La frontera de red hace que el Circuit Breaker y la cola resuelvan un problema real y demostrable:
  se apaga la base de datos y la ingesta sigue aceptando.
- El dominio se mantiene en un solo sitio: una regla de negocio se cambia una vez.

**Negativas**
- Un cambio en `Domain` obliga a redesplegar los tres servicios; están acoplados por versión.
- Depuración distribuida: seguir una posición desde el POST hasta el dashboard cruza tres procesos y
  un broker. Se mitiga propagando un `correlationId` en los logs estructurados.
- Los tests de integración necesitan levantar infraestructura real (Testcontainers), y son más lentos
  que los unitarios.

**Reversibilidad**
Alta hacia el monolito: los tres hosts son delgados y componen los mismos casos de uso; fusionarlos
es principalmente trabajo de configuración de DI. Baja hacia el dominio partido: eso exigiría
rediseñar el modelo y la estrategia de datos.
