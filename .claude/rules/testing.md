# Regla: testing

## Qué se testea siempre

Estas son las reglas de negocio críticas del sistema. Si tocas una y no hay test, el PR no está listo:

1. **Deduplicación** — la misma coordenada dentro de la ventana no se propaga dos veces.
2. **Distancia (Haversine)** — con valores conocidos y verificables a mano.
3. **Detección de vehículo detenido** — dentro del umbral y pasado el minuto → alerta; un metro más
   allá del umbral → no hay alerta. Los bordes importan más que el caso feliz.
4. **Circuit breaker** — cuando la persistencia falla, la ingesta sigue aceptando y encolando.
5. **Saga de borrado** — el vehículo desaparece de caché y de base de datos; si un paso falla, queda
   en un estado observable.
6. **Cola offline del PWA** — encolar sin red y drenar en un solo lote sin duplicados al reconectar.
7. **Specifications de validación** — latitud, longitud, timestamp, identificador.

## Qué NO se testea

Getters, mapeos triviales, configuración de DI, y cualquier test que solo repita la implementación.
Un test que se rompe con cada refactor sin haber detectado un bug es deuda, no cobertura.

## Cómo

- **Unitarios** (`Domain.Tests`, `Application.Tests`): xUnit + NSubstitute para los puertos. Rápidos,
  sin E/S.
- **Arquitectura** (`Architecture.Tests`): NetArchTest verifica la dirección de las dependencias.
- **Integración** (`Integration.Tests`): Testcontainers levanta TimescaleDB, Redis y RabbitMQ reales.
  Nada de mocks de base de datos: el objetivo es cazar justo lo que un mock oculta.
- **Frontend**: Vitest + Testing Library sobre la lógica de estado y la cola offline.

## Nombres

`MethodOrScenario_Condition_ExpectedOutcome`:

```csharp
[Fact]
public void IsStopped_WhenWithinThresholdForOverAMinute_ReturnsTrue()
```

El nombre debe permitir entender el fallo sin abrir el cuerpo del test.

## Verificación manual

Los tests verdes no cierran una feature. Antes de dar algo por terminado hay que **ejecutarlo**:
levantar el compose, hacer el `curl`, abrir el navegador. Usa la skill `telemetry-verify`.
