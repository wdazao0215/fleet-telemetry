# Fleet Telemetry

Sistema de monitoreo y telemetría de flotas con GPS: ingesta de coordenadas de múltiples vehículos,
detección de anomalías en tiempo real y dashboard web operativo.

> Prueba técnica Senior Fullstack. La documentación completa —arquitectura, decisiones, instrucciones
> de ejecución, Reporte de IA y Desafíos y Soluciones— se construye a lo largo del desarrollo.

## Arranque rápido

```bash
docker compose up --build
```

| Servicio | URL |
|---|---|
| Dashboard | http://localhost:3000 |
| Ingestion API | http://localhost:8081 |
| Query API | http://localhost:8082 |
| RabbitMQ (management) | http://localhost:15672 |

## Documentación

- [`CLAUDE.md`](CLAUDE.md) — guía de arquitectura y convenciones del proyecto
- [`docs/adr/`](docs/adr/) — decisiones arquitectónicas registradas
