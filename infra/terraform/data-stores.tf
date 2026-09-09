# Contraseñas generadas por Terraform y guardadas en Secrets Manager. Nunca se escriben en variables
# ni en tfvars: lo que está en un tfvars acaba en un repositorio tarde o temprano.
resource "random_password" "database" {
  length  = 32
  special = true
  # Se excluyen los caracteres que rompen una cadena de conexión de Npgsql al no ir escapados.
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

resource "random_password" "broker" {
  length           = 32
  special          = true
  override_special = "-_"
}

resource "aws_db_subnet_group" "main" {
  name       = local.name
  subnet_ids = module.vpc.private_subnets
}

# PostgreSQL.
#
# NOTA IMPORTANTE sobre TimescaleDB: RDS no ofrece la extensión en su edición gestionada. Las
# opciones reales son (a) Timescale Cloud, (b) Postgres autogestionado en EC2 o EKS, o (c) renunciar
# a la hypertable y usar particionado declarativo nativo, que es el fallback documentado en el
# ADR-0004. Este recurso deja el motor listo para (c) y la decisión anotada donde se ve.
resource "aws_db_instance" "main" {
  identifier     = local.name
  engine         = "postgres"
  engine_version = "17"

  instance_class    = var.database_instance_class
  allocated_storage = var.database_allocated_storage_gb
  # gp3 sobre gp2: mismo precio aproximado con IOPS desacopladas del tamaño del volumen, que importa
  # en una carga de escritura constante como la telemetría.
  storage_type      = "gp3"
  storage_encrypted = true

  db_name  = "fleet_telemetry"
  username = "fleet"
  password = random_password.database.result

  db_subnet_group_name   = aws_db_subnet_group.main.name
  vpc_security_group_ids = [aws_security_group.data.id]
  publicly_accessible    = false

  multi_az                = var.environment == "production"
  backup_retention_period = var.environment == "production" ? 14 : 3
  # Ventana fuera del horario de operación de la flota en Colombia (UTC-5).
  backup_window      = "07:00-08:00"
  maintenance_window = "sun:08:30-sun:09:30"

  # Protección contra borrado solo en producción: en staging estorba al reconstruir el entorno.
  deletion_protection = var.environment == "production"
  skip_final_snapshot = var.environment != "production"
  final_snapshot_identifier = var.environment == "production" ? "${local.name}-final" : null

  performance_insights_enabled = true
  enabled_cloudwatch_logs_exports = ["postgresql"]

  # Las versiones menores traen parches de seguridad; aplicarlas en la ventana de mantenimiento es
  # preferible a acumular deuda hasta un salto grande.
  auto_minor_version_upgrade = true
}

resource "aws_elasticache_subnet_group" "main" {
  name       = local.name
  subnet_ids = module.vpc.private_subnets
}

# Redis. Guarda deduplicación, estado caliente y cooldowns de alerta: todo reconstruible, así que la
# persistencia no es crítica, pero la disponibilidad sí — sin caché la ingesta no puede deduplicar.
resource "aws_elasticache_replication_group" "main" {
  replication_group_id = local.name
  description          = "Estado caliente de la flota"

  engine         = "redis"
  engine_version = "7.1"
  node_type      = var.cache_node_type
  port           = 6379

  # Réplica y failover automático solo en producción: en staging duplicaría el coste para proteger
  # datos que se regeneran solos en segundos.
  num_cache_clusters         = var.environment == "production" ? 2 : 1
  automatic_failover_enabled = var.environment == "production"
  multi_az_enabled           = var.environment == "production"

  subnet_group_name  = aws_elasticache_subnet_group.main.name
  security_group_ids = [aws_security_group.data.id]

  at_rest_encryption_enabled = true
  transit_encryption_enabled = true

  # Las claves de telemetría ya llevan TTL propio; esta política solo cubre el caso de que la memoria
  # se llene antes de que expiren, y descarta lo menos usado en vez de rechazar escrituras.
  parameter_group_name = "default.redis7"

  snapshot_retention_limit = var.environment == "production" ? 3 : 0
  apply_immediately        = var.environment != "production"
}

# Amazon MQ con RabbitMQ. Se elige sobre SQS para no cambiar de tecnología entre local y AWS: el
# adaptador de IEventBus es el mismo, y una prueba de carga en staging mide el mismo comportamiento
# que habrá en producción. La alternativa SQS está descrita en el ADR-0003.
resource "aws_mq_broker" "main" {
  broker_name = local.name

  engine_type        = "RabbitMQ"
  engine_version     = "3.13"
  host_instance_type = var.broker_instance_type

  # CLUSTER_MULTI_AZ exige al menos mq.m5.large; en staging no compensa.
  deployment_mode = var.environment == "production" ? "CLUSTER_MULTI_AZ" : "SINGLE_INSTANCE"

  subnet_ids      = var.environment == "production" ? module.vpc.private_subnets : [module.vpc.private_subnets[0]]
  security_groups = [aws_security_group.data.id]

  publicly_accessible = false

  user {
    username = "fleet"
    password = random_password.broker.result
  }

  logs {
    general = true
  }

  maintenance_window_start_time {
    day_of_week = "SUNDAY"
    time_of_day = "09:00"
    time_zone   = "UTC"
  }
}
