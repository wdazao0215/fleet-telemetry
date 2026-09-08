using FleetTelemetry.Domain.Telemetry;

namespace FleetTelemetry.Application.Abstractions.Models;

/// <summary>Punto del recorrido histórico de un vehículo.</summary>
public sealed record TrackPoint(Coordinate Position, DateTimeOffset RecordedAt);
