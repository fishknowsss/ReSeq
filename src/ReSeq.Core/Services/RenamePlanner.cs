using ReSeq.Core.Models;

namespace ReSeq.Core.Services;

public sealed class RenamePlanner
{
    public RenamePlan CreatePlan(
        string folderPath,
        IReadOnlyCollection<VideoItem> currentVideos,
        InsertOperation insertOperation)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        var operations = new List<RenameOperation>();

        if (!Directory.Exists(folderPath))
        {
            errors.Add($"工作文件夹不存在：{folderPath}");
        }

        if (!File.Exists(insertOperation.NewVideoPath))
        {
            errors.Add($"拖入文件不存在：{insertOperation.NewVideoPath}");
        }
        else if (!VideoScanner.IsSupportedVideo(insertOperation.NewVideoPath))
        {
            errors.Add("只支持 mp4、mov、avi、mkv、wmv 视频");
        }

        if (insertOperation.TargetX < 1 || insertOperation.TargetY < 1)
        {
            errors.Add("目标编号必须从 1 开始");
        }

        var duplicateExisting = currentVideos
            .GroupBy(item => (item.X, item.Y))
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key.X}-{group.Key.Y}")
            .ToList();
        if (duplicateExisting.Count > 0)
        {
            errors.Add($"存在重复编号，请先手动处理：{string.Join(", ", duplicateExisting)}");
        }

        if (errors.Count > 0)
        {
            return new RenamePlan(folderPath, insertOperation, operations, errors, warnings);
        }

        switch (insertOperation.Type)
        {
            case InsertOperationType.InsertShotRow:
                AddShiftedExisting(
                    currentVideos.Where(item => item.X >= insertOperation.TargetX),
                    item => (item.X + 1, item.Y),
                    folderPath,
                    operations);
                AddNewVideoOperation(folderPath, insertOperation.NewVideoPath, insertOperation.TargetX, 1, operations);
                break;

            case InsertOperationType.InsertVersion:
                AddShiftedExisting(
                    currentVideos.Where(item => item.X == insertOperation.TargetX && item.Y >= insertOperation.TargetY),
                    item => (item.X, item.Y + 1),
                    folderPath,
                    operations);
                AddNewVideoOperation(folderPath, insertOperation.NewVideoPath, insertOperation.TargetX, insertOperation.TargetY, operations);
                break;

            case InsertOperationType.PlaceIntoEmptyCell:
                if (currentVideos.Any(item => item.X == insertOperation.TargetX && item.Y == insertOperation.TargetY))
                {
                    errors.Add($"目标 {insertOperation.TargetX}-{insertOperation.TargetY} 已有视频，不能覆盖");
                }
                else
                {
                    AddNewVideoOperation(folderPath, insertOperation.NewVideoPath, insertOperation.TargetX, insertOperation.TargetY, operations);
                }
                break;

            default:
                errors.Add("未知拖拽操作");
                break;
        }

        ValidateOperationSet(operations, errors);
        ValidateTargetConflicts(operations, errors);

        var ordered = operations
            .Where(operation => !operation.IsNoOp)
            .OrderBy(operation => Path.GetFileName(operation.TargetPath), StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (ordered.Count == 0 && errors.Count == 0)
        {
            warnings.Add("没有需要重命名的文件");
        }

        return new RenamePlan(folderPath, insertOperation, ordered, errors, warnings);
    }

    private static void AddShiftedExisting(
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
                OperationType = RenameOperationType.ShiftExisting
            });
        }
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

    private static void ValidateOperationSet(IReadOnlyCollection<RenameOperation> operations, ICollection<string> errors)
    {
        var duplicatedTargets = operations
            .GroupBy(operation => operation.TargetPath, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => Path.GetFileName(group.Key))
            .ToList();

        if (duplicatedTargets.Count > 0)
        {
            errors.Add($"重命名计划产生重复目标：{string.Join(", ", duplicatedTargets)}");
        }
    }

    private static void ValidateTargetConflicts(IReadOnlyCollection<RenameOperation> operations, ICollection<string> errors)
    {
        var sourceSet = operations
            .Where(operation => operation.OperationType == RenameOperationType.ShiftExisting)
            .Select(operation => operation.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in operations)
        {
            if (!File.Exists(operation.TargetPath))
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

            errors.Add($"目标文件已存在：{Path.GetFileName(operation.TargetPath)}");
        }
    }
}
