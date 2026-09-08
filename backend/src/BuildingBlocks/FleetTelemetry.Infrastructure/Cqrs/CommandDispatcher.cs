using System.Collections.Concurrent;
using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FleetTelemetry.Infrastructure.Cqrs;

/// <summary>
/// Resuelve el handler de un comando y lo ejecuta envuelto en el pipeline configurado.
/// </summary>
/// <remarks>
/// Vive en Infrastructure y no en Application porque necesita <see cref="IServiceProvider"/>, y la
/// capa de aplicación no puede depender del contenedor de DI. Application declara la interfaz; aquí
/// está el detalle técnico.
///
/// El tipo concreto del comando solo se conoce en ejecución, así que hace falta un salto por
/// reflexión. Se hace una vez por tipo y se cachea: a partir de la segunda invocación el coste es el
/// de una búsqueda en un diccionario.
/// </remarks>
internal sealed class CommandDispatcher(IServiceProvider provider) : ICommandDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Executors = new();

    public Task<Result<TResponse>> SendAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var executor = (IExecutor<TResponse>)Executors.GetOrAdd(
            command.GetType(),
            commandType => Activator.CreateInstance(
                typeof(Executor<,>).MakeGenericType(commandType, typeof(TResponse)))!);

        return executor.ExecuteAsync(provider, command, cancellationToken);
    }

    private interface IExecutor<TResponse>
    {
        Task<Result<TResponse>> ExecuteAsync(
            IServiceProvider provider,
            object command,
            CancellationToken cancellationToken);
    }

    private sealed class Executor<TCommand, TResponse> : IExecutor<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public Task<Result<TResponse>> ExecuteAsync(
            IServiceProvider provider,
            object command,
            CancellationToken cancellationToken)
        {
            var typedCommand = (TCommand)command;
            var handler = provider.GetRequiredService<ICommandHandler<TCommand, TResponse>>();
            var behaviors = provider.GetServices<ICommandPipelineBehavior<TCommand, TResponse>>().ToArray();

            Task<Result<TResponse>> Pipeline() => handler.HandleAsync(typedCommand, cancellationToken);

            var next = (Func<Task<Result<TResponse>>>)Pipeline;

            // Se recorre al revés para que el primer behavior registrado sea el más externo, que es
            // lo que uno espera al leer el registro de DI de arriba abajo.
            for (var index = behaviors.Length - 1; index >= 0; index--)
            {
                var behavior = behaviors[index];
                var inner = next;
                next = () => behavior.HandleAsync(typedCommand, inner, cancellationToken);
            }

            return next();
        }
    }
}
