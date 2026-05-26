namespace ReSeq.Core.Models;

public enum ReSeqError
{
    None,
    FolderNotFound,
    SourceMissing,
    UnsupportedExtension,
    InvalidTarget,
    DuplicateNumber,
    TempFileExists,
    TargetOccupied,
    DuplicateTarget,
    PlanEmpty,
    Phase1Failed,
    Phase2Failed,
    RollbackFailed,
    OperationFailed,
    PermissionDenied,
    FileInUse,
    Unknown
}
