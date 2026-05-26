using ReSeq.Core.Models;
using ReSeq.Core.Services;

var tests = new (string Name, Action Body)[]
{
    ("目标文件存在非计划文件时返回冲突", TestTargetConflict),
    ("第一阶段失败时不进入第二阶段", TestPhase1FailureStopsBeforePhase2),
    ("第二阶段失败时回滚已完成操作", TestPhase2FailureRollsBackCompletedMoves),
    ("临时文件残留被识别并阻止执行", TestTempResidueBlocksExecution),
    ("扫描文件夹异常时返回失败结果", TestScanFailureReturnsResult),
    ("重复编号被识别为问题", TestDuplicateNumberIssue),
    ("插入新镜头行时后续 X 递增", TestInsertShotRowPlan),
    ("删除中间版本时后续 Y 左移", TestDeleteVersionCompactsRow),
    ("删除整行时下方 X 上移", TestDeleteOnlyVideoCompactsRows),
    ("测试 A：插入新镜头行", TestInsertShotRowExecution),
    ("测试 B：插入同镜头新版本", TestInsertVersionExecution),
    ("测试 C：拖到空单元格", TestPlaceIntoEmptyCellExecution)
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        test.Body();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FAIL {test.Name}");
        Console.WriteLine(ex.Message);
    }
}

if (failed > 0)
{
    Environment.Exit(1);
}

static void TestTargetConflict()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old"),
        ("C:\\case\\work\\2-1.mp4", "foreign"),
        ("C:\\case\\incoming\\new.mp4", "new"));

    var planner = new RenamePlanner(fs);
    var videos = new[]
    {
        new VideoItem("C:\\case\\work\\1-1.mp4", "1-1.mp4", ".mp4", 1, 1)
    };

    var result = planner.CreatePlan(
        "C:\\case\\work",
        videos,
        new InsertOperation(InsertOperationType.PlaceIntoEmptyCell, 2, 1, "C:\\case\\incoming\\new.mp4"));

    AssertFalse(result.IsSuccess, "计划应返回冲突错误");
    AssertEqual(ReSeqError.TargetOccupied, result.Error, "错误类型不正确");
}

static void TestPhase1FailureStopsBeforePhase2()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1"),
        ("C:\\case\\work\\2-1.mp4", "old 2"),
        ("C:\\case\\incoming\\new.mp4", "new"));
    fs.FailMoveWhen = move => move.Source.EndsWith("2-1.mp4", StringComparison.OrdinalIgnoreCase)
        && Path.GetFileName(move.Target).StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase);

    var plan = CreatePlan(fs, "C:\\case\\work", new InsertOperation(InsertOperationType.InsertShotRow, 2, 1, "C:\\case\\incoming\\new.mp4"));
    var result = new SafeRenameExecutor(fs).Execute(plan);

    AssertFalse(result.IsSuccess, "第一阶段失败应返回失败结果");
    AssertEqual(ReSeqError.Phase1Failed, result.Error, "错误类型不正确");
    AssertFalse(fs.Moves.Any(move => Path.GetFileName(move.Source).StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase)),
        "第一阶段失败后不应启动第二阶段");
    AssertFile(fs, "C:\\case\\work\\1-1.mp4", "old 1");
    AssertFile(fs, "C:\\case\\work\\2-1.mp4", "old 2");
}

static void TestPhase2FailureRollsBackCompletedMoves()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1"),
        ("C:\\case\\work\\2-1.mp4", "old 2"),
        ("C:\\case\\work\\3-1.mp4", "old 3"),
        ("C:\\case\\incoming\\new.mp4", "new"));
    fs.FailMoveWhen = move => move.Target.Equals("C:\\case\\work\\4-1.mp4", StringComparison.OrdinalIgnoreCase);

    var plan = CreatePlan(fs, "C:\\case\\work", new InsertOperation(InsertOperationType.InsertShotRow, 2, 1, "C:\\case\\incoming\\new.mp4"));
    var result = new SafeRenameExecutor(fs).Execute(plan);

    AssertFalse(result.IsSuccess, "第二阶段失败应返回失败结果");
    AssertEqual(ReSeqError.Phase2Failed, result.Error, "错误类型不正确");
    AssertFile(fs, "C:\\case\\work\\1-1.mp4", "old 1");
    AssertFile(fs, "C:\\case\\work\\2-1.mp4", "old 2");
    AssertFile(fs, "C:\\case\\work\\3-1.mp4", "old 3");
    AssertFile(fs, "C:\\case\\incoming\\new.mp4", "new");
    AssertFalse(fs.Files.Any(path => Path.GetFileName(path).StartsWith("__temp_rename_", StringComparison.OrdinalIgnoreCase)),
        "回滚后不应残留临时文件");
}

static void TestTempResidueBlocksExecution()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old"),
        ("C:\\case\\work\\__temp_rename_left.mp4", "temp"),
        ("C:\\case\\incoming\\new.mp4", "new"));

    var scan = new VideoScanner(fs).Scan("C:\\case\\work").Value!;
    AssertTrue(scan.Issues.Any(issue => issue.Kind == ScanIssueKind.TempFile), "扫描应识别临时文件残留");

    var plan = new RenamePlan(
        "C:\\case\\work",
        new InsertOperation(InsertOperationType.PlaceIntoEmptyCell, 2, 1, "C:\\case\\incoming\\new.mp4"),
        [
            new RenameOperation
            {
                SourcePath = "C:\\case\\incoming\\new.mp4",
                TargetPath = "C:\\case\\work\\2-1.mp4",
                OldName = "new.mp4",
                NewName = "2-1.mp4",
                OperationType = RenameOperationType.AddNewVideo
            }
        ],
        [],
        []);
    var result = new SafeRenameExecutor(fs).Execute(plan);

    AssertFalse(result.IsSuccess, "临时文件残留应阻止执行");
    AssertEqual(ReSeqError.TempFileExists, result.Error, "错误类型不正确");
}

static void TestDuplicateNumberIssue()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old mp4"),
        ("C:\\case\\work\\1-1.mov", "old mov"));

    var scan = new VideoScanner(fs).Scan("C:\\case\\work").Value!;
    AssertTrue(scan.Issues.Any(issue => issue.Kind == ScanIssueKind.DuplicateNumber), "扫描应识别重复编号");
    AssertTrue(scan.HasBlockingIssues, "重复编号应是阻塞问题");
}

static void TestScanFailureReturnsResult()
{
    var fs = InMemoryFileSystem.Create(("C:\\case\\work\\1-1.mp4", "old"));
    fs.FailEnumerate = true;

    var result = new VideoScanner(fs).Scan("C:\\case\\work");

    AssertFalse(result.IsSuccess, "扫描异常应该返回失败结果");
    AssertEqual(ReSeqError.FileInUse, result.Error, "错误类型不正确");
}

static void TestInsertShotRowPlan()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1"),
        ("C:\\case\\work\\2-1.mp4", "old 2-1"),
        ("C:\\case\\work\\2-2.mp4", "old 2-2"),
        ("C:\\case\\work\\3-1.mp4", "old 3"),
        ("C:\\case\\incoming\\new.mp4", "new"));

    var plan = CreatePlan(fs, "C:\\case\\work", new InsertOperation(InsertOperationType.InsertShotRow, 2, 1, "C:\\case\\incoming\\new.mp4"));
    AssertTargets(plan, "2-1.mp4", "3-1.mp4", "3-2.mp4", "4-1.mp4");
}

static void TestDeleteVersionCompactsRow()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1"),
        ("C:\\case\\work\\1-2.mp4", "delete"),
        ("C:\\case\\work\\1-3.mp4", "old 3"));
    var scan = new VideoScanner(fs).Scan("C:\\case\\work").Value!;
    var deleting = scan.Videos.Single(item => item.X == 1 && item.Y == 2);
    var plan = new RenamePlanner(fs).CreateDeletePlan("C:\\case\\work", scan.Videos, deleting).Value!;
    var result = new SafeRenameExecutor(fs).Execute(plan);

    AssertTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Messages));
    AssertFiles(fs, "C:\\case\\work", "1-1.mp4", "1-2.mp4");
    AssertFile(fs, "C:\\case\\work\\1-2.mp4", "old 3");
}

static void TestDeleteOnlyVideoCompactsRows()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "row 1"),
        ("C:\\case\\work\\2-1.mp4", "delete"),
        ("C:\\case\\work\\3-1.mp4", "row 3"),
        ("C:\\case\\work\\3-2.mp4", "row 3-2"));
    var scan = new VideoScanner(fs).Scan("C:\\case\\work").Value!;
    var deleting = scan.Videos.Single(item => item.X == 2 && item.Y == 1);
    var plan = new RenamePlanner(fs).CreateDeletePlan("C:\\case\\work", scan.Videos, deleting).Value!;
    var result = new SafeRenameExecutor(fs).Execute(plan);

    AssertTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Messages));
    AssertFiles(fs, "C:\\case\\work", "1-1.mp4", "2-1.mp4", "2-2.mp4");
    AssertFile(fs, "C:\\case\\work\\2-1.mp4", "row 3");
    AssertFile(fs, "C:\\case\\work\\2-2.mp4", "row 3-2");
}

static void TestInsertShotRowExecution()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1-1"),
        ("C:\\case\\work\\2-1.mp4", "old 2-1"),
        ("C:\\case\\work\\2-2.mp4", "old 2-2"),
        ("C:\\case\\work\\3-1.mp4", "old 3-1"),
        ("C:\\case\\incoming\\new.mp4", "new"));

    ExecutePlan(fs, "C:\\case\\work", new InsertOperation(InsertOperationType.InsertShotRow, 2, 1, "C:\\case\\incoming\\new.mp4"));

    AssertFiles(fs, "C:\\case\\work", "1-1.mp4", "2-1.mp4", "3-1.mp4", "3-2.mp4", "4-1.mp4");
    AssertFile(fs, "C:\\case\\work\\2-1.mp4", "new");
    AssertFile(fs, "C:\\case\\work\\3-1.mp4", "old 2-1");
    AssertFile(fs, "C:\\case\\work\\3-2.mp4", "old 2-2");
    AssertFile(fs, "C:\\case\\work\\4-1.mp4", "old 3-1");
}

static void TestInsertVersionExecution()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1-1"),
        ("C:\\case\\work\\1-2.mp4", "old 1-2"),
        ("C:\\case\\work\\1-3.mp4", "old 1-3"),
        ("C:\\case\\work\\2-1.mp4", "old 2-1"),
        ("C:\\case\\incoming\\new.mp4", "new"));

    ExecutePlan(fs, "C:\\case\\work", new InsertOperation(InsertOperationType.InsertVersion, 1, 2, "C:\\case\\incoming\\new.mp4"));

    AssertFiles(fs, "C:\\case\\work", "1-1.mp4", "1-2.mp4", "1-3.mp4", "1-4.mp4", "2-1.mp4");
    AssertFile(fs, "C:\\case\\work\\1-2.mp4", "new");
    AssertFile(fs, "C:\\case\\work\\1-3.mp4", "old 1-2");
    AssertFile(fs, "C:\\case\\work\\1-4.mp4", "old 1-3");
}

static void TestPlaceIntoEmptyCellExecution()
{
    var fs = InMemoryFileSystem.Create(
        ("C:\\case\\work\\1-1.mp4", "old 1-1"),
        ("C:\\case\\work\\2-1.mp4", "old 2-1"),
        ("C:\\case\\incoming\\new.mp4", "new"));

    ExecutePlan(fs, "C:\\case\\work", new InsertOperation(InsertOperationType.PlaceIntoEmptyCell, 2, 2, "C:\\case\\incoming\\new.mp4"));

    AssertFiles(fs, "C:\\case\\work", "1-1.mp4", "2-1.mp4", "2-2.mp4");
    AssertFile(fs, "C:\\case\\work\\2-2.mp4", "new");
}

static RenamePlan CreatePlan(InMemoryFileSystem fs, string folderPath, InsertOperation operation)
{
    var scanResult = new VideoScanner(fs).Scan(folderPath);
    AssertTrue(scanResult.IsSuccess, string.Join(Environment.NewLine, scanResult.Messages));

    var planResult = new RenamePlanner(fs).CreatePlan(folderPath, scanResult.Value!.Videos, operation);
    AssertTrue(planResult.IsSuccess, string.Join(Environment.NewLine, planResult.Messages));
    AssertTrue(planResult.Value!.CanExecute, string.Join(Environment.NewLine, planResult.Value.Errors));
    return planResult.Value;
}

static void ExecutePlan(InMemoryFileSystem fs, string folderPath, InsertOperation operation)
{
    var plan = CreatePlan(fs, folderPath, operation);
    var result = new SafeRenameExecutor(fs).Execute(plan);
    AssertTrue(result.IsSuccess, string.Join(Environment.NewLine, result.Messages));
}

static void AssertTargets(RenamePlan plan, params string[] expectedNames)
{
    var actual = plan.Operations
        .Select(operation => operation.NewName)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var expected = expectedNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    AssertSequence(expected, actual, "目标文件名不匹配");
}

static void AssertFiles(InMemoryFileSystem fs, string folderPath, params string[] expectedNames)
{
    var actual = fs.EnumerateFiles(folderPath)
        .Select(Path.GetFileName)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var expected = expectedNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();
    AssertSequence(expected!, actual!, "文件列表不匹配");
}

static void AssertFile(InMemoryFileSystem fs, string path, string expectedContent)
{
    AssertTrue(fs.TryRead(path, out var actualContent), $"缺少文件：{path}");
    AssertEqual(expectedContent, actualContent, $"文件内容不匹配：{path}");
}

static void AssertSequence<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual, string message)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"{message}。Expected: {string.Join(", ", expected)}; Actual: {string.Join(", ", actual)}");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    AssertTrue(!condition, message);
}

static void AssertEqual<T>(T expected, T actual, string message)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message}。Expected: {expected}; Actual: {actual}");
    }
}

internal sealed class InMemoryFileSystem : IFileSystemService
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public Func<(string Source, string Target), bool>? FailMoveWhen { get; set; }

    public bool FailEnumerate { get; set; }

    public List<(string Source, string Target)> Moves { get; } = [];

    public IEnumerable<string> Files => _files.Keys.ToArray();

    public static InMemoryFileSystem Create(params (string Path, string Content)[] files)
    {
        var fs = new InMemoryFileSystem();
        foreach (var (path, content) in files)
        {
            fs.AddFile(path, content);
        }

        return fs;
    }

    public bool DirectoryExists(string path)
    {
        return _directories.Contains(Normalize(path));
    }

    public bool FileExists(string path)
    {
        return _files.ContainsKey(Normalize(path));
    }

    public IEnumerable<string> EnumerateFiles(string folderPath)
    {
        if (FailEnumerate)
        {
            throw new IOException("模拟枚举失败");
        }

        var folder = Normalize(folderPath);
        return _files.Keys
            .Where(path => string.Equals(Normalize(Path.GetDirectoryName(path) ?? string.Empty), folder, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void MoveFile(string sourcePath, string targetPath)
    {
        var source = Normalize(sourcePath);
        var target = Normalize(targetPath);
        Moves.Add((source, target));

        if (FailMoveWhen?.Invoke((source, target)) == true)
        {
            throw new IOException($"模拟移动失败：{Path.GetFileName(source)}");
        }

        if (!_files.TryGetValue(source, out var content))
        {
            throw new FileNotFoundException(source);
        }

        if (_files.ContainsKey(target))
        {
            throw new IOException($"目标已存在：{target}");
        }

        _files.Remove(source);
        AddDirectory(Path.GetDirectoryName(target)!);
        _files[target] = content;
    }

    public void DeleteFile(string path)
    {
        var normalized = Normalize(path);
        Moves.Add((normalized, "<delete>"));
        if (FailMoveWhen?.Invoke((normalized, "<delete>")) == true)
        {
            throw new IOException($"模拟删除失败：{Path.GetFileName(normalized)}");
        }

        if (!_files.Remove(normalized))
        {
            throw new FileNotFoundException(normalized);
        }
    }

    public bool TryRead(string path, out string content)
    {
        return _files.TryGetValue(Normalize(path), out content!);
    }

    private void AddFile(string path, string content)
    {
        var normalized = Normalize(path);
        AddDirectory(Path.GetDirectoryName(normalized)!);
        _files[normalized] = content;
    }

    private void AddDirectory(string path)
    {
        var current = Normalize(path);
        while (!string.IsNullOrWhiteSpace(current))
        {
            _directories.Add(current);
            var parent = Path.GetDirectoryName(current);
            if (string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            current = parent ?? string.Empty;
        }
    }

    private static string Normalize(string path)
    {
        return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
