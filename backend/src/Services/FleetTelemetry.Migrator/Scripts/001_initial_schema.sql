-- Esquema inicial de Fleet Telemetry.
-- Idempotente: el migrador se ejecuta en cada arranque del entorno y no debe fallar al repetirse.

CREATE EXTENSION IF NOT EXISTS timescaledb;

CREATE TABLE IF NOT EXISTS vehicles (
    id                       VARCHAR(64) PRIMARY KEY,
    label                    VARCHAR(128) NOT NULL,
    state                    INTEGER      NOT NULL DEFAULT 0,
    registered_at            TIMESTAMPTZ  NOT NULL,
    deletion_requested_at    TIMESTAMPTZ  NULL,
    deletion_failure_reason  VARCHAR(512) NULL
);

CREATE INDEX IF NOT EXISTS ix_vehicles_state ON vehicles (state);

CREATE TABLE IF NOT EXISTS alerts (
    id               UUID PRIMARY KEY,
    vehicle_id       VARCHAR(64)  NOT NULL,
    kind             INTEGER      NOT NULL,
    latitude         DOUBLE PRECISION NOT NULL,
    longitude        DOUBLE PRECISION NOT NULL,
    raised_at        TIMESTAMPTZ  NOT NULL,
    acknowledged_at  TIMESTAMPTZ  NULL,
    detail           VARCHAR(512) NOT NULL
);

CREATE INDEX IF NOT EXISTS ix_alerts_raised_at ON alerts (raised_at DESC);
CREATE INDEX IF NOT EXISTS ix_alerts_vehicle_id ON alerts (vehicle_id);

-- La clave primaria incluye recorded_at porque TimescaleDB exige que todo índice único contenga la
-- columna de particionado. Es además la que da idempotencia al consumidor: RabbitMQ entrega
-- at-least-once, y el ON CONFLICT DO NOTHING del repositorio se apoya en esta restricción para que
-- reprocesar un mensaje no duplique el recorrido.
CREATE TABLE IF NOT EXISTS positions (
    vehicle_id   VARCHAR(64)      NOT NULL,
    recorded_at  TIMESTAMPTZ      NOT NULL,
    latitude     DOUBLE PRECISION NOT NULL,
    longitude    DOUBLE PRECISION NOT NULL,
    PRIMARY KEY (vehicle_id, recorded_at)
);

-- Convertir en hypertable: TimescaleDB parte la tabla en chunks por rango de tiempo de forma
-- transparente. Las consultas de recorrido filtran siempre por ventana temporal, así que el planner
-- descarta chunks enteros en lugar de recorrer un índice sobre millones de filas.
SELECT create_hypertable(
    'positions',
    'recorded_at',
    chunk_time_interval => INTERVAL '1 day',
    if_not_exists => TRUE
);

CREATE INDEX IF NOT EXISTS ix_positions_vehicle_time
    ON positions (vehicle_id, recorded_at DESC);
