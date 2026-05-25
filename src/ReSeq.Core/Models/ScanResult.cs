namespace ReSeq.Core.Models;

public sealed class ScanResult
{
    public ScanResult(
        string folderPath,
        IReadOnlyList<VideoItem> videos,
        IReadOnlyList<InvalidFileInfo> invalidFiles,
        IReadOnlyList<DuplicateGroup> duplicateGroups,
        IReadOnlyList<string> tempFiles)
    {
        FolderPath = folderPath;
        Videos = videos;
        InvalidFiles = invalidFiles;
        DuplicateGroups = duplicateGroups;
        TempFiles = tempFiles;
    }

    public string FolderPath { get; }

    public IReadOnlyList<VideoItem> Videos { get; }

    public IReadOnlyList<InvalidFileInfo> InvalidFiles { get; }

    public IReadOnlyList<DuplicateGroup> DuplicateGroups { get; }

    public IReadOnlyList<string> TempFiles { get; }

    public bool HasBlockingIssues => DuplicateGroups.Count > 0 || TempFiles.Count > 0;
}
