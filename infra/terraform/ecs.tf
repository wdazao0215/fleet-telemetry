resource "aws_ecs_cluster" "main" {
  name = local.name

  setting {
    name  = "containerInsights"
    value = var.environment == "production" ? "enabled" : "disabled"
  }
}

resource "aws_cloudwatch_log_group" "services" {
  for_each = merge(local.services, { migrator = { port = 0, public = false } })

  name = "/ecs/${local.name}/${each.key}"
  # Los logs de telemetría son voluminosos y su valor decae rápido; 30 días cubren la investigación
  # de un incidente sin acumular coste indefinido.
  retention_in_days = var.environment == "production" ? 30 : 7
}

locals {
  # Todas las tareas comparten la misma configuración sensible, resuelta desde Secrets Manager por el
  # rol de ejecución. La sintaxis "arn:json-key::" extrae un campo concreto del secreto.
  common_secrets = [
    for key in [
      "ConnectionStrings__FleetDb",
      "Redis__ConnectionString",
      "RabbitMq__Host",
      "RabbitMq__Username",
      "RabbitMq__Password",
      "Jwt__SigningKey",
      "Ingestion__ApiKey",
    ] : {
      name      = key
      valueFrom = "${aws_secretsmanager_secret.app.arn}:${key}::"
    }
  ]

  desired_counts = {
    ingestion-api     = var.ingestion_desired_count
    query-api         = var.query_desired_count
    processing-worker = var.worker_desired_count
    web               = var.web_desired_count
  }
}

resource "aws_ecs_task_definition" "services" {
  for_each = local.services

  family                   = "fleet-${each.key}"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"

  cpu    = each.key == "web" ? 512 : 1024
  memory = each.key == "web" ? 1024 : 2048

  # ARM64 sobre Graviton: en torno a un 20% más barato por vCPU y sin ninguna dependencia nativa que
  # lo impida en estos servicios. El pipeline publica imágenes multi-arquitectura.
  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }

  execution_role_arn = aws_iam_role.execution.arn
  task_role_arn      = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = each.key
      image     = local.container_images[each.key]
      essential = true

      portMappings = each.value.port > 0 ? [
        { containerPort = each.value.port, protocol = "tcp" }
      ] : []

      environment = [
        { name = "ASPNETCORE_ENVIRONMENT", value = title(var.environment) },
        { name = "DOTNET_ENVIRONMENT", value = title(var.environment) },
        { name = "Cors__AllowedOrigins__0", value = "https://${var.dashboard_host}" },
      ]

      secrets = each.key == "web" ? [] : local.common_secrets

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.services[each.key].name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "ecs"
        }
      }

      # El worker no expone puerto, así que su salud se comprueba con el propio proceso: si el
      # contenedor muere, ECS lo repone. Los servicios HTTP usan el health check del target group.
      healthCheck = each.value.port > 0 ? {
        command     = ["CMD-SHELL", "curl -f http://localhost:${each.value.port}${each.key == "web" ? "/" : "/health"} || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      } : null
    }
  ])
}

resource "aws_ecs_service" "services" {
  for_each = local.services

  name            = "fleet-${each.key}"
  cluster         = aws_ecs_cluster.main.id
  task_definition = aws_ecs_task_definition.services[each.key].arn
  desired_count   = local.desired_counts[each.key]
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = module.vpc.private_subnets
    security_groups  = [aws_security_group.app.id]
    assign_public_ip = false
  }

  dynamic "load_balancer" {
    for_each = each.value.public ? [1] : []

    content {
      target_group_arn = aws_lb_target_group.services[each.key].arn
      container_name   = each.key
      container_port   = each.value.port
    }
  }

  # Si el despliegue no estabiliza, ECS revierte solo a la versión anterior. Sin esto, un contenedor
  # que no arranca deja el servicio degradado hasta que alguien lo mira.
  deployment_circuit_breaker {
    enable   = true
    rollback = true
  }

  # 200/100 permite levantar la nueva versión completa antes de retirar la anterior: despliegue sin
  # pérdida de capacidad, a costa de duplicar recursos durante unos minutos.
  deployment_maximum_percent         = 200
  deployment_minimum_healthy_percent = 100

  # Da margen a que la aplicación arranque y pase el health check antes de contar fallos.
  health_check_grace_period_seconds = each.value.public ? 90 : 0

  lifecycle {
    # El pipeline despliega con force-new-deployment y actualiza la revisión de la tarea; sin esto,
    # el siguiente terraform apply revertiría la imagen a la del último plan.
    ignore_changes = [task_definition, desired_count]
  }
}

# El migrador no es un servicio: se ejecuta como tarea puntual desde el pipeline antes de desplegar.
resource "aws_ecs_task_definition" "migrator" {
  family                   = "fleet-migrator"
  requires_compatibilities = ["FARGATE"]
  network_mode             = "awsvpc"
  cpu                      = 512
  memory                   = 1024

  runtime_platform {
    cpu_architecture        = "ARM64"
    operating_system_family = "LINUX"
  }

  execution_role_arn = aws_iam_role.execution.arn
  task_role_arn      = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "migrator"
      image     = "${aws_ecr_repository.migrator.repository_url}:${var.image_tag}"
      essential = true

      secrets = [
        {
          name      = "ConnectionStrings__FleetDb"
          valueFrom = "${aws_secretsmanager_secret.app.arn}:ConnectionStrings__FleetDb::"
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.services["migrator"].name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "ecs"
        }
      }
    }
  ])
}
