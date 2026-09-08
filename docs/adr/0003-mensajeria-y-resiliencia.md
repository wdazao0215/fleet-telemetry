# 0003 — RabbitMQ tras un puerto, con circuit breaker y buffer local

- **Estado:** Aceptada
- **Fecha:** 2026-09-08

## Contexto

El enunciado es explícito: *"Si la base de datos de persistencia falla temporalmente o el servicio de
ruteo está inaccesible, el sistema de ingesta no debe caer (debe encolar o manejar el error
elegantemente)"*.

La ingesta recibe posiciones de vehículos en movimiento. Un dato perdido no se puede volver a pedir:
el vehículo ya no está donde estaba. Y un dispositivo a bordo con conectividad intermitente no puede
asumir que un error de servidor significa "reintenta luego".

## Decisión

Tres piezas, en este orden:

1. **`IEventBus` como puerto en `Application`.** El caso de uso publica un evento y no sabe qué hay
   detrás.
2. **RabbitMQ como adaptador**, con exchange topic, colas durables y dead-letter exchange declarado
   desde el primer arranque.
3. **`ResilientEventBus` como decorador**: reintentos con backoff exponencial y jitter → circuit
   breaker → buffer local acotado con drenaje en segundo plano.

La secuencia importa. Los reintentos cubren el fallo transitorio. El circuit breaker cubre el fallo
sostenido: sin él, cada petición esperaría el timeout completo del broker y la ingesta moriría por
agotamiento de hilos aunque, formalmente, "no se hubiera caído". Con el circuito abierto el fallo es
inmediato y barato, y el mensaje va al buffer.

## Alternativas consideradas

| Opción | A favor | En contra | Por qué no |
|---|---|---|---|
| Redis Streams | Un contenedor menos: Redis ya está por la caché | Mezcla caché y mensajería en la misma pieza; si Redis cae, se pierden a la vez la deduplicación y la cola | Concentrar los dos fallos en un único componente contradice el objetivo de resiliencia |
| Tabla outbox en Postgres | Garantía transaccional con la escritura de negocio | La base de datos puede ser justamente lo que está caído | Un fallback que depende del componente que falló no es un fallback |
| Llamada HTTP directa al worker | Sin infraestructura extra, más simple de depurar | Acopla la disponibilidad de la ingesta a la del worker | Es exactamente el acoplamiento que el requisito pide eliminar |
| SQS desde el principio | Es el destino en AWS | No hay entorno AWS en esta prueba, y LocalStack pesa | El puerto `IEventBus` deja el cambio en escribir un adaptador |

## Consecuencias

**Positivas**
- La ingesta responde `202` con el broker apagado. Verificado: 10 de 10 lecturas aceptadas, circuito
  abierto, 10 mensajes en el buffer y 0 descartados; al volver el broker, los 10 se drenaron y la
  cola pasó de 1 a 11 mensajes.
- El estado del circuito es observable en `/health/resilience`, así que la tolerancia a fallos se
  puede *enseñar*, no solo afirmar.
- Migrar a Amazon SQS o EventBridge es escribir un adaptador; el dominio no se entera.

**Negativas**
- **El buffer vive en memoria: si el proceso muere mientras contiene mensajes, esos mensajes se
  pierden.** Es la contrapartida consciente de no depender del disco ni de la base de datos. En
  producción sería un outbox en disco local o un volumen persistente.
- El buffer es acotado (10.000 por defecto) y descarta los mensajes más antiguos cuando se llena.
  Ante una caída prolongada se pierde histórico, no posición actual: un vehículo se localiza con su
  última posición, no con la de hace veinte minutos.
- Una capa más de indirección que hay que entender al depurar por qué un mensaje no llegó.

**Reversibilidad**
Alta. El decorador se puede quitar del registro de DI y la publicación pasa a ser directa; y el
adaptador de RabbitMQ se puede sustituir sin tocar `Application` ni `Domain`.
