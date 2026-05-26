namespace ReSeq.Core.Models;

public enum RenameOperationType
{
    RenumberExisting,
    MoveExisting,
    AddNewVideo,
    DeleteExisting
}

public sealed class RenameOperation
{
    public required string SourcePath { get; init; }

    public string? TempPath { get; set; }

    public required string TargetPath { get; init; }

    public required string OldName { get; init; }

    public required string NewName { get; init; }

    public required RenameOperationType OperationType { get; init; }

    public bool IsNoOp => OperationType != RenameOperationType.DeleteExisting &&
        string.Equals(SourcePath, TargetPath, StringComparison.OrdinalIgnoreCase);
}
