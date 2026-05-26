using ReSeq.Core.Models;

namespace ReSeq.Core.Services;

public sealed class SafeRenameExecutor
{
    private readonly IFileSystemService _fileSystem;

    public SafeRenameExecutor(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public Result<ExecutionResult, ReSeqError> Execute(
        RenamePlan plan,
        IProgress<RenameProgress>? progress = null)
    {
        var messages = new List<string>();
        var errors = new List<string>();
        var total = plan.Operations.Count;
        var completedCount = 0;

        progress?.Report(new RenameProgress(RenameProgressStage.Validating, 0, total, "正在检查重命名计划"));

        if (!plan.CanExecute)
        {
            errors.AddRange(plan.Errors);
            if (errors.Count == 0)
            {
                errors.Add("没有可执行的重命名计划");
            }

            return Fail(ReSeqError.PlanEmpty, RenameProgressStage.Failed);
        }

        var tempFiles = _fileSystem.EnumerateFiles(plan.FolderPath)
            .Where(path => Path.GetFileName(path).StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase))
            .Select(Path.GetFileName)
            .ToList();
        if (tempFiles.Count > 0)
        {
            errors.Add($"存在临时重命名残留文件：{string.Join(", ", tempFiles)}");
            return Fail(ReSeqError.TempFileExists, RenameProgressStage.Failed);
        }

        var validationError = ValidateBeforeExecute(plan);
        if (validationError is not null)
        {
            errors.Add(validationError.Value.Message);
            return Fail(validationError.Value.Error, RenameProgressStage.Failed);
        }

        var stagedExisting = new List<RenameOperation>();
        var completed = new List<RenameOperation>();

        var existingOperations = plan.Operations
            .Where(operation => operation.OperationType is RenameOperationType.RenumberExisting or RenameOperationType.MoveExisting or RenameOperationType.DeleteExisting)
            .ToList();
        var finalRenameOperations = existingOperations
            .Where(operation => operation.OperationType != RenameOperationType.DeleteExisting)
            .ToList();
        var deleteOperations = existingOperations
            .Where(operation => operation.OperationType == RenameOperationType.DeleteExisting)
            .ToList();
        // [DECISION] 新视频放在已有文件全部进入最终位置后再移动，避免外部文件提前占住回滚路径。
        var newVideoOperations = plan.Operations
            .Where(operation => operation.OperationType == RenameOperationType.AddNewVideo)
            .ToList();

        try
        {
            foreach (var operation in existingOperations)
            {
                var tempPath = CreateTempPath(Path.GetDirectoryName(operation.SourcePath)!, operation.Extension());
                operation.TempPath = tempPath;
                _fileSystem.MoveFile(operation.SourcePath, tempPath);
                stagedExisting.Add(operation);
                completedCount++;
                messages.Add($"临时改名：{operation.OldName} -> {Path.GetFileName(tempPath)}");
                progress?.Report(new RenameProgress(RenameProgressStage.Phase1ToTemp, completedCount, total, "正在写入临时文件", operation.OldName));
            }
        }
        catch (Exception ex)
        {
            errors.Add($"第一阶段失败：{ex.Message}");
            RollBackStagedOnly(stagedExisting, messages, errors, progress, total);
            return Fail(ReSeqError.Phase1Failed, RenameProgressStage.Failed);
        }

        try
        {
            foreach (var operation in finalRenameOperations)
            {
                _fileSystem.MoveFile(operation.TempPath!, operation.TargetPath);
                completed.Add(operation);
                completedCount++;
                messages.Add($"完成：{operation.OldName} -> {operation.NewName}");
                progress?.Report(new RenameProgress(RenameProgressStage.Phase2ToFinal, completedCount, total, "正在写入目标文件", operation.NewName));
            }

            foreach (var operation in newVideoOperations)
            {
                _fileSystem.MoveFile(operation.SourcePath, operation.TargetPath);
                completed.Add(operation);
                completedCount++;
                messages.Add($"加入：{operation.OldName} -> {operation.NewName}");
                progress?.Report(new RenameProgress(RenameProgressStage.MovingNewVideo, completedCount, total, "正在加入新视频", operation.NewName));
            }

            foreach (var operation in deleteOperations)
            {
                _fileSystem.DeleteFile(operation.TempPath!);
                completed.Add(operation);
                completedCount++;
                messages.Add($"删除：{operation.OldName}");
                progress?.Report(new RenameProgress(RenameProgressStage.Phase2ToFinal, completedCount, total, "正在删除视频", operation.OldName));
            }

            progress?.Report(new RenameProgress(RenameProgressStage.Completed, total, total, "重命名完成"));
            return Result<ExecutionResult, ReSeqError>.Ok(ExecutionResult.FromMessages(true, messages, errors));
        }
        catch (Exception ex)
        {
            errors.Add($"第二阶段失败：{ex.Message}");
            RollBackAfterPhase2(stagedExisting, completed, messages, errors, progress, total);
            return Fail(ReSeqError.Phase2Failed, RenameProgressStage.Failed);
        }

        Result<ExecutionResult, ReSeqError> Fail(ReSeqError error, RenameProgressStage stage)
        {
            progress?.Report(new RenameProgress(stage, completedCount, total, errors.LastOrDefault() ?? "重命名失败"));
            return Result<ExecutionResult, ReSeqError>.Fail(
                error,
                errors.Count > 0 ? errors : messages);
        }
    }

    private (ReSeqError Error, string Message)? ValidateBeforeExecute(RenamePlan plan)
    {
        var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingSources = plan.Operations
            .Where(operation => operation.OperationType is RenameOperationType.RenumberExisting or RenameOperationType.MoveExisting or RenameOperationType.DeleteExisting)
            .Select(operation => operation.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in plan.Operations)
        {
            if (operation.OperationType == RenameOperationType.DeleteExisting)
            {
                if (!_fileSystem.FileExists(operation.SourcePath))
                {
                    return (ReSeqError.SourceMissing, $"源文件不存在：{operation.OldName}");
                }

                continue;
            }

            if (!targetSet.Add(operation.TargetPath))
            {
                return (ReSeqError.DuplicateTarget, $"重复目标：{Path.GetFileName(operation.TargetPath)}");
            }

            if (!_fileSystem.FileExists(operation.SourcePath))
            {
                return (ReSeqError.SourceMissing, $"源文件不存在：{operation.OldName}");
            }

            if (_fileSystem.FileExists(operation.TargetPath) &&
                !existingSources.Contains(operation.TargetPath) &&
                !string.Equals(operation.SourcePath, operation.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                return (ReSeqError.TargetOccupied, $"目标文件已存在：{Path.GetFileName(operation.TargetPath)}");
            }
        }

        return null;
    }

    private string CreateTempPath(string folderPath, string extension)
    {
        string tempPath;
        do
        {
            tempPath = Path.Combine(folderPath, $"__temp_rename_{Guid.NewGuid():N}{extension}");
        }
        while (_fileSystem.FileExists(tempPath));

        return tempPath;
    }

    private void RollBackStagedOnly(
        IReadOnlyCollection<RenameOperation> stagedExisting,
        ICollection<string> messages,
        ICollection<string> errors,
        IProgress<RenameProgress>? progress,
        int total)
    {
        foreach (var operation in stagedExisting.Reverse())
        {
            TryMoveBackTemp(operation, messages, errors, progress, total);
        }
    }

    private void RollBackAfterPhase2(
        IReadOnlyCollection<RenameOperation> stagedExisting,
        IReadOnlyCollection<RenameOperation> completed,
        ICollection<string> messages,
        ICollection<string> errors,
        IProgress<RenameProgress>? progress,
        int total)
    {
        foreach (var operation in completed.Reverse())
        {
            if (operation.OperationType == RenameOperationType.DeleteExisting)
            {
                TryMoveBackTemp(operation, messages, errors, progress, total);
                continue;
            }

            try
            {
                if (_fileSystem.FileExists(operation.TargetPath) && !_fileSystem.FileExists(operation.SourcePath))
                {
                    _fileSystem.MoveFile(operation.TargetPath, operation.SourcePath);
                    messages.Add($"回滚：{operation.NewName} -> {operation.OldName}");
                    progress?.Report(new RenameProgress(RenameProgressStage.RollingBack, 0, total, "正在回滚", operation.NewName));
                }
            }
            catch (Exception ex)
            {
                errors.Add($"回滚失败：{operation.NewName}，{ex.Message}");
            }
        }

        foreach (var operation in stagedExisting.Reverse().Except(completed))
        {
            TryMoveBackTemp(operation, messages, errors, progress, total);
        }
    }

    private void TryMoveBackTemp(
        RenameOperation operation,
        ICollection<string> messages,
        ICollection<string> errors,
        IProgress<RenameProgress>? progress,
        int total)
    {
        try
        {
            if (operation.TempPath is not null &&
                _fileSystem.FileExists(operation.TempPath) &&
                !_fileSystem.FileExists(operation.SourcePath))
            {
                _fileSystem.MoveFile(operation.TempPath, operation.SourcePath);
                messages.Add($"回滚临时文件：{Path.GetFileName(operation.TempPath)} -> {operation.OldName}");
                progress?.Report(new RenameProgress(RenameProgressStage.RollingBack, 0, total, "正在回滚临时文件", operation.OldName));
            }
        }
        catch (Exception ex)
        {
            errors.Add($"临时文件回滚失败：{operation.OldName}，{ex.Message}");
        }
    }
}

internal static class RenameOperationExtensions
{
    public static string Extension(this RenameOperation operation)
    {
        return Path.GetExtension(operation.SourcePath);
    }
}
