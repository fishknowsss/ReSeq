using ReSeq.Core.Models;

namespace ReSeq.Core.Services;

public sealed class VideoScanner
{
    public static readonly ISet<string> SupportedExtensions = new HashSet<string>(
        [".mp4", ".mov", ".avi", ".mkv", ".wmv"],
        StringComparer.OrdinalIgnoreCase);

    public ScanResult Scan(string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"文件夹不存在：{folderPath}");
        }

        var videos = new List<VideoItem>();
        var invalid = new List<InvalidFileInfo>();
        var tempFiles = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(folderPath))
        {
            var fileName = Path.GetFileName(filePath);
            var extension = Path.GetExtension(filePath);

            if (fileName.StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase))
            {
                tempFiles.Add(filePath);
                continue;
            }

            if (!IsSupportedVideo(filePath))
            {
                continue;
            }

            if (!VideoNameParser.TryParse(fileName, out var x, out var y, out var parsedExtension) ||
                !SupportedExtensions.Contains(parsedExtension))
            {
                invalid.Add(new InvalidFileInfo(filePath, "文件名不符合 数字-数字.扩展名"));
                continue;
            }

            videos.Add(new VideoItem(filePath, fileName, extension, x, y));
        }

        var duplicates = videos
            .GroupBy(item => (item.X, item.Y))
            .Where(group => group.Count() > 1)
            .Select(group => new DuplicateGroup(group.Key.X, group.Key.Y, group.OrderBy(item => item.OriginalFileName, StringComparer.OrdinalIgnoreCase).ToList()))
            .OrderBy(group => group.X)
            .ThenBy(group => group.Y)
            .ToList();

        var orderedVideos = videos
            .OrderBy(item => item.X)
            .ThenBy(item => item.Y)
            .ThenBy(item => item.OriginalFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new ScanResult(folderPath, orderedVideos, invalid, duplicates, tempFiles);
    }

    public static bool IsSupportedVideo(string filePath)
    {
        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }
}
