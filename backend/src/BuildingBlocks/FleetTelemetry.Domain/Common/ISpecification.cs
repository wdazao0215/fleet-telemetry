namespace FleetTelemetry.Domain.Common;

/// <summary>
/// Regla de negocio evaluable de forma aislada y combinable con otras.
/// </summary>
/// <remarks>
/// Se usa Specification en lugar de atributos de validación porque estas reglas son de dominio, no
/// de transporte: la misma comprobación de latitud debe aplicarse a una posición que llega por HTTP,
/// a una que llega en un lote de sincronización offline y a una que se rehidrata desde la cola.
/// Con atributos habría que repetirla en cada DTO.
/// </remarks>
public interface ISpecification<in T>
{
    Error Error { get; }

    bool IsSatisfiedBy(T candidate);
}

public static class SpecificationExtensions
{
    /// <summary>
    /// Evalúa todas las especificaciones y devuelve el primer incumplimiento.
    /// </summary>
    /// <remarks>
    /// Devuelve el primer error y no la lista completa a propósito: en la ruta de ingesta se
    /// descartan miles de payloads por segundo y basta con saber por qué se rechazó el primero.
    /// </remarks>
    public static Result IsSatisfiedByAll<T>(this IEnumerable<ISpecification<T>> specifications, T candidate)
    {
        ArgumentNullException.ThrowIfNull(specifications);

        foreach (var specification in specifications)
        {
            if (!specification.IsSatisfiedBy(candidate))
            {
                return Result.Failure(specification.Error);
            }
        }

        return Result.Success();
    }
}
