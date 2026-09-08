namespace FleetTelemetry.Domain.Common;

/// <summary>
/// Resultado de una operación que puede fallar de forma prevista.
/// </summary>
/// <remarks>
/// Un GPS duplicado o una latitud fuera de rango no son situaciones excepcionales: en esta prueba
/// son el 15% del tráfico por diseño. Modelarlos como excepciones convertiría el flujo normal del
/// sistema en control de flujo por stack unwinding, que es caro y oculta la intención.
/// </remarks>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
        {
            throw new ArgumentException("Un resultado exitoso no puede llevar error.", nameof(error));
        }

        if (!isSuccess && error == Error.None)
        {
            throw new ArgumentException("Un resultado fallido necesita un error.", nameof(error));
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error) => this.value = value;

    public TValue Value => IsSuccess
        ? value!
        : throw new InvalidOperationException("No se puede leer el valor de un resultado fallido.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
