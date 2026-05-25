using ReSeq.Core.Models;

namespace ReSeq.Core.Services;

public sealed class SafeRenameExecutor
{
    public ExecutionResult Execute(RenamePlan plan)
    {
        var messages = new List<string>();
        var errors = new List<string>();

        if (!plan.CanExecute)
        {
            errors.AddRange(plan.Errors);
            if (errors.Count == 0)
            {
                errors.Add("没有可执行的重命名计划");
            }

            return new ExecutionResult(false, messages, errors);
        }

        if (!ValidateBeforeExecute(plan, errors))
        {
            return new ExecutionResult(false, messages, errors);
        }

        var stagedExisting = new List<RenameOperation>();
        var completed = new List<RenameOperation>();

        try
        {
            foreach (var operation in plan.Operations.Where(operation => operation.OperationType == RenameOperationType.ShiftExisting))
            {
                var tempPath = CreateTempPath(Path.GetDirectoryName(operation.SourcePath)!, operation.Extension());
                operation.TempPath = tempPath;
                File.Move(operation.SourcePath, tempPath);
                stagedExisting.Add(operation);
                messages.Add($"临时改名：{operation.OldName} -> {Path.GetFileName(tempPath)}");
            }

            foreach (var operation in plan.Operations.Where(operation => operation.OperationType == RenameOperationType.ShiftExisting))
            {
                File.Move(operation.TempPath!, operation.TargetPath);
                completed.Add(operation);
                messages.Add($"完成：{operation.OldName} -> {operation.NewName}");
            }

            foreach (var operation in plan.Operations.Where(operation => operation.OperationType == RenameOperationType.AddNewVideo))
            {
                File.Move(operation.SourcePath, operation.TargetPath);
                completed.Add(operation);
                messages.Add($"加入：{operation.OldName} -> {operation.NewName}");
            }

            return new ExecutionResult(true, messages, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"执行失败：{ex.Message}");
            RollBack(stagedExisting, completed, messages, errors);
            return new ExecutionResult(false, messages, errors);
        }
    }

    private static bool ValidateBeforeExecute(RenamePlan plan, ICollection<string> errors)
    {
        var targetSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingSources = plan.Operations
            .Where(operation => operation.OperationType == RenameOperationType.ShiftExisting)
            .Select(operation => operation.SourcePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var operation in plan.Operations)
        {
            if (!targetSet.Add(operation.TargetPath))
            {
                errors.Add($"重复目标：{Path.GetFileName(operation.TargetPath)}");
            }

            if (!File.Exists(operation.SourcePath))
            {
                errors.Add($"源文件不存在：{operation.OldName}");
            }

            if (File.Exists(operation.TargetPath) &&
                !existingSources.Contains(operation.TargetPath) &&
                !string.Equals(operation.SourcePath, operation.TargetPath, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"目标文件已存在：{Path.GetFileName(operation.TargetPath)}");
            }
        }

        return errors.Count == 0;
    }

    private static string CreateTempPath(string folderPath, string extension)
    {
        string tempPath;
        do
        {
            tempPath = Path.Combine(folderPath, $"__temp_rename_{Guid.NewGuid():N}{extension}");
        }
        while (File.Exists(tempPath));

        return tempPath;
    }

    private static void RollBack(
        IReadOnlyCollection<RenameOperation> stagedExisting,
        IReadOnlyCollection<RenameOperation> completed,
        ICollection<string> messages,
        ICollection<string> errors)
    {
        foreach (var operation in completed.Reverse())
        {
            try
            {
                if (operation.OperationType == RenameOperationType.ShiftExisting)
                {
                    if (File.Exists(operation.TargetPath) && !File.Exists(operation.SourcePath))
                    {
                        File.Move(operation.TargetPath, operation.SourcePath);
                        messages.Add($"回滚：{operation.NewName} -> {operation.OldName}");
                    }
                }
                else if (File.Exists(operation.TargetPath) && !File.Exists(operation.SourcePath))
                {
                    File.Move(operation.TargetPath, operation.SourcePath);
                    messages.Add($"回滚新文件：{operation.NewName} -> {operation.OldName}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"回滚失败：{operation.NewName}，{ex.Message}");
            }
        }

        foreach (var operation in stagedExisting.Reverse())
        {
            try
            {
                if (operation.TempPath is not null && File.Exists(operation.TempPath) && !File.Exists(operation.SourcePath))
                {
                    File.Move(operation.TempPath, operation.SourcePath);
                    messages.Add($"回滚临时文件：{Path.GetFileName(operation.TempPath)} -> {operation.OldName}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"临时文件回滚失败：{operation.OldName}，{ex.Message}");
            }
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
