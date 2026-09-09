resource "random_password" "jwt_signing_key" {
  length  = 64
  special = false
}

resource "random_password" "ingestion_api_key" {
  length  = 40
  special = false
}

# Un único secreto con todo lo sensible. Las tareas lo reciben por referencia, así que los valores no
# aparecen nunca en la definición de tarea ni en la consola de ECS, que es donde acaban expuestos
# cuando se usan variables de entorno en claro.
resource "aws_secretsmanager_secret" "app" {
  name        = "${local.name}/app"
  description = "Credenciales de la plataforma de telemetría."

  # En producción no se destruye de inmediato: da margen para recuperar si se borra por error.
  recovery_window_in_days = var.environment == "production" ? 30 : 0
}

resource "aws_secretsmanager_secret_version" "app" {
  secret_id = aws_secretsmanager_secret.app.id

  secret_string = jsonencode({
    ConnectionStrings__FleetDb = join(";", [
      "Host=${aws_db_instance.main.address}",
      "Port=${aws_db_instance.main.port}",
      "Database=${aws_db_instance.main.db_name}",
      "Username=${aws_db_instance.main.username}",
      "Password=${random_password.database.result}",
      # TLS obligatorio hacia la base de datos: el tráfico cruza la VPC, pero cifrarlo es barato y
      # elimina una clase entera de problemas si algún día se peer con otra red.
      "SSL Mode=Require",
      "Trust Server Certificate=true",
    ])

    Redis__ConnectionString = join(",", [
      "${aws_elasticache_replication_group.main.primary_endpoint_address}:6379",
      "ssl=true",
      "abortConnect=false",
    ])

    RabbitMq__Host     = replace(aws_mq_broker.main.instances[0].endpoints[0], "amqps://", "")
    RabbitMq__Username = "fleet"
    RabbitMq__Password = random_password.broker.result

    Jwt__SigningKey   = random_password.jwt_signing_key.result
    Ingestion__ApiKey = random_password.ingestion_api_key.result
  })
}
