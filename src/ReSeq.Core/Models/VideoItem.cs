namespace ReSeq.Core.Models;

public sealed record VideoItem(
    string FilePath,
    string OriginalFileName,
    string Extension,
    int X,
    int Y)
{
    public string Number => $"{X}-{Y}";
}
