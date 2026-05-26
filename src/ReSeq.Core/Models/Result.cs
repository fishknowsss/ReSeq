namespace ReSeq.Core.Models;

public sealed class Result<T, TError>
{
    private Result(bool isSuccess, T? value, TError error, IReadOnlyList<string> messages)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        Messages = messages;
    }

    public bool IsSuccess { get; }

    public T? Value { get; }

    public TError Error { get; }

    public IReadOnlyList<string> Messages { get; }

    public static Result<T, TError> Ok(T value, IReadOnlyList<string>? messages = null)
    {
        return new Result<T, TError>(true, value, default!, messages ?? Array.Empty<string>());
    }

    public static Result<T, TError> Fail(TError error, IReadOnlyList<string>? messages = null)
    {
        return new Result<T, TError>(false, default, error, messages ?? Array.Empty<string>());
    }
}
