namespace ReSeq.Core.Models;

public sealed class ExecutionResult
{
    public ExecutionResult(
        bool success,
        IReadOnlyList<string> messages,
        IReadOnlyList<string> errors)
    {
        Success = success;
        Messages = messages;
        Errors = errors;
    }

    public bool Success { get; }

    public IReadOnlyList<string> Messages { get; }

    public IReadOnlyList<string> Errors { get; }

    public static ExecutionResult FromMessages(bool success, IReadOnlyList<string> messages, IReadOnlyList<string> errors)
    {
        return new ExecutionResult(success, messages, errors);
    }
}
