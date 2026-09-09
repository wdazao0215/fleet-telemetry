resource "aws_ecr_repository" "services" {
  for_each = local.services

  name = "fleet-${each.key}"

  image_scanning_configuration {
    # Escaneo en cada push: una imagen base con un CVE conocido se detecta al publicarla y no meses
    # después en una auditoría.
    scan_on_push = true
  }

  # IMMUTABLE: impide sobrescribir una etiqueta ya publicada. Sin esto, dos despliegues distintos
  # pueden acabar con el mismo tag apuntando a imágenes diferentes, y reproducir un incidente se
  # vuelve imposible.
  image_tag_mutability = "IMMUTABLE"

  encryption_configuration {
    encryption_type = "AES256"
  }
}

resource "aws_ecr_lifecycle_policy" "services" {
  for_each = aws_ecr_repository.services

  repository = each.value.name

  # Sin política, el registro crece sin límite: cada commit a main publica cinco imágenes.
  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Conservar las últimas 30 imágenes"
        selection = {
          tagStatus   = "any"
          countType   = "imageCountMoreThan"
          countNumber = 30
        }
        action = { type = "expire" }
      }
    ]
  })
}

# El migrador no es un servicio con réplicas, pero sí necesita su propio repositorio: se ejecuta como
# tarea puntual antes de cada despliegue.
resource "aws_ecr_repository" "migrator" {
  name                 = "fleet-migrator"
  image_tag_mutability = "IMMUTABLE"

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = "AES256"
  }
}
