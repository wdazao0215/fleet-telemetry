# Red. Se usa el módulo oficial de VPC en lugar de escribir a mano las ~20 piezas (subredes, tablas
# de rutas, NAT, IGW): es infraestructura resuelta hace años, y reimplementarla solo añade sitios
# donde equivocarse sin aportar nada propio del sistema.
module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "~> 5.13"

  name = local.name
  cidr = var.vpc_cidr

  azs             = var.availability_zones
  public_subnets  = [for index, _ in var.availability_zones : cidrsubnet(var.vpc_cidr, 8, index)]
  private_subnets = [for index, _ in var.availability_zones : cidrsubnet(var.vpc_cidr, 8, index + 10)]

  # Un solo NAT gateway: cuesta unos 35 USD al mes cada uno y en staging la alta disponibilidad de la
  # salida a internet no compensa. En producción esto pasa a ser uno por zona.
  enable_nat_gateway = true
  single_nat_gateway = var.environment != "production"

  enable_dns_hostnames = true
  enable_dns_support   = true
}

resource "aws_security_group" "alb" {
  name        = "${local.name}-alb"
  description = "Entrada pública al balanceador."
  vpc_id      = module.vpc.vpc_id

  ingress {
    description = "HTTPS desde internet"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTP, solo para redirigir a HTTPS"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    description = "Hacia las tareas de ECS"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "app" {
  name        = "${local.name}-app"
  description = "Tareas de ECS."
  vpc_id      = module.vpc.vpc_id

  # Solo el balanceador puede alcanzar las tareas. Sin esta restricción, cualquier cosa dentro de la
  # VPC podría hablar directamente con la ingesta saltándose el ALB y sus reglas.
  ingress {
    description     = "Tráfico del balanceador"
    from_port       = 0
    to_port         = 65535
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  ingress {
    description = "Entre tareas del mismo grupo"
    from_port   = 0
    to_port     = 65535
    protocol    = "tcp"
    self        = true
  }

  egress {
    description = "Salida a AWS, ECR y dependencias"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }
}

resource "aws_security_group" "data" {
  name        = "${local.name}-data"
  description = "Base de datos, caché y broker."
  vpc_id      = module.vpc.vpc_id

  # Los almacenes solo aceptan conexiones de las tareas, nunca de internet ni del ALB.
  ingress {
    description     = "PostgreSQL"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.app.id]
  }

  ingress {
    description     = "Redis"
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [aws_security_group.app.id]
  }

  ingress {
    description     = "AMQP sobre TLS"
    from_port       = 5671
    to_port         = 5671
    protocol        = "tcp"
    security_groups = [aws_security_group.app.id]
  }
}
