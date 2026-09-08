# CLAUDE.md — Guía del proyecto Fleet Telemetry

Sistema de monitoreo y telemetría de flotas con GPS. Ingesta coordenadas de múltiples vehículos,
detecta anomalías, y las muestra en un dashboard web en tiempo real.

## Idioma

- **Código, identificadores, commits, PRs y comentarios en el código: inglés.**
- **Documentación (README, ADRs, este archivo): español.**

## Stack

| Capa | Tecnología |
|---|---|
| Backend | .NET 10, ASP.NET Core Minimal APIs, Worker Services |
| Persistencia histórica | PostgreSQL 17 + TimescaleDB (hypertable) |
| Caché / deduplicación | Redis 7 |
| Mensajería | RabbitMQ (detrás del puerto `IEventBus`) |
| Resiliencia | Polly v8 (`ResiliencePipeline`) |
| Frontend | Next.js 16, React 19, TypeScript estricto, Tailwind |
| Mapa | Leaflet + react-leaflet 5 (OpenStreetMap) |
| Tiempo real | SignalR con degradación a polling |
| Tests | xUnit + NSubstitute + Testcontainers; Vitest + Testing Library |
| Infra | Docker Compose, GitHub Actions, Terraform (ECS Fargate) |

## Arquitectura

Tres deployables sobre **un único bounded context** (`Fleet Telemetry`). La separación es por perfil
de escalado — la ingesta escribe, la consulta lee — no por dominio. Ver `docs/adr/0001`.

```
simulator ──► Ingestion.Api ──► RabbitMQ ──► Processing.Worker ──► TimescaleDB
                   │                              │                    ▲
              Redis (dedupe)                 Redis (estado)            │
                                                  │                    │
              Next.js ◄── SignalR ◄── Query.Api ──┴────────────────────┘
```

### Regla de dependencias (obligatoria)

```
Domain  ←  Application  ←  Infrastructure  ←  Services
```

- `Domain` **no referencia nada**: ni EF Core, ni ASP.NET, ni Redis. Se testea sin base de datos.
- `Application` solo referencia `Domain`. Define **puertos** (`IPositionRepository`, `IPositionCache`,
  `IEventBus`); nunca los implementa.
- `Infrastructure` implementa los puertos. **`DbContext`, `IConnectionMultiplexer` y el cliente de
  RabbitMQ no salen de aquí.**
- `Services` son hosts delgados: composición de DI, endpoints, configuración.

Esta regla está verificada por `FleetTelemetry.Architecture.Tests` con NetArchTest. Si la violas, el
build falla. No es una convención: es una restricción ejecutable.

## Estructura

```
backend/
  src/BuildingBlocks/{Domain,Application,Infrastructure,Contracts}
  src/Services/{Ingestion.Api,Processing.Worker,Query.Api,Migrator}
  src/Tools/Simulator
  tests/{Domain.Tests,Application.Tests,Architecture.Tests,Integration.Tests}
frontend/src/features/<feature>/{domain,api,hooks,ui}
infra/terraform
docs/adr
```

## Comandos

```bash
docker compose up --build        # entorno completo (requisito del enunciado: un solo comando)
docker compose logs -f ingestion-api
dotnet build backend/FleetTelemetry.sln
dotnet test  backend/FleetTelemetry.sln
cd frontend && npm run dev && npm test
```

## Convenciones

**Commits:** Conventional Commits en inglés (`feat:`, `fix:`, `refactor:`, `test:`, `docs:`,
`chore:`). Un commit describe *por qué* cambia algo, no solo qué archivo se tocó.

**Ramas:** una rama por feature (`feat/<slug>`), un PR por rama, merge a `main`. Nunca commits
directos a `main`.

**Comentarios:** explican la **razón** de una decisión, nunca lo que hace la línea siguiente.

```csharp
// Redondeamos a 5 decimales (~1.1 m) porque el jitter del GPS en reposo produce
// coordenadas distintas para un vehículo que no se ha movido.
```

**Tests:** toda regla de negocio crítica lleva test (deduplicación, cálculo de distancia, detección de
vehículo detenido, apertura del circuit breaker, cola offline del PWA). No se escriben tests
decorativos de getters.

**Errores:** `ProblemDetails` (RFC 7807) en todas las respuestas de error de las APIs.

## Reglas detalladas

- `.claude/rules/architecture.md` — capas, puertos y adaptadores
- `.claude/rules/testing.md` — qué se testea y qué no
- `.claude/rules/frontend.md` — convenciones de Next.js y TypeScript

## Skills

- `dotnet-vertical-slice` — caso de uso CQRS completo respetando las capas
- `next-feature-module` — feature del frontend con sus capas
- `feature-branch-flow` — rama, commits, PR, merge
- `adr-writer` — registrar una decisión arquitectónica
- `telemetry-verify` — verificar el sistema end-to-end ejecutándolo de verdad

## No hacer

- No poner lógica de negocio en los endpoints ni en los componentes de React.
- No usar `any` en TypeScript ni `dynamic` en C#.
- No hacer `fetch` dentro de un componente: va en `features/<x>/api` y se consume por hook.
- No dar por terminada una feature sin haberla ejecutado. Levantar el entorno y comprobarlo.
