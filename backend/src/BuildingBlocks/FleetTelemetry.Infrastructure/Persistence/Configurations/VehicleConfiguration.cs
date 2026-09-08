using FleetTelemetry.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTelemetry.Infrastructure.Persistence.Configurations;

internal sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("vehicles");
        builder.HasKey(vehicle => vehicle.Id);

        // El value object se guarda como texto plano. La validación de formato ya ocurrió al
        // construirlo, así que al leer se usa el constructor sin volver a validar: si estuviera mal
        // en la base, fallar en la lectura no ayudaría a nadie.
        builder.Property(vehicle => vehicle.Id)
            .HasColumnName("id")
            .HasMaxLength(VehicleId.MaxLength)
            .HasConversion(id => id.Value, value => VehicleId.Create(value).Value);

        builder.Property(vehicle => vehicle.Label).HasColumnName("label").HasMaxLength(128).IsRequired();
        builder.Property(vehicle => vehicle.State).HasColumnName("state").HasConversion<int>();
        builder.Property(vehicle => vehicle.RegisteredAt).HasColumnName("registered_at");
        builder.Property(vehicle => vehicle.DeletionRequestedAt).HasColumnName("deletion_requested_at");
        builder.Property(vehicle => vehicle.DeletionFailureReason)
            .HasColumnName("deletion_failure_reason")
            .HasMaxLength(512);

        // El dashboard filtra constantemente por estado para separar los vehículos activos de los
        // que están en mitad de una saga de borrado.
        builder.HasIndex(vehicle => vehicle.State).HasDatabaseName("ix_vehicles_state");
    }
}
