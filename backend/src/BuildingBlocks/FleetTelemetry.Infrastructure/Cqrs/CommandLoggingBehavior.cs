using System.Diagnostics;
using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Domain.Common;
using Microsoft.Extensions.Logging;

namespace FleetTelemetry.Infrastructure.Cqrs;

/// <summary>
/// Registra la ejecución de cada comando con su duración y su resultado.
/// </summary>
/// <remarks>
/// Es la razón práctica de tener dispatcher: esto se declara una vez y cubre todos los casos de uso
/// presentes y futuros. Los fallos se registran con su código de error, no con una excepción, porque
/// un duplicado o una validación son resultados normales y llenarían el log de falsos incidentes.
/// </remarks>
internal sealed partial class CommandLoggingBehavior<TCommand, TResponse>(
    ILogger<CommandLoggingBehavior<TCommand, TResponse>> logger)
    : ICommandPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        Func<Task<Result<TResponse>>> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var commandName = typeof(TCommand).Name;
        var timestamp = Stopwatch.GetTimestamp();

        var result = await next().ConfigureAwait(false);

        var elapsed = Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;

        if (result.IsSuccess)
        {
            CommandSucceeded(logger, commandName, elapsed);
        }
        else
        {
            CommandRejected(logger, commandName, result.Error.Code, elapsed);
        }

        return result;
    }

    [LoggerMessage(EventId = 2001, Level = LogLevel.Debug, Message = "{Command} completado en {ElapsedMs:F1} ms.")]
    static partial void CommandSucceeded(ILogger logger, string command, double elapsedMs);

    [LoggerMessage(EventId = 2002, Level = LogLevel.Debug,
        Message = "{Command} rechazado por {ErrorCode} en {ElapsedMs:F1} ms.")]
    static partial void CommandRejected(ILogger logger, string command, string errorCode, double elapsedMs);
}
