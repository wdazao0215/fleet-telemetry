# Guion del video de sustentación

Objetivo: 8 minutos (el máximo son 10). Subir a YouTube **como "No listado"** y enlazar en el README.

## Antes de grabar

```bash
docker compose down -v          # entorno limpio, para que la demo empiece de cero
docker compose up --build       # esperar a que los 8 servicios estén healthy
```

Dejar el simulador corriendo **al menos 2 minutos** antes de empezar: hace falta que el vehículo
estacionario haya generado ya su alerta y que haya recorridos dibujables en el mapa.

Ventanas a tener abiertas y listas para alternar:

1. Navegador con `localhost:3000` (dashboard, ya logueado)
2. Segunda pestaña con `localhost:3000/driver` en vista móvil (DevTools → *device toolbar*)
3. Terminal con el repositorio
4. El diagrama del README a mano

---

## 0:00 — 0:45 · Qué es y qué decisiones se tomaron

Abrir con el dashboard funcionando, no con código.

> "Sistema de telemetría de flotas. Tres microservicios en .NET 10, Next.js en el frontend,
> TimescaleDB, Redis y RabbitMQ. Todo levanta con `docker compose up`."

Señalar en pantalla: vehículos moviéndose, estados cambiando, el indicador "En vivo".

> "La separación en tres servicios es por perfil de escalado, no por dominio: la ingesta escribe
> miles de veces por segundo, la consulta mantiene WebSockets abiertos. Comparten un único bounded
> context y eso está declarado explícitamente en el ADR-0001, porque partir el dominio daría el coste
> de los microservicios sin su beneficio."

## 0:45 — 2:15 · El hallazgo: deduplicación contra detección

**Este es el punto más fuerte de la entrega. Dedicarle tiempo.**

> "El enunciado pide dos cosas que, leídas literalmente, se contradicen: ignorar coordenadas
> idénticas en una ventana de tiempo, y alertar cuando un vehículo envía la misma coordenada durante
> más de un minuto."

Dibujar o señalar el flujo:

> "Si la clave de deduplicación fuera vehículo más coordenada, un vehículo parado —que emite la misma
> coordenada cada 2 o 3 segundos— vería descartadas todas sus lecturas menos la primera. El worker
> dejaría de recibir eventos suyos y la alerta de vehículo detenido no se generaría **nunca**. La
> deduplicación desactivaría justo lo que se pide."

> "La clave incluye el timestamp de la lectura. Así se descarta el reenvío real del mismo paquete
> —que es lo que inyecta el simulador con su 10%— y pasa la emisión legítima del vehículo parado."

Mostrar `RedisKeys.Deduplication` y el comentario que lo explica.

## 2:15 — 3:30 · Circuit Breaker en vivo

La demostración con más impacto. Hacerla de verdad, no contarla.

```bash
docker compose stop rabbitmq
```

Enviar lecturas y mostrar que siguen respondiendo `202`:

```bash
curl -s http://localhost:8081/health/resilience
# {"circuitState":"Open","publisherHealthy":false,"bufferedMessages":10,"droppedMessages":0}
```

> "El broker está apagado y la ingesta sigue aceptando. El circuito está abierto y los mensajes están
> en el buffer local. Cero descartados."

> "El breaker no es decoración: sin él cada petición esperaría el timeout completo del broker y la
> ingesta moriría por agotamiento de hilos, aunque formalmente no se hubiera caído."

```bash
docker compose start rabbitmq
```

> "Drenados 10 mensajes, cero pérdidas. Y el buffer está en memoria a propósito, no en la base de
> datos: la base puede ser justamente lo que está caído, y un fallback que depende del componente que
> falló no es un fallback. Esa contrapartida está escrita en el ADR."

## 3:30 — 4:30 · Alerta de vehículo detenido y tiempo real

Señalar en el dashboard el vehículo en rojo y el panel de alertas.

> "Este vehículo lleva parado más de un minuto. La alerta llegó por SignalR, sin recargar la página."

> "El detector es una función pura: no depende de Redis ni del reloj del sistema, así que la regla
> más importante del enunciado se testea en milisegundos y sobre sus bordes exactos."

> "El ancla es la última posición con **movimiento confirmado**, no la última recibida. Anclando en
> la última recibida, el cronómetro se reiniciaría en cada lectura y la alerta no llegaría nunca."

Mencionar de pasada el cooldown: sin él, un camión aparcado genera cientos de alertas idénticas.

## 4:30 — 5:30 · La PWA del conductor y el offline

Cambiar a la vista móvil.

> "El cliente del conductor: telemetría, botón de pánico e historial local."

Activar "Simular pérdida de cobertura" y esperar ~20 segundos.

> "Sin cobertura. Las lecturas se acumulan en IndexedDB, ninguna se pierde. La lectura se escribe
> **siempre** primero en local y solo después se intenta enviar: la red es un detalle de entrega, no
> la fuente de verdad."

Desactivar el interruptor.

> "Se sincronizan en un lote, más antiguas primero. En lotes de 50, porque enviar 200 peticiones de
> golpe por cada conductor que sale de un túnel es exactamente cómo se tumba un servidor cuando vuelve
> la red."

Pulsar el botón de pánico y **cambiar al dashboard** para mostrar que la alerta llegó.

## 5:30 — 6:30 · Saga de eliminación

```bash
curl -X DELETE http://localhost:8082/api/v1/vehicles/VH-001 -H "Authorization: Bearer $TOKEN"
```

> "Responde 202, no 204. El borrado se aceptó pero la consistencia es eventual: un 204 daría a
> entender que ya no existe en ningún sitio."

Mostrar que desaparece del dashboard y luego la comprobación en base de datos y Redis.

> "Tres órdenes son deliberados y cada uno tiene su test: guardar antes de publicar, bloquear la
> telemetría antes de purgar, y purgar la caché antes que el histórico, porque la caché es lo que lee
> el dashboard."

> "Y si un paso falla, el vehículo **no** vuelve a Active: queda en DeletionFailed con el motivo. Un
> borrado a medias es un hecho que hay que ver, no que ocultar."

## 6:30 — 7:15 · Calidad: la regla de capas es ejecutable

```bash
cat backend/src/BuildingBlocks/FleetTelemetry.Domain/FleetTelemetry.Domain.csproj
```

> "El dominio no tiene un solo PackageReference. Y no es una aspiración: hay tests de arquitectura
> que rompen el build si alguien mete EF Core o Redis en el dominio o en la capa de aplicación."

> "Los validé invirtiendo temporalmente una regla para comprobar que fallan de verdad, en lugar de
> confiar en una barra verde."

```bash
dotnet test backend/FleetTelemetry.sln --filter "FullyQualifiedName!~Integration"
```

> "102 tests entre backend y frontend. Y el simulador lleva horas corriendo: 27.000 lecturas, de las
> cuales 1.255 malformadas y exactamente 1.255 rechazadas. Ni un falso positivo."

## 7:15 — 8:00 · Uso de IA y cierre honesto

> "Usé Claude Code como par de programación. Lo que más valor aportó fue el andamiaje, el simulador
> y los tests de bordes."

> "Y falló en cosas concretas, todas detectadas **ejecutando**, no leyendo: propuso unas teselas de
> mapa que hoy exigen API key; escribió un patrón de React que la versión 19 marca como error; faltaba
> CORS en la ingesta, y la PWA parecía estar sin cobertura con el servidor al lado. Y el contenedor
> del dashboard reportaba unhealthy sirviendo tráfico perfectamente, porque Next hace bind a la
> variable HOSTNAME y Docker la define con el id del contenedor."

> "Lo que sé que falta: no hay tests de integración con Testcontainers, el buffer de respaldo se
> pierde si el proceso muere, y un vehículo puede quedarse en PendingDeletion si el worker está caído
> justo después. Está todo escrito en la sección de Desafíos y Soluciones del README."

Cerrar con el dashboard en pantalla.

---

## Consejos

- **Nada de leer código línea a línea.** Mostrar el sistema funcionando y explicar el porqué.
- Los momentos que más puntúan son el hallazgo de la deduplicación y el circuit breaker en vivo. Si
  hay que recortar, recortar de otro sitio.
- Decir en voz alta las limitaciones. Un candidato que conoce los límites de su propia solución
  transmite más solidez que uno que presenta todo como resuelto.
- Grabar en 1080p y comprobar que el texto de la terminal se lee.
