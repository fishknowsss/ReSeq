using ReSeq.Core.Models;

namespace ReSeq.Core.Services;

public sealed class RenamePlanner
{
    private readonly IFileSystemService _fileSystem;

    public RenamePlanner(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Result<RenamePlan, ReSeqError> CreatePlan(
        string folderPath,
        IReadOnlyCollection<VideoItem> currentVideos,
        InsertOperation insertOperation)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var operations = new List<RenameOperation>();

        if (!_fileSystem.DirectoryExists(folderPath))
        {
            return Fail(ReSeqError.FolderNotFound, $"工作文件夹不存在：{folderPath}");
        }

        var tempFiles = _fileSystem.EnumerateFiles(folderPath)
            .Where(path => Path.GetFileName(path).StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();
        if (tempFiles.Count > 0)
        {
            return Fail(ReSeqError.TempFileExists, $"存在临时重命名残留文件：{string.Join(", ", tempFiles)}");
        }

        if (!_fileSystem.FileExists(insertOperation.NewVideoPath))
        {
            return Fail(ReSeqError.SourceMissing, $"拖入文件不存在：{insertOperation.NewVideoPath}");
        }

        if (!VideoScanner.IsSupportedVideo(insertOperation.NewVideoPath))
        {
            return Fail(ReSeqError.UnsupportedExtension, "只支持 mp4、mov、avi、mkv、wmv 视频");
        }

        if (insertOperation.TargetX < 1 || insertOperation.TargetY < 1)
        {
            return Fail(ReSeqError.InvalidTarget, "目标编号必须从 1 开始");
        }

        var duplicateExisting = currentVideos
            .GroupBy(item => (item.X, item.Y))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.X}-{group.Key.Y}")
            .ToList();
        if (duplicateExisting.Count > 0)
        {
            return Fail(ReSeqError.DuplicateNumber, $"存在重复编号，请先手动处理：{string.Join(", ", duplicateExisting)}");
        }

        switch (insertOperation.Type)
        {
            case InsertOperationType.InsertShotRow:
                AddRenumberedExisting(
                    currentVideos.Where(item => item.X >= insertOperation.TargetX),
                    item => (item.X + 1, item.Y),
                    folderPath,
                    operations);
                AddNewVideoOperation(folderPath, insertOperation.NewVideoPath, insertOperation.TargetX, 1, operations);
                break;

            case InsertOperationType.InsertVersion:
                AddRenumberedExisting(
                    currentVideos.Where(item => item.X == insertOperation.TargetX && item.Y >= insertOperation.TargetY),
                    item => (item.X, item.Y + 1),
                    folderPath,
                    operations);
                AddNewVideoOperation(folderPath, insertOperation.NewVideoPath, insertOperation.TargetX, insertOperation.TargetY, operations);
                break;

            case InsertOperationType.PlaceIntoEmptyCell:
                if (currentVideos.Any(item => item.X == insertOperation.TargetX && item.Y == insertOperation.TargetY))
                {
                    return Fail(ReSeqError.TargetOccupied, $"目标 {insertOperation.TargetX}-{insertOperation.TargetY} 已有视频，不能覆盖");
                }

                AddNewVideoOperation(folderPath, insertOperation.NewVideoPath, insertOperation.TargetX, insertOperation.TargetY, operations);
                break;

            default:
                return Fail(ReSeqError.InvalidTarget, "未知拖拽操作");
        }

        var validationError = ValidateOperationSet(operations);
        if (validationError is not null)
        {
            return Fail(ReSeqError.DuplicateTarget, validationError);
        }

        var conflictError = ValidateTargetConflicts(operations);
        if (conflictError is not null)
        {
            return Fail(ReSeqError.TargetOccupied, conflictError);
        }

        var ordered = operations
            .Where(operation => !operation.IsNoOp)
            .OrderBy(operation => Path.GetFileName(operation.TargetPath), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ordered.Count == 0)
        {
            warnings.Add("没有需要重命名的文件");
            return Result<RenamePlan, ReSeqError>.Fail(
                ReSeqError.PlanEmpty,
                warnings);
        }

        return Result<RenamePlan, ReSeqError>.Ok(new RenamePlan(folderPath, insertOperation, ordered, errors, warnings));

        Result<RenamePlan, ReSeqError> Fail(ReSeqError error, string message)
        {
            return Result<RenamePlan, ReSeqError>.Fail(error, [message]);
        }
    }

    public Result<RenamePlan, ReSeqError> CreateMoveExistingPlan(
        string folderPath,
        IReadOnlyCollection<VideoItem> currentVideos,
        VideoItem movingVideo,
        InsertOperationType operationType,
        int targetX,
        int targetY)
    {
        if (operationType == InsertOperationType.InsertShotRow)
        {
            targetY = 1;
        }

        var remaining = currentVideos
            .Where(item => !string.Equals(item.FilePath, movingVideo.FilePath, StringComparison.OrdinalIgnoreCase))
            .Select(item => item with { })
            .ToList();

        var compacted = CompactAfterRemoving(remaining, movingVideo.X, movingVideo.Y);
        var adjustedTargetX = targetX;
        var adjustedTargetY = targetY;

        if (operationType != InsertOperationType.InsertShotRow &&
            movingVideo.X == targetX &&
            movingVideo.Y < targetY)
        {
            adjustedTargetY = Math.Max(1, targetY - 1);
        }

        if (operationType == InsertOperationType.InsertShotRow && movingVideo.X < targetX)
        {
            adjustedTargetX = Math.Max(1, targetX - 1);
        }

        var withMoving = ApplyInsertToList(compacted, movingVideo, operationType, adjustedTargetX, adjustedTargetY);
        return BuildPlanFromFinalPositions(folderPath, currentVideos, withMoving, new InsertOperation(operationType, targetX, targetY, movingVideo.FilePath));
    }

    public Result<RenamePlan, ReSeqError> CreateDeletePlan(
        string folderPath,
        IReadOnlyCollection<VideoItem> currentVideos,
        VideoItem deletingVideo)
    {
        var remaining = currentVideos
            .Where(item => !string.Equals(item.FilePath, deletingVideo.FilePath, StringComparison.OrdinalIgnoreCase))
            .Select(item => item with { })
            .ToList();
        var compacted = CompactAfterRemoving(remaining, deletingVideo.X, deletingVideo.Y);
        var result = BuildPlanFromFinalPositions(
            folderPath,
            currentVideos,
            compacted,
            new InsertOperation(InsertOperationType.PlaceIntoEmptyCell, deletingVideo.X, deletingVideo.Y, deletingVideo.FilePath),
            [deletingVideo.FilePath]);

        if (!result.IsSuccess && result.Error != ReSeqError.PlanEmpty || result.IsSuccess && result.Value is null)
        {
            return result;
        }

        var deleteOperation = new RenameOperation
        {
            SourcePath = deletingVideo.FilePath,
            TargetPath = deletingVideo.FilePath,
            OldName = deletingVideo.OriginalFileName,
            NewName = "删除",
            OperationType = RenameOperationType.DeleteExisting
        };
        var operation = result.Value?.Operation ?? new InsertOperation(InsertOperationType.PlaceIntoEmptyCell, deletingVideo.X, deletingVideo.Y, deletingVideo.FilePath);
        var operations = (result.Value?.Operations ?? Array.Empty<RenameOperation>()).Prepend(deleteOperation).ToList();
        return Result<RenamePlan, ReSeqError>.Ok(new RenamePlan(folderPath, operation, operations, [], []));
    }

    private static void AddRenumberedExisting(
        IEnumerable<VideoItem> videos,
        Func<VideoItem, (int X, int Y)> targetSelector,
        string folderPath,
        ICollection<RenameOperation> operations)
    {
        foreach (var item in videos)
        {
            var (x, y) = targetSelector(item);
            var targetPath = Path.Combine(folderPath, $"{x}-{y}{item.Extension}");
            operations.Add(new RenameOperation
            {
                SourcePath = item.FilePath,
                TargetPath = targetPath,
                OldName = item.OriginalFileName,
                NewName = Path.GetFileName(targetPath),
                OperationType = RenameOperationType.RenumberExisting
            });
        }
    }

    private Result<RenamePlan, ReSeqError> BuildPlanFromFinalPositions(
        string folderPath,
        IReadOnlyCollection<VideoItem> currentVideos,
        IReadOnlyCollection<VideoItem> finalVideos,
        InsertOperation operation,
        IReadOnlyCollection<string>? additionalVacatedSources = null)
    {
        if (!_fileSystem.DirectoryExists(folderPath))
        {
            return Result<RenamePlan, ReSeqError>.Fail(ReSeqError.FolderNotFound, [$"工作文件夹不存在：{folderPath}"]);
        }

        var tempFiles = _fileSystem.EnumerateFiles(folderPath)
            .Where(path => Path.GetFileName(path).StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();
        if (tempFiles.Count > 0)
        {
            return Result<RenamePlan, ReSeqError>.Fail(ReSeqError.TempFileExists, [$"存在临时重命名残留文件：{string.Join(", ", tempFiles)}"]);
        }

        var operations = new List<RenameOperation>();
        foreach (var item in finalVideos)
        {
            var targetPath = Path.Combine(folderPath, $"{item.X}-{item.Y}{item.Extension}");
            var source = currentVideos.FirstOrDefault(video => string.Equals(video.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase));
            if (source is null)
            {
                return Result<RenamePlan, ReSeqError>.Fail(ReSeqError.SourceMissing, [$"源文件不存在：{item.OriginalFileName}"]);
            }

            operations.Add(new RenameOperation
            {
                SourcePath = source.FilePath,
                TargetPath = targetPath,
                OldName = source.OriginalFileName,
                NewName = Path.GetFileName(targetPath),
                OperationType = string.Equals(source.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase)
                    ? RenameOperationType.MoveExisting
                    : RenameOperationType.RenumberExisting
            });
        }

        var validationError = ValidateOperationSet(operations);
        if (validationError is not null)
        {
            return Result<RenamePlan, ReSeqError>.Fail(ReSeqError.DuplicateTarget, [validationError]);
        }

        var conflictError = ValidateTargetConflicts(operations, additionalVacatedSources);
        if (conflictError is not null)
        {
            return Result<RenamePlan, ReSeqError>.Fail(ReSeqError.TargetOccupied, [conflictError]);
        }

        var ordered = operations
            .Where(item => !item.IsNoOp)
            .OrderBy(item => Path.GetFileName(item.TargetPath), StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ordered.Count == 0
            ? Result<RenamePlan, ReSeqError>.Fail(ReSeqError.PlanEmpty, ["没有需要重命名的文件"])
            : Result<RenamePlan, ReSeqError>.Ok(new RenamePlan(folderPath, operation, ordered, [], []));
    }

    private static IReadOnlyList<VideoItem> CompactAfterRemoving(
        IReadOnlyCollection<VideoItem> remaining,
        int removedX,
        int removedY)
    {
        if (remaining.Any(item => item.X == removedX))
        {
            return remaining
                .Select(item => item.X == removedX && item.Y > removedY
                    ? item with { Y = item.Y - 1 }
                    : item)
                .ToList();
        }

        return remaining
            .Select(item => item.X > removedX ? item with { X = item.X - 1 } : item)
            .ToList();
    }

    private static IReadOnlyList<VideoItem> ApplyInsertToList(
        IReadOnlyCollection<VideoItem> videos,
        VideoItem movingVideo,
        InsertOperationType operationType,
        int targetX,
        int targetY)
    {
        var adjusted = operationType switch
        {
            InsertOperationType.InsertShotRow => videos
                .Select(item => item.X >= targetX ? item with { X = item.X + 1 } : item)
                .Append(movingVideo with { X = targetX, Y = 1 })
                .ToList(),
            InsertOperationType.InsertVersion => videos
                .Select(item => item.X == targetX && item.Y >= targetY ? item with { Y = item.Y + 1 } : item)
                .Append(movingVideo with { X = targetX, Y = targetY })
                .ToList(),
            _ => videos
                .Append(movingVideo with { X = targetX, Y = targetY })
                .ToList()
        };

        return adjusted;
    }

    private static void AddNewVideoOperation(
        string folderPath,
        string newVideoPath,
        int x,
        int y,
        ICollection<RenameOperation> operations)
    {
        var extension = Path.GetExtension(newVideoPath);
        var targetPath = Path.Combine(folderPath, $"{x}-{y}{extension}");

        operations.Add(new RenameOperation
        {
            SourcePath = newVideoPath,
            TargetPath = targetPath,
            OldName = Path.GetFileName(newVideoPath),
            NewName = Path.GetFileName(targetPath),
            OperationType = RenameOperationType.AddNewVideo
        });
    }

    private static string? ValidateOperationSet(IReadOnlyCollection<RenameOperation> operations)
    {
        var duplicatedTargets = operations
            .GroupBy(operation => operation.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => Path.GetFileName(group.Key))
            .ToList();

        return duplicatedTargets.Count > 0
            ? $"重命名计划产生重复目标：{string.Join(", ", duplicatedTargets)}"
            : null;
    }

    private string? ValidateTargetConflicts(
        IReadOnlyCollection<RenameOperation> operations,
        IReadOnlyCollection<string>? additionalVacatedSources = null)
    {
        var sourceSet = operations
            .Where(operation => operation.OperationType is RenameOperationType.RenumberExisting or RenameOperationType.MoveExisting)
            .Select(operation => operation.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (additionalVacatedSources is not null)
        {
            foreach (var source in additionalVacatedSources)
            {
                sourceSet.Add(source);
            }
        }

        foreach (var operation in operations)
        {
            if (!_fileSystem.FileExists(operation.TargetPath))
            {
                continue;
            }

            if (sourceSet.Contains(operation.TargetPath))
            {
                continue;
            }

            if (string.Equals(operation.SourcePath, operation.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return $"目标文件已存在：{Path.GetFileName(operation.TargetPath)}";
        }

        return null;
    }
}
