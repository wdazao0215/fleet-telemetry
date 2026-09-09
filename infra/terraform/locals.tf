locals {
  name = "fleet-telemetry-${var.environment}"

  # Los cuatro ejecutables .NET más el dashboard. Se enumeran aquí para no repetir la lista en ECR,
  # en las definiciones de tarea y en los servicios.
  services = {
    ingestion-api     = { port = 8080, public = true }
    query-api         = { port = 8080, public = true }
    processing-worker = { port = 0, public = false }
    web               = { port = 3000, public = true }
  }

  container_images = {
    for name, _ in local.services :
    name => "${aws_ecr_repository.services[name].repository_url}:${var.image_tag}"
  }
}
