terraform {
  required_version = ">= 1.9"

  required_providers {
    aws = {
      source = "hashicorp/aws"
      # Se fija el major y se deja flotar el minor: los cambios de major de este proveedor rompen
      # atributos de recursos, y descubrirlo durante un despliegue es el peor momento posible.
      version = "~> 6.0"
    }

    random = {
      source  = "hashicorp/random"
      version = "~> 3.6"
    }
  }

  # El backend se declara vacío a propósito para que `terraform init -backend=false` funcione en CI
  # sin credenciales. En un despliegue real se configura con -backend-config apuntando a un bucket
  # S3 con bloqueo, porque un estado en local es un despliegue que solo una persona puede hacer.
  backend "s3" {}
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Project     = "fleet-telemetry"
      Environment = var.environment
      ManagedBy   = "terraform"
    }
  }
}
