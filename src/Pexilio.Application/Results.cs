using Pexilio.Domain;

namespace Pexilio.Application;

public sealed record OperationFailure(
    int StatusCode,
    string Code,
    IReadOnlyList<FieldError> Errors,
    IReadOnlyDictionary<string, string>? Details = null)
{
    public static OperationFailure Validation(IReadOnlyList<FieldError> errors) =>
        new(400, ErrorCodes.ValidationFailed, errors);

    public static OperationFailure Of(int statusCode, string code) =>
        new(statusCode, code, []);

    public static OperationFailure Of(int statusCode, string code, IReadOnlyDictionary<string, string> details) =>
        new(statusCode, code, [], details);
}

public sealed class Result<T>
{
    private Result(T? value, OperationFailure? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public OperationFailure? Error { get; }

    public bool Succeeded => Error is null;

    public static Result<T> Success(T value) => new(value, null);

    public static Result<T> Fail(OperationFailure error) => new(default, error);
}
