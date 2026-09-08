# 0004 — TimescaleDB para el histórico, con esquema en scripts SQL

- **Estado:** Aceptada
- **Fecha:** 2026-09-08

## Contexto

Las posiciones GPS son una serie temporal pura: se escriben una vez, nunca se actualizan, y se
consultan casi siempre acotadas por una ventana de tiempo y un vehículo. Seis vehículos emitiendo
cada 3 segundos generan unas **170.000 filas al día**; una flota real de 500 vehículos supera los 14
millones.

El resto del modelo —vehículos y alertas— es relacional corriente, con transacciones y actualizaciones.

La plataforma del cliente ya usa **Druid** para analítica, lo que confirma que tratan la telemetría
como serie temporal y no como filas sueltas.

## Decisión

**PostgreSQL con la extensión TimescaleDB.** La tabla `positions` es una *hypertable* particionada
por `recorded_at` en chunks de un día, con compresión a los 7 días y retención a los 90.

`vehicles` y `alerts` son tablas relacionales normales gestionadas con EF Core.

**El esquema se define en scripts SQL versionados, no con EF Migrations.** `create_hypertable`,
`add_compression_policy` y `add_retention_policy` no se pueden expresar en el modelo de EF; mezclar
ambos mecanismos dejaría media verdad en las migraciones y la otra media en un script suelto.

## Alternativas consideradas

| Opción | A favor | En contra | Por qué no |
|---|---|---|---|
| PostgreSQL a secas con particionado nativo | Sin extensiones; cualquier RDS lo soporta | Las particiones se crean y se podan a mano, o con un cron propio; sin compresión de series | Es reimplementar peor lo que la extensión ya hace, y el mantenimiento recae en nosotros |
| MongoDB con índice 2dsphere | Documentos flexibles, TTL nativo, geoconsultas potentes | Sin transacciones cómodas para la saga de borrado, y otro motor más que operar junto a Postgres | La consistencia del borrado es un requisito explícito del enunciado |
| Druid o ClickHouse | Lo que usa el cliente; excelente en analítica masiva | Ingesta orientada a lotes y sin transacciones para el modelo relacional; haría falta un segundo motor igualmente | Desproporcionado para un prototipo, y no elimina la necesidad de Postgres |
| InfluxDB | Especializado en series temporales | Un motor más y otro lenguaje de consulta, y de nuevo Postgres seguiría haciendo falta | Dos bases de datos donde una extensión resuelve el problema |

## Consecuencias

**Positivas**
- Las consultas de recorrido filtran por ventana temporal, así que el planner descarta chunks
  enteros en vez de recorrer un índice sobre millones de filas.
- La compresión y la retención son declarativas: el crecimiento infinito de la tabla no depende de
  que alguien se acuerde. Los datos de localización son datos personales, y una retención acotada es
  también una decisión de privacidad, no solo de espacio.
- Una sola base de datos para todo el sistema: una conexión, una copia de seguridad, un motor que
  operar.
- La clave primaria `(vehicle_id, recorded_at)` —que TimescaleDB exige que incluya la columna de
  particionado— da además **idempotencia** al consumidor: con `ON CONFLICT DO NOTHING`, reprocesar
  un mensaje de una cola at-least-once no duplica el recorrido.

**Negativas**
- Es una extensión: en AWS, RDS no la ofrece en su edición gestionada, así que habría que usar
  Aurora con la extensión disponible, Timescale Cloud, o autogestionar Postgres en EC2. Está
  anotado en el Terraform.
- El esquema en scripts SQL renuncia a la generación automática de migraciones de EF: hay que
  escribir el DDL a mano y mantenerlo alineado con las entidades.
- `positions` queda fuera del change tracker de EF, así que se escribe con SQL parametrizado. Es
  deliberado —el tracker no aporta nada en una tabla de solo-inserción— pero rompe la simetría con
  el resto del acceso a datos.

**Reversibilidad**
Media. Volver a Postgres plano es quitar `create_hypertable` y añadir particionado declarativo; los
datos y las consultas siguen siendo válidos porque una hypertable es una tabla normal por debajo.
