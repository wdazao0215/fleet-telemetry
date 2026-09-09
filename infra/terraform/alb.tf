resource "aws_lb" "main" {
  name               = substr(local.name, 0, 32)
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = module.vpc.public_subnets

  # Solo en producción: en staging impide destruir el entorno con terraform destroy.
  enable_deletion_protection = var.environment == "production"

  # 65 s en lugar de los 60 por defecto. Debe ser mayor que el keep-alive de Kestrel (por defecto
  # 130 s no aplica aquí) para que sea el balanceador quien cierre las conexiones ociosas y no
  # aparezcan 502 esporádicos por una carrera de cierre. Además cubre las conexiones WebSocket de
  # SignalR entre latidos.
  idle_timeout = 65
}

resource "aws_lb_target_group" "services" {
  for_each = { for name, config in local.services : name => config if config.public }

  name        = substr("${local.name}-${each.key}", 0, 32)
  port        = each.value.port
  protocol    = "HTTP"
  vpc_id      = module.vpc.vpc_id
  target_type = "ip"

  health_check {
    path                = each.key == "web" ? "/" : "/health"
    interval            = 15
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 3
    matcher             = "200"
  }

  # Margen para que las peticiones en curso terminen antes de retirar la tarea. Por debajo de esto,
  # un despliegue corta respuestas a medio enviar.
  deregistration_delay = 30

  stickiness {
    # SignalR con WebSockets no necesita afinidad porque la conexión es persistente, pero si el
    # transporte degrada a long polling, las peticiones del mismo cliente deben ir a la misma tarea.
    type            = "lb_cookie"
    enabled         = each.key == "query-api"
    cookie_duration = 3600
  }
}

resource "aws_lb_listener" "https" {
  load_balancer_arn = aws_lb.main.arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS13-1-2-2021-06"
  certificate_arn   = var.certificate_arn

  # Por defecto va al dashboard; las APIs se enrutan por host más abajo.
  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.services["web"].arn
  }
}

resource "aws_lb_listener" "http_redirect" {
  load_balancer_arn = aws_lb.main.arn
  port              = 80
  protocol          = "HTTP"

  # Nunca se sirve por HTTP: la API key de los dispositivos y el JWT viajarían en claro.
  default_action {
    type = "redirect"

    redirect {
      port        = "443"
      protocol    = "HTTPS"
      status_code = "HTTP_301"
    }
  }
}

resource "aws_lb_listener_rule" "ingestion" {
  listener_arn = aws_lb_listener.https.arn
  priority     = 10

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.services["ingestion-api"].arn
  }

  condition {
    host_header {
      values = [var.ingestion_host]
    }
  }
}

resource "aws_lb_listener_rule" "query" {
  listener_arn = aws_lb_listener.https.arn
  priority     = 20

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.services["query-api"].arn
  }

  condition {
    host_header {
      values = [var.query_host]
    }
  }
}
