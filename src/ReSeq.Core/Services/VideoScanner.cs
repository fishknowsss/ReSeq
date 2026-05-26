using ReSeq.Core.Models;

namespace ReSeq.Core.Services;

public sealed class VideoScanner
{
    private readonly IFileSystemService _fileSystem;

    public VideoScanner(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public static readonly ISet<string> SupportedExtensions = new HashSet<string>(
        [".mp4", ".mov", ".avi", ".mkv", ".wmv"],
        StringComparer.OrdinalIgnoreCase);

    public Result<ScanResult, ReSeqError> Scan(string folderPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !_fileSystem.DirectoryExists(folderPath))
            {
                return Result<ScanResult, ReSeqError>.Fail(
                    ReSeqError.FolderNotFound,
                    [$"文件夹不存在：{folderPath}"]);
            }

            var videos = new List<VideoItem>();
            var invalid = new List<InvalidFileInfo>();
            var tempFiles = new List<string>();

            foreach (var filePath in _fileSystem.EnumerateFiles(folderPath))
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

            var result = new ScanResult(folderPath, orderedVideos, invalid, duplicates, tempFiles);
            return Result<ScanResult, ReSeqError>.Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Result<ScanResult, ReSeqError>.Fail(ReSeqError.PermissionDenied, [$"没有权限读取文件夹：{ex.Message}"]);
        }
        catch (IOException ex)
        {
            return Result<ScanResult, ReSeqError>.Fail(ReSeqError.FileInUse, [$"读取文件夹失败：{ex.Message}"]);
        }
        catch (Exception ex)
        {
            return Result<ScanResult, ReSeqError>.Fail(ReSeqError.Unknown, [$"扫描失败：{ex.Message}"]);
        }
    }

    public static bool IsSupportedVideo(string filePath)
    {
        return SupportedExtensions.Contains(Path.GetExtension(filePath));
    }
}
