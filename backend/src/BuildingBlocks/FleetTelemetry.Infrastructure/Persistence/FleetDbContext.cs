using FleetTelemetry.Domain.Alerts;
using FleetTelemetry.Domain.Vehicles;
using Microsoft.EntityFrameworkCore;

namespace FleetTelemetry.Infrastructure.Persistence;

/// <summary>
/// Contexto de EF Core. No sale de este proyecto: los servicios acceden a datos por los puertos.
/// </summary>
public sealed class FleetDbContext(DbContextOptions<FleetDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();

    public DbSet<Alert> Alerts => Set<Alert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FleetDbContext).Assembly);

        // Las posiciones no son una entidad de EF: viven en una hypertable que se escribe con SQL
        // parametrizado e ON CONFLICT DO NOTHING. El change tracker no aporta nada a una tabla de
        // solo-inserción de alto volumen, y sí cuesta memoria y tiempo.
        base.OnModelCreating(modelBuilder);
    }
}
