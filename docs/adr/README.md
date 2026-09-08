# Decisiones arquitectónicas (ADR)

Registro de las decisiones técnicas del proyecto y, sobre todo, de **por qué** se tomaron y qué se
descartó. Formato [MADR](https://adr.github.io/madr/), en español, numeración secuencial.

Un ADR aceptado no se reescribe: si la decisión cambia, se escribe uno nuevo que lo sustituye.

| # | Decisión | Estado |
|---|---|---|
| [0001](0001-topologia-de-servicios.md) | Tres deployables sobre un único bounded context | Aceptada |
| [0002](0002-cqrs-con-dispatcher-propio.md) | CQRS con dispatcher propio en lugar de MediatR | Aceptada |
| [0003](0003-mensajeria-y-resiliencia.md) | RabbitMQ tras un puerto, con circuit breaker y buffer local | Aceptada |
