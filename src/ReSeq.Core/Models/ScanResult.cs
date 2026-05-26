namespace ReSeq.Core.Models;

public sealed class ScanResult
{
    public ScanResult(
        string folderPath,
        IReadOnlyList<VideoItem> videos,
        IReadOnlyList<InvalidFileInfo> invalidFiles,
        IReadOnlyList<DuplicateGroup> duplicateGroups,
        IReadOnlyList<string> tempFiles,
        IReadOnlyList<ScanIssue>? issues = null)
    {
        FolderPath = folderPath;
        Videos = videos;
        InvalidFiles = invalidFiles;
        DuplicateGroups = duplicateGroups;
        TempFiles = tempFiles;
        Issues = issues ?? BuildIssues(invalidFiles, duplicateGroups, tempFiles);
    }

    public string FolderPath { get; }

    public IReadOnlyList<VideoItem> Videos { get; }

    public IReadOnlyList<InvalidFileInfo> InvalidFiles { get; }

    public IReadOnlyList<DuplicateGroup> DuplicateGroups { get; }

    public IReadOnlyList<string> TempFiles { get; }

    public IReadOnlyList<ScanIssue> Issues { get; }

    public bool HasBlockingIssues => Issues.Any(issue => issue.IsBlocking);

    private static IReadOnlyList<ScanIssue> BuildIssues(
        IReadOnlyList<InvalidFileInfo> invalidFiles,
        IReadOnlyList<DuplicateGroup> duplicateGroups,
        IReadOnlyList<string> tempFiles)
    {
        var issues = new List<ScanIssue>();
        issues.AddRange(invalidFiles.Select(file => new ScanIssue(ScanIssueKind.InvalidName, file.FilePath, file.Reason)));
        foreach (var duplicate in duplicateGroups)
        {
            foreach (var item in duplicate.Items)
            {
                issues.Add(new ScanIssue(ScanIssueKind.DuplicateNumber, item.FilePath, $"重复编号 {duplicate.Number}", duplicate.X, duplicate.Y));
            }
        }

        issues.AddRange(tempFiles.Select(file => new ScanIssue(ScanIssueKind.TempFile, file, "发现临时重命名残留文件")));
        return issues;
    }
}
