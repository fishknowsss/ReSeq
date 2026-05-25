namespace ReSeq.Core.Models;

public sealed class RenamePlan
{
    public RenamePlan(
        string folderPath,
        InsertOperation operation,
        IReadOnlyList<RenameOperation> operations,
        IReadOnlyList<string> errors,
        IReadOnlyList<string> warnings)
    {
        FolderPath = folderPath;
        Operation = operation;
        Operations = operations;
        Errors = errors;
        Warnings = warnings;
    }

    public string FolderPath { get; }

    public InsertOperation Operation { get; }

    public IReadOnlyList<RenameOperation> Operations { get; }

    public IReadOnlyList<string> Errors { get; }

    public IReadOnlyList<string> Warnings { get; }

    public bool CanExecute => Errors.Count == 0 && Operations.Count > 0;
}
