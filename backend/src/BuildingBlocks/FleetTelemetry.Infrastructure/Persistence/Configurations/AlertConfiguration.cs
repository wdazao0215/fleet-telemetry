using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Telemetry;
using FleetTelemetry.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FleetTelemetry.Infrastructure.Persistence.Configurations;

internal sealed class AlertConfiguration : IEntityTypeConfiguration<Alert>
{
    public void Configure(EntityTypeBuilder<Alert> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("alerts");
        builder.HasKey(alert => alert.Id);

        builder.Property(alert => alert.Id).HasColumnName("id");

        builder.Property(alert => alert.VehicleId)
            .HasColumnName("vehicle_id")
            .HasMaxLength(VehicleId.MaxLength)
            .HasConversion(id => id.Value, value => VehicleId.Create(value).Value);

        builder.Property(alert => alert.Kind).HasColumnName("kind").HasConversion<int>();
        builder.Property(alert => alert.RaisedAt).HasColumnName("raised_at");
        builder.Property(alert => alert.AcknowledgedAt).HasColumnName("acknowledged_at");
        builder.Property(alert => alert.Detail).HasColumnName("detail").HasMaxLength(512).IsRequired();

        // ComplexProperty mapea el value object a dos columnas sin convertirlo en entidad ni
        // obligar a abrir su constructor: EF materializa por el constructor privado. Descomponerlo
        // a mano en shadow properties dejaría Alert.Position vacío al leer de la base.
        builder.ComplexProperty(alert => alert.Position, position =>
        {
            position.Property(coordinate => coordinate.Latitude).HasColumnName("latitude");
            position.Property(coordinate => coordinate.Longitude).HasColumnName("longitude");
        });

        builder.Ignore(alert => alert.IsAcknowledged);

        builder.HasIndex(alert => alert.RaisedAt).HasDatabaseName("ix_alerts_raised_at").IsDescending();
        builder.HasIndex(alert => alert.VehicleId).HasDatabaseName("ix_alerts_vehicle_id");
    }
}
