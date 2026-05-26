namespace ReSeq.Core.Models;

public enum RenameProgressStage
{
    Validating,
    Phase1ToTemp,
    Phase2ToFinal,
    MovingNewVideo,
    RollingBack,
    Completed,
    Failed
}

public sealed record RenameProgress(
    RenameProgressStage Stage,
    int Completed,
    int Total,
    string Message,
    string? FileName = null);
