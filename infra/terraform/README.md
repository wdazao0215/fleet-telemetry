# Infraestructura en AWS

Terraform que describe el despliegue de Fleet Telemetry en ECS Fargate.

> **Este código no se aplica en esta entrega.** El pipeline lo valida (`fmt`, `validate`, `plan`),
> pero `apply` y `destroy` están explícitamente denegados en la configuración del proyecto. Sin una
> cuenta de AWS, aplicarlo no aportaría nada verificable y sí un riesgo de coste real.

## Qué levanta

```
Internet
   │
   ▼
  ALB (HTTPS, redirección desde HTTP)
   ├── ingest.fleet…  → ingestion-api   (Fargate, 2 tareas)
   ├── api.fleet…     → query-api       (Fargate, 2 tareas, sticky para long polling)
   └── fleet…         → web             (Fargate, 2 tareas)
                          │
        subredes privadas │
                          ▼
        processing-worker (Fargate, sin puerto expuesto)
                          │
   ┌──────────────────────┼───────────────────────┐
   ▼                      ▼                       ▼
 RDS PostgreSQL   ElastiCache Redis        Amazon MQ (RabbitMQ)
```

Además: cinco repositorios de ECR con escaneo y etiquetas inmutables, Secrets Manager para toda la
configuración sensible, y grupos de CloudWatch Logs por servicio.

## Uso

```bash
terraform init -backend=false     # validación local, sin estado remoto
terraform validate
terraform plan -var-file=terraform.tfvars
```

Para un despliegue real haría falta, además: crear el bucket de estado con bloqueo, el rol de OIDC
para GitHub Actions, y poner la variable de repositorio `AWS_ENABLED` a `true`.

## Decisiones que conviene mirar

**ARM64 sobre Graviton.** Alrededor de un 20% más barato por vCPU y ninguna dependencia nativa lo
impide. El pipeline publica imágenes multi-arquitectura.

**Rol de ejecución y rol de tarea separados.** El primero resuelve secretos *antes* de arrancar el
contenedor; el segundo es lo que tiene la aplicación en marcha, y hoy está deliberadamente vacío
porque la app no llama a ninguna API de AWS.

**Circuit breaker de despliegue con rollback.** Si la versión nueva no estabiliza, ECS revierte sola
en lugar de dejar el servicio degradado hasta que alguien lo mire.

**Etiquetas de imagen inmutables.** Sin esto, dos despliegues pueden acabar con el mismo tag
apuntando a imágenes distintas y reproducir un incidente se vuelve imposible.

**TimescaleDB no está disponible en RDS gestionado.** Es la limitación más importante de este
diseño y está anotada en `data-stores.tf` y en el [ADR-0004](../../docs/adr/0004-timescaledb-para-el-historico.md).
Las salidas reales son Timescale Cloud, Postgres autogestionado, o el particionado declarativo
nativo como fallback.
