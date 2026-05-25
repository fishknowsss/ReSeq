namespace ReSeq.Core.Models;

public sealed record DuplicateGroup(int X, int Y, IReadOnlyList<VideoItem> Items)
{
    public string Number => $"{X}-{Y}";
}
