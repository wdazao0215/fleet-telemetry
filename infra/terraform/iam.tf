data "aws_iam_policy_document" "ecs_assume_role" {
  statement {
    actions = ["sts:AssumeRole"]

    principals {
      type        = "Service"
      identifiers = ["ecs-tasks.amazonaws.com"]
    }
  }
}

# Rol de ejecución: lo usa el agente de ECS para descargar la imagen y resolver los secretos ANTES
# de arrancar el contenedor. Es distinto del rol de la tarea, que es lo que usa la aplicación ya en
# marcha; separarlos evita que el código de la app herede permisos para leer cualquier secreto.
resource "aws_iam_role" "execution" {
  name               = "${local.name}-execution"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume_role.json
}

resource "aws_iam_role_policy_attachment" "execution_managed" {
  role       = aws_iam_role.execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

data "aws_iam_policy_document" "read_app_secret" {
  statement {
    actions = ["secretsmanager:GetSecretValue"]
    # Solo este secreto, no todos los de la cuenta.
    resources = [aws_secretsmanager_secret.app.arn]
  }
}

resource "aws_iam_role_policy" "execution_secrets" {
  name   = "${local.name}-read-secrets"
  role   = aws_iam_role.execution.id
  policy = data.aws_iam_policy_document.read_app_secret.json
}

# Rol de la tarea: los permisos que tiene la aplicación en ejecución. Hoy no necesita ninguno —habla
# con Postgres, Redis y RabbitMQ por red, no por API de AWS— y se deja vacío a propósito. El día que
# haga falta S3 para exportar informes, se añade solo eso.
resource "aws_iam_role" "task" {
  name               = "${local.name}-task"
  assume_role_policy = data.aws_iam_policy_document.ecs_assume_role.json
}
