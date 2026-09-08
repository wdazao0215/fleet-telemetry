using System.Collections.Concurrent;
using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace FleetTelemetry.Infrastructure.Cqrs;

/// <summary>
/// Localiza y ejecuta el handler de una query.
/// </summary>
/// <remarks>
/// Sin pipeline de behaviors: las queries no mutan estado, así que no necesitan las garantías
/// transaccionales ni de auditoría que sí justifican envolver un comando.
/// </remarks>
internal sealed class QueryDispatcher(IServiceProvider provider) : IQueryDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Executors = new();

    public Task<Result<TResponse>> QueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var executor = (IExecutor<TResponse>)Executors.GetOrAdd(
            query.GetType(),
            queryType => Activator.CreateInstance(
                typeof(Executor<,>).MakeGenericType(queryType, typeof(TResponse)))!);

        return executor.ExecuteAsync(provider, query, cancellationToken);
    }

    private interface IExecutor<TResponse>
    {
        Task<Result<TResponse>> ExecuteAsync(
            IServiceProvider provider,
            object query,
            CancellationToken cancellationToken);
    }

    private sealed class Executor<TQuery, TResponse> : IExecutor<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public Task<Result<TResponse>> ExecuteAsync(
            IServiceProvider provider,
            object query,
            CancellationToken cancellationToken) =>
            provider
                .GetRequiredService<IQueryHandler<TQuery, TResponse>>()
                .HandleAsync((TQuery)query, cancellationToken);
    }
}
