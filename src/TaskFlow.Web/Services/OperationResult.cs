namespace TaskFlow.Web.Services;

public enum OperationStatus
{
    Ok = 0,
    NotFound = 1,
    Forbidden = 2,
    Conflict = 3,
    Invalid = 4
}

public readonly record struct OperationResult(OperationStatus Status)
{
    public static OperationResult Ok() => new(OperationStatus.Ok);
    public static OperationResult NotFound() => new(OperationStatus.NotFound);
    public static OperationResult Forbidden() => new(OperationStatus.Forbidden);
    public static OperationResult Conflict() => new(OperationStatus.Conflict);
    public static OperationResult Invalid() => new(OperationStatus.Invalid);

    public bool Succeeded => Status == OperationStatus.Ok;
}

public readonly record struct OperationResult<T>(OperationStatus Status, T? Value)
{
    public static OperationResult<T> Ok(T value) => new(OperationStatus.Ok, value);
    public static OperationResult<T> NotFound() => new(OperationStatus.NotFound, default);
    public static OperationResult<T> Forbidden() => new(OperationStatus.Forbidden, default);
    public static OperationResult<T> Conflict() => new(OperationStatus.Conflict, default);
    public static OperationResult<T> Invalid() => new(OperationStatus.Invalid, default);

    public bool Succeeded => Status == OperationStatus.Ok;
    public T ValueOrThrow => Succeeded ? Value! : throw new InvalidOperationException("La operación no fue exitosa.");
}