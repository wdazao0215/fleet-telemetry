using FleetTelemetry.Domain.Common;

namespace FleetTelemetry.Application.Abstractions.Messaging;

/// <summary>Intención de cambiar el estado del sistema.</summary>
public interface ICommand<TResponse>;

/// <summary>Pregunta que no modifica nada.</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken cancellationToken);
}

/// <summary>Localiza y ejecuta el handler del comando, aplicando el pipeline configurado.</summary>
public interface ICommandDispatcher
{
    Task<Result<TResponse>> SendAsync<TResponse>(ICommand<TResponse> command, CancellationToken cancellationToken);
}

public interface IQueryDispatcher
{
    Task<Result<TResponse>> QueryAsync<TResponse>(IQuery<TResponse> query, CancellationToken cancellationToken);
}

/// <summary>
/// Preocupación transversal que envuelve la ejecución de un comando.
/// </summary>
/// <remarks>
/// Es la razón de tener dispatcher propio en vez de llamar al handler directamente: logging,
/// métricas y validación se declaran una vez y se aplican a todos los casos de uso, sin que cada
/// handler tenga que acordarse de invocarlos. Ver docs/adr/0002.
/// </remarks>
public interface ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(
        TCommand command,
        Func<Task<Result<TResponse>>> next,
        CancellationToken cancellationToken);
}
