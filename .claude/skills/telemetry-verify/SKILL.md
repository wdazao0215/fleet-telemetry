---
name: telemetry-verify
description: >
  Brings up the full docker compose environment and verifies the telemetry system end to end —
  ingestion, deduplication, malformed payloads, stopped-vehicle alert, circuit breaker, deletion saga
  and the live dashboard. Use before closing any feature, when the user says "verifica", "pruébalo",
  "levanta el entorno", or before opening a PR.
---

# Verificación end-to-end

Una feature no está terminada porque compile ni porque los tests pasen. Está terminada cuando la has
**ejecutado**. Esta skill es el guion de esa comprobación.

## 0. Requisito previo

```bash
docker info >/dev/null 2>&1 || echo "Docker Desktop no está corriendo"
```

Si el daemon está caído, pídele al usuario que abra Docker Desktop. No intentes rodearlo.

## 1. Levantar

```bash
docker compose up --build -d
docker compose ps          # todos los servicios en healthy
```

## 2. Ingesta y deduplicación

```bash
API=http://localhost:8081/api/v1/telemetry
KEY="X-Api-Key: dev-fleet-key"
BODY='{"vehicleId":"VH-001","latitude":4.7110,"longitude":-74.0721,"timestamp":"2026-01-01T10:00:00Z"}'

curl -si -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "$BODY" | head -1
# esperado: 202 Accepted

curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "$BODY" | jq .duplicate
# esperado: true — y sin fila nueva en la base
```

## 3. Payload malformado

```bash
curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' \
  -d '{"vehicleId":"VH-001","latitude":999,"longitude":-74,"timestamp":"2026-01-01T10:00:00Z"}' | jq .
# esperado: 400 con ProblemDetails nombrando el campo latitude
```

## 4. Alerta de vehículo detenido

El simulador mantiene un vehículo estacionario a propósito. Pasado un minuto:

```bash
curl -s http://localhost:8082/api/v1/alerts | jq '.[0]'
```

Y compruébalo en el dashboard **sin recargar la página**: es lo que pide el enunciado.

## 5. Circuit breaker

```bash
docker compose stop timescaledb
curl -si -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "$BODY" | head -1
# esperado: 202 — la ingesta NO cae
curl -s http://localhost:8081/health/resilience | jq .
# esperado: circuito abierto, mensajes en buffer
docker compose start timescaledb
# el buffer drena: comparar contador del simulador contra filas persistidas
```

## 6. Saga de borrado

```bash
curl -X DELETE http://localhost:8082/api/v1/vehicles/VH-001 -H "Authorization: Bearer $TOKEN"
docker compose exec redis redis-cli KEYS 'vehicle:VH-001*'   # vacío
```

## 7. Dashboard y PWA

Con las herramientas del navegador: marcadores moviéndose en el mapa, estados cambiando en la tabla,
y `/driver` en modo offline acumulando en IndexedDB y drenando en un solo lote al reconectar.

## Reportar

Di qué comprobaste y **pega la salida real**. Si algo falla, dilo con el error a la vista; no lo
describas como si hubiera pasado.
