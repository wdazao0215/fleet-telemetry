output "alb_dns_name" {
  description = "DNS del balanceador; a esto apuntan los registros CNAME de los tres hosts."
  value       = aws_lb.main.dns_name
}

output "ecr_repositories" {
  description = "URLs de los repositorios de imágenes, para configurar ECR_REGISTRY en el pipeline."
  value = merge(
    { for name, repository in aws_ecr_repository.services : name => repository.repository_url },
    { migrator = aws_ecr_repository.migrator.repository_url },
  )
}

output "database_endpoint" {
  description = "Endpoint de PostgreSQL."
  value       = aws_db_instance.main.address
}

output "app_secret_arn" {
  description = "ARN del secreto con la configuración sensible."
  value       = aws_secretsmanager_secret.app.arn
}

# La contraseña no se expone ni siquiera marcada como sensible: quien la necesite la lee de Secrets
# Manager con sus propios permisos, en lugar de que quede escrita en el estado de salida.
