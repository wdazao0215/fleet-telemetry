variable "aws_region" {
  description = "Región donde se despliega la plataforma."
  type        = string
  default     = "us-east-1"
}

variable "environment" {
  description = "Entorno lógico (staging, production)."
  type        = string
  default     = "staging"

  validation {
    condition     = contains(["staging", "production"], var.environment)
    error_message = "El entorno debe ser staging o production."
  }
}

variable "vpc_cidr" {
  description = "Rango de la VPC."
  type        = string
  default     = "10.20.0.0/16"
}

variable "availability_zones" {
  description = "Zonas de disponibilidad. Dos como mínimo: RDS Multi-AZ y el ALB las exigen."
  type        = list(string)
  default     = ["us-east-1a", "us-east-1b"]

  validation {
    condition     = length(var.availability_zones) >= 2
    error_message = "Hacen falta al menos dos zonas de disponibilidad."
  }
}

variable "database_instance_class" {
  description = "Clase de instancia de RDS."
  type        = string
  default     = "db.t4g.medium"
}

variable "database_allocated_storage_gb" {
  description = "Almacenamiento inicial de RDS en GB."
  type        = number
  default     = 100
}

variable "cache_node_type" {
  description = "Tipo de nodo de ElastiCache."
  type        = string
  default     = "cache.t4g.micro"
}

variable "broker_instance_type" {
  description = "Tipo de instancia de Amazon MQ."
  type        = string
  default     = "mq.t3.micro"
}

variable "ingestion_desired_count" {
  description = "Réplicas del servicio de ingesta."
  type        = number
  default     = 2
}

variable "query_desired_count" {
  description = "Réplicas de la API de consulta."
  type        = number
  default     = 2
}

variable "worker_desired_count" {
  description = "Réplicas del worker de procesamiento."
  type        = number
  default     = 2
}

variable "web_desired_count" {
  description = "Réplicas del dashboard."
  type        = number
  default     = 2
}

variable "image_tag" {
  description = "Etiqueta de las imágenes a desplegar. El pipeline la fija al SHA del commit."
  type        = string
  default     = "latest"
}

variable "certificate_arn" {
  description = "ARN del certificado de ACM para el listener HTTPS."
  type        = string
}

variable "ingestion_host" {
  description = "Nombre de host público de la API de ingesta."
  type        = string
  default     = "ingest.fleet.example.com"
}

variable "query_host" {
  description = "Nombre de host público de la API de consulta."
  type        = string
  default     = "api.fleet.example.com"
}

variable "dashboard_host" {
  description = "Nombre de host público del dashboard, usado para la política de CORS."
  type        = string
  default     = "fleet.example.com"
}
