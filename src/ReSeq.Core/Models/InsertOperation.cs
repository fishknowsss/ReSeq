namespace ReSeq.Core.Models;

public enum InsertOperationType
{
    InsertShotRow,
    InsertVersion,
    PlaceIntoEmptyCell
}

public sealed record InsertOperation(
    InsertOperationType Type,
    int TargetX,
    int TargetY,
    string NewVideoPath);
