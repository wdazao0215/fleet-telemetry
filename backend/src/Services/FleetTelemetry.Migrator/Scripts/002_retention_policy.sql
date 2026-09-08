-- Retención y compresión.
-- La telemetría GPS crece sin descanso: seis vehículos emitiendo cada 3 segundos generan ~170.000
-- filas al día. Sin política, la tabla crece hasta que alguien se da cuenta en producción.

ALTER TABLE positions SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'vehicle_id',
    timescaledb.compress_orderby = 'recorded_at DESC'
);

-- Se comprime lo que ya nadie consulta en caliente. El dashboard mira la última hora; el análisis
-- histórico tolera de sobra el coste de descomprimir.
SELECT add_compression_policy('positions', INTERVAL '7 days', if_not_exists => TRUE);

-- Retención de 90 días: suficiente para investigar un incidente y acotado para no acumular
-- indefinidamente datos de localización, que además son datos personales.
SELECT add_retention_policy('positions', INTERVAL '90 days', if_not_exists => TRUE);
