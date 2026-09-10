# Checklist de pruebas manuales

Recorrido completo del sistema, en el orden en que conviene hacerlo. Cada paso trae el comando y el
**resultado esperado**, tomado de una ejecución real.

Dos terminales y un navegador. Los comandos asumen que estás en la raíz del repositorio.

---

## 0 · Preparación

- [ ] **Docker en marcha**

  ```bash
  docker info --format 'Server {{.ServerVersion}}'
  ```
  Si falla: abre Docker Desktop y espera. Si devuelve error 500, *Troubleshoot → Restart*.

- [ ] **Entorno desde cero** (opcional, pero es lo que hará quien evalúe)

  ```bash
  docker compose down -v
  ```

- [ ] **Levantar todo con un solo comando**

  ```bash
  docker compose up --build
  ```
  Déjalo en primera plano en una terminal para ver los logs. La primera vez tarda ~3 min.

- [ ] **Los 8 servicios arriba** (en la otra terminal)

  ```bash
  docker compose ps --format '{{.Service}}\t{{.Status}}'
  ```
  → 6 en `healthy`; `processing-worker` y `simulator` en `Up` (no exponen puerto, no tienen healthcheck).

- [ ] **El migrador aplicó el esquema y terminó**

  ```bash
  docker compose logs migrator | tail -3
  ```
  → `Migración 001_initial_schema aplicada` · `002_retention_policy` · `Esquema al día.`

- [ ] **`positions` es realmente una hypertable**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -c "SELECT hypertable_name FROM timescaledb_information.hypertables;"
  ```
  → una fila: `positions`

---

## 1 · Ingesta y validación

Prepara las variables en tu terminal:

```bash
API=http://localhost:8081/api/v1/telemetry; KEY="X-Api-Key: dev-fleet-key"; ts() { python3 -c "import datetime;print(datetime.datetime.now(datetime.timezone.utc).isoformat().replace('+00:00','Z'))"; }
```

- [ ] **Lectura válida → `202`**

  ```bash
  curl -s -w "\nHTTP %{http_code}\n" -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-MANUAL\",\"latitude\":4.7110,\"longitude\":-74.0721,\"timestamp\":\"$(ts)\"}"
  ```
  → `{"vehicleId":"VH-MANUAL","duplicate":false,...}` y `HTTP 202`

- [ ] **Latitud fuera de rango → `400` con ProblemDetails**

  ```bash
  curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-MANUAL\",\"latitude\":999,\"longitude\":-74,\"timestamp\":\"$(ts)\"}"
  ```
  → `"code":"telemetry.latitude_out_of_range"` y detalle en español

- [ ] **Campo ausente → `400`** (comprueba que un `latitude` omitido no se toma como 0)

  ```bash
  curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-MANUAL\",\"longitude\":-74,\"timestamp\":\"$(ts)\"}"
  ```
  → `"code":"telemetry.missing_fields"`

- [ ] **Sin API key → `401`**

  ```bash
  curl -s -o /dev/null -w "HTTP %{http_code}\n" -X POST $API -H 'Content-Type: application/json' -d '{"vehicleId":"VH-MANUAL","latitude":4.7,"longitude":-74,"timestamp":"2026-01-01T10:00:00Z"}'
  ```
  → `HTTP 401`

- [ ] **Timestamp en el futuro → `400`**

  ```bash
  curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-MANUAL\",\"latitude\":4.71,\"longitude\":-74.07,\"timestamp\":\"2030-01-01T10:00:00Z\"}"
  ```
  → `"code":"telemetry.timestamp_in_future"`

---

## 2 · Deduplicación (el punto clave)

- [ ] **El mismo paquete reenviado → `200` con `duplicate: true`**

  ```bash
  B="{\"vehicleId\":\"VH-DUP\",\"latitude\":4.7110,\"longitude\":-74.0721,\"timestamp\":\"$(ts)\"}"
  curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "$B" | python3 -m json.tool
  curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "$B" | python3 -m json.tool
  ```
  → el primero `"duplicate": false`, el segundo `"duplicate": true`

- [ ] **Solo se guardó una posición**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -t -c "SELECT count(*) FROM positions WHERE vehicle_id='VH-DUP';"
  ```
  → `1`

- [ ] **Misma coordenada, distinto timestamp → NO es duplicado** ⭐

  Es lo que permite que la alerta de vehículo detenido exista. Un vehículo parado sigue emitiendo, y
  esas lecturas deben pasar.

  ```bash
  curl -s -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-DUP\",\"latitude\":4.7110,\"longitude\":-74.0721,\"timestamp\":\"$(ts)\"}" | python3 -c "import sys,json;print('duplicate:', json.load(sys.stdin)['duplicate'])"
  ```
  → `duplicate: False`

---

## 3 · Alerta de vehículo detenido

El simulador mantiene `VH-001` estacionario a propósito.

- [ ] **Espera ~90 s desde el arranque y comprueba la alerta**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -c "SELECT vehicle_id, kind, detail FROM alerts ORDER BY raised_at DESC LIMIT 3;"
  ```
  → `VH-001 | 0 | Vehículo detenido durante NN segundos.`

- [ ] **En los logs del worker**

  ```bash
  docker compose logs processing-worker | grep -i alerta | tail -3
  ```
  → `Alerta de vehículo detenido levantada para VH-001.`

- [ ] **El cooldown evita alertas repetidas** — espera 1 minuto más y vuelve a contar:

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -t -c "SELECT count(*) FROM alerts WHERE vehicle_id='VH-001';"
  ```
  → sigue siendo un número bajo (1 por cada 5 min), **no** una alerta cada 3 segundos

- [ ] **…pero las posiciones sí se siguen guardando**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -t -c "SELECT count(*) FROM positions WHERE vehicle_id='VH-001';"
  ```
  → crece constantemente

---

## 4 · Circuit Breaker ⭐

La demostración con más impacto. Hazla tal cual.

- [ ] **Estado inicial sano**

  ```bash
  curl -s http://localhost:8081/health/resilience | python3 -m json.tool
  ```
  → `"circuitState": "Closed"`, `"bufferedMessages": 0`

- [ ] **Apaga el broker**

  ```bash
  docker compose stop rabbitmq
  ```

- [ ] **Envía 10 lecturas: todas deben responder `202`**

  ```bash
  for i in $(seq 1 10); do curl -s -o /dev/null -w "lectura $i -> %{http_code}\n" -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-CB\",\"latitude\":4.7$i,\"longitude\":-74.07,\"timestamp\":\"$(ts)\"}"; done
  ```
  → **10 de 10 en `202`**. La ingesta no cae.

- [ ] **El circuito se abrió y los mensajes están en el buffer**

  ```bash
  curl -s http://localhost:8081/health/resilience | python3 -m json.tool
  ```
  → `"circuitState": "Open"`, `"droppedMessages": 0` y `bufferedMessages` **mayor que 10**: el
  simulador sigue emitiendo mientras el broker está caído, así que sus lecturas también se acumulan.
  Lo que importa es que nada se descarta.

- [ ] **Levanta el broker y observa el drenaje**

  ```bash
  docker compose start rabbitmq
  ```
  ```bash
  docker compose logs -f ingestion-api | grep -i drenad
  ```
  → `Drenados N mensajes del buffer local; quedan 0` (tarda hasta ~30 s). Puede aparecer en varias
  tandas —`Drenados 8`, luego `Drenados 1`— porque el drenaje va por lotes. `Ctrl+C` para salir.

- [ ] **Cero pérdidas: las 10 llegaron a la base**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -t -c "SELECT count(*) FROM positions WHERE vehicle_id='VH-CB';"
  ```
  → `10`

- [ ] **El circuito se cierra solo con el siguiente tráfico**

  ```bash
  curl -s -o /dev/null -X POST $API -H "$KEY" -H 'Content-Type: application/json' -d "{\"vehicleId\":\"VH-CB\",\"latitude\":4.75,\"longitude\":-74.07,\"timestamp\":\"$(ts)\"}"; sleep 2; curl -s http://localhost:8081/health/resilience | python3 -m json.tool
  ```
  → `"circuitState": "Closed"`

---

## 5 · Dashboard

Abre **http://localhost:3000** en Chrome.

- [ ] **Login** con `operator` / `fleet-dev`
- [ ] **Sin credenciales correctas** → mensaje de error, no entra
- [ ] **Cabecera** muestra el número de vehículos y cuántos en alerta
- [ ] **Indicador dice `En vivo`** (verde) → SignalR conectado
- [ ] **Los marcadores se mueven solos**, sin recargar la página
- [ ] **La tabla ordena por prioridad**: Alerta primero, luego Detenido, En movimiento y Sin señal
- [ ] **Los estados llevan color y texto** (no solo color)
- [ ] **`VH-001` aparece en rojo** con la columna "Parado desde" contando
- [ ] **El panel de alertas** muestra "Vehículo detenido durante NN segundos"
- [ ] **Clic en un vehículo de la tabla** → el mapa centra en él y dibuja su recorrido en azul
- [ ] **Consola del navegador sin errores** (F12 → Console)

### Degradación a polling

- [ ] **Apaga la API de consulta**

  ```bash
  docker compose stop query-api
  ```
  → en ~10 s el indicador pasa a **`Polling`** (ámbar) y aparece "Sin conexión con el servidor"

- [ ] **Vuelve a levantarla**

  ```bash
  docker compose start query-api
  ```
  → el indicador vuelve solo a **`En vivo`**, sin recargar la página

- [ ] **Caso extremo**: recarga la página **con `query-api` apagada** → arranca en `Polling` con 0
      vehículos y se recupera sola al levantarla

---

## 6 · PWA del conductor

Abre **http://localhost:3000/driver** en Chrome (⌘⇧M para vista móvil).

- [ ] **Service worker activo**: F12 → *Application* → *Service Workers* → `activated and is running`
- [ ] **Instalable**: icono ⊕ en la barra de direcciones → *Instalar*
- [ ] **El viaje avanza**: "Viaje actual" en metros y el contador de lecturas sube
- [ ] **Estado `Conectado`** y "Sin enviar: 0"

### Ciclo offline ⭐

- [ ] **Activa "Simular pérdida de cobertura"** → estado pasa a `Sin conexión`
- [ ] **Espera ~25 s** → "Sin enviar" sube a 5-6, ninguna lectura se pierde
- [ ] **Compruébalo en IndexedDB**: F12 → *Application* → *IndexedDB* → `fleet-driver` → `readings`
- [ ] **Desactiva el interruptor** → cola vuelve a **0** y en el historial aparece
      "Conexión restablecida. N lecturas sincronizadas."
- [ ] **Llegaron a la base**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -t -c "SELECT count(*) FROM positions WHERE vehicle_id='VH-DRIVER';"
  ```

### Botón de pánico ⭐

- [ ] **Púlsalo** → el botón cambia a "ALERTA ENVIADA" y aparece en el historial local
- [ ] **Llegó al backend**

  ```bash
  docker compose logs processing-worker | grep -i "PÁNICO" | tail -2
  ```
  → `ALERTA DE PÁNICO registrada para VH-DRIVER.`

- [ ] **Y al dashboard**: cambia a la pestaña de `localhost:3000` → la alerta está en el panel

---

## 7 · Saga de eliminación ⭐

- [ ] **Obtén un token**

  ```bash
  TOKEN=$(curl -s -X POST http://localhost:8082/api/v1/auth/token -H 'Content-Type: application/json' -d '{"username":"operator","password":"fleet-dev"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['accessToken'])"); echo "${TOKEN:0:30}..."
  ```

- [ ] **Estado ANTES del borrado**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -c "SELECT (SELECT count(*) FROM vehicles WHERE id='VH-002') AS vehiculo, (SELECT count(*) FROM positions WHERE vehicle_id='VH-002') AS posiciones;"
  ```

- [ ] **Borra el vehículo → `202`, no `204`**

  ```bash
  curl -s -w "\nHTTP %{http_code}\n" -X DELETE http://localhost:8082/api/v1/vehicles/VH-002 -H "Authorization: Bearer $TOKEN"
  ```
  → `{"accepted":true,"state":"PendingDeletion"}`. Es `202` porque la consistencia es eventual.

- [ ] **La saga se completó**

  ```bash
  docker compose logs processing-worker | grep -i saga | tail -2
  ```
  → `Saga de eliminación completada para VH-002: caché e histórico purgados.`

- [ ] **Estado DESPUÉS: histórico a cero y el vehículo como lápida**

  ```bash
  docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -c "SELECT (SELECT state FROM vehicles WHERE id='VH-002') AS estado, (SELECT count(*) FROM positions WHERE vehicle_id='VH-002') AS posiciones, (SELECT count(*) FROM alerts WHERE vehicle_id='VH-002') AS alertas;"
  ```
  → `estado = 3` (Deleted), `posiciones = 0`, `alertas = 0`

  La fila **no se borra a propósito**: queda como lápida. Sin ella, el dispositivo del vehículo
  —que sigue encendido— haría que el alta automática lo recreara como activo en segundos.

- [ ] **Espera 25 s y comprueba que NO resucita** ⭐

  ```bash
  sleep 25 && docker compose exec -T timescaledb psql -U fleet -d fleet_telemetry -t -c "SELECT 'estado='||state||' posiciones='||(SELECT count(*) FROM positions WHERE vehicle_id='VH-002') FROM vehicles WHERE id='VH-002';"
  ```
  → sigue `estado=3` y `posiciones=0`, aunque el simulador lleve 25 s emitiendo para ese vehículo

- [ ] **Redis: fuera del índice de la flota**

  ```bash
  docker compose exec -T redis redis-cli -a fleet_local_dev --no-auth-warning SISMEMBER fleet:vehicles:live VH-002
  ```
  → `0`

  Si listas las claves verás algunas `fleet:dedupe:VH-002:...`. Es correcto: son efímeras, tienen TTL
  de segundos y no se purgan a propósito porque buscarlas exigiría un `SCAN` sobre todo el espacio de
  claves. Compruébalo:
  ```bash
  docker compose exec -T redis redis-cli -a fleet_local_dev --no-auth-warning KEYS 'fleet:*VH-002*' | head -2
  ```

- [ ] **Borrarlo otra vez → `404`**

  ```bash
  curl -s -X DELETE http://localhost:8082/api/v1/vehicles/VH-002 -H "Authorization: Bearer $TOKEN"
  ```
  → `"code":"vehicle.not_found"`

- [ ] **Desapareció del dashboard** (mira la pestaña, sin recargar)

---

## 8 · API de consulta

- [ ] **Sin token → `401`**

  ```bash
  curl -s -o /dev/null -w "HTTP %{http_code}\n" http://localhost:8082/api/v1/vehicles
  ```

- [ ] **Snapshot de la flota**

  ```bash
  curl -s http://localhost:8082/api/v1/vehicles -H "Authorization: Bearer $TOKEN" | python3 -m json.tool | head -20
  ```
  → estados como texto (`"Moving"`, `"Alerted"`), no números

- [ ] **Recorrido de un vehículo**

  ```bash
  curl -s "http://localhost:8082/api/v1/vehicles/VH-003/track" -H "Authorization: Bearer $TOKEN" | python3 -c "import sys,json;d=json.load(sys.stdin);print('puntos:',len(d['points']))"
  ```

- [ ] **El hub exige autenticación**

  ```bash
  curl -s -o /dev/null -w "sin token: HTTP %{http_code}\n" -X POST "http://localhost:8082/hubs/telemetry/negotiate?negotiateVersion=1"
  ```
  → `401`

- [ ] **Documentación OpenAPI** → abre http://localhost:8081/openapi/v1.json y http://localhost:8082/openapi/v1.json

---

## 9 · Simulador

- [ ] **Ratios de inyección de fallos**

  ```bash
  docker compose logs simulator | grep Enviadas | tail -1
  ```
  → `Enviadas N | duplicadas ~10% | malformadas ~5% | rechazadas = malformadas | fallos de red 0`

  El dato que importa: **rechazadas == malformadas**. Ni un solo payload válido rechazado.

- [ ] **Visto desde la ingesta**

  ```bash
  docker compose logs ingestion-api | grep -oE "responded [0-9]+" | sort | uniq -c | sort -rn
  ```
  → mayoría `202`, una parte `200` (duplicados) y una minoría `400` (malformados)

---

## 10 · Calidad de código

- [ ] **Tests unitarios y de arquitectura**

  ```bash
  dotnet test backend/FleetTelemetry.sln --filter "FullyQualifiedName!~Integration"
  ```
  → 80 tests en verde

- [ ] **Tests de integración con Testcontainers** (necesita Docker)

  ```bash
  dotnet test backend/tests/FleetTelemetry.Integration.Tests
  ```
  → 12 en verde, levantando Redis y TimescaleDB reales

- [ ] **Frontend**

  ```bash
  cd frontend && npm run lint && npm test && npm run build && cd ..
  ```
  → lint limpio, 22 tests, build correcto

- [ ] **El dominio no tiene dependencias** — ábrelo y compruébalo:

  ```bash
  cat backend/src/BuildingBlocks/FleetTelemetry.Domain/FleetTelemetry.Domain.csproj
  ```
  → ni un solo `PackageReference`

- [ ] **La regla de capas es ejecutable** — sabotéala a propósito y comprueba que el test la caza.

  No basta con referenciar el paquete: NetArchTest analiza el IL compilado, y una referencia que no
  se usa no genera dependencia. Hay que **usar** el tipo:

  ```bash
  A=backend/src/BuildingBlocks/FleetTelemetry.Application; dotnet add $A package StackExchange.Redis >/dev/null && printf 'using StackExchange.Redis;\n\nnamespace FleetTelemetry.Application;\n\ninternal sealed class LayerViolationProbe(IConnectionMultiplexer c)\n{\n    public IDatabase Database => c.GetDatabase();\n}\n' > $A/LayerViolationProbe.cs && dotnet test backend/tests/FleetTelemetry.Architecture.Tests
  ```
  → **`Con error: 1, Superado: 5`** — el test detecta a Redis infiltrado en la capa de aplicación.

  Deshazlo:
  ```bash
  A=backend/src/BuildingBlocks/FleetTelemetry.Application; rm $A/LayerViolationProbe.cs && dotnet remove $A package StackExchange.Redis >/dev/null && dotnet test backend/tests/FleetTelemetry.Architecture.Tests && git status --porcelain backend
  ```
  → vuelve a `Superado: 6` y el árbol queda limpio

---

## 11 · DevOps

- [ ] **Terraform válido**

  ```bash
  cd infra/terraform && terraform init -backend=false && terraform validate && cd ../..
  ```
  (si no tienes Terraform instalado, esto lo valida el CI en cada PR)

- [ ] **CI en verde en GitHub**

  ```bash
  gh run list --branch main --limit 2
  ```

- [ ] **Historial de commits y PRs**

  ```bash
  git log --oneline | head -20
  gh pr list --state merged --limit 20
  ```

---

## 12 · Documentación

- [ ] El **README** abre con el arranque en un comando
- [ ] El **diagrama Mermaid** se renderiza en GitHub
- [ ] Los **5 ADRs** tienen alternativas descartadas **y consecuencias negativas**
- [ ] La sección de **app móvil** responde offline-first y batería
- [ ] El **Reporte de IA** nombra errores concretos, no generalidades
- [ ] **Desafíos y Soluciones** declara las limitaciones conocidas
- [ ] El **enlace al video** está puesto ← *lo último que queda por hacer*

---

## Al terminar

```bash
docker compose down          # conserva los datos
docker compose down -v       # borra también los volúmenes
```
