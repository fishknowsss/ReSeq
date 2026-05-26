namespace ReSeq.Core.Models;

public enum ScanIssueKind
{
    InvalidName,
    DuplicateNumber,
    TempFile
}

public sealed record ScanIssue(ScanIssueKind Kind, string FilePath, string Message, int? X = null, int? Y = null)
{
    public bool IsBlocking => Kind is ScanIssueKind.DuplicateNumber or ScanIssueKind.TempFile;
}
