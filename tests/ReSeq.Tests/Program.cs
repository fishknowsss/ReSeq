using ReSeq.Core.Models;
using ReSeq.Core.Services;

var tests = new (string Name, Action Body)[]
{
    ("测试 A：插入新镜头行", TestInsertShotRow),
    ("测试 B：插入同镜头新版本", TestInsertVersion),
    ("测试 C：拖到空单元格", TestPlaceIntoEmptyCell)
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

static void TestInsertShotRow()
{
    using var fixture = TestFixture.Create(
        ("1-1.mp4", "old 1-1"),
        ("2-1.mp4", "old 2-1"),
        ("2-2.mp4", "old 2-2"),
        ("3-1.mp4", "old 3-1"));

    var newVideo = fixture.CreateIncoming("new.mp4", "new");
    var result = ExecutePlan(fixture.WorkFolder, new InsertOperation(InsertOperationType.InsertShotRow, 2, 1, newVideo));

    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    AssertFiles(fixture.WorkFolder, "1-1.mp4", "2-1.mp4", "3-1.mp4", "3-2.mp4", "4-1.mp4");
    AssertContent(fixture.WorkFolder, "1-1.mp4", "old 1-1");
    AssertContent(fixture.WorkFolder, "2-1.mp4", "new");
    AssertContent(fixture.WorkFolder, "3-1.mp4", "old 2-1");
    AssertContent(fixture.WorkFolder, "3-2.mp4", "old 2-2");
    AssertContent(fixture.WorkFolder, "4-1.mp4", "old 3-1");
}

static void TestInsertVersion()
{
    using var fixture = TestFixture.Create(
        ("1-1.mp4", "old 1-1"),
        ("1-2.mp4", "old 1-2"),
        ("1-3.mp4", "old 1-3"),
        ("2-1.mp4", "old 2-1"));

    var newVideo = fixture.CreateIncoming("new.mp4", "new");
    var result = ExecutePlan(fixture.WorkFolder, new InsertOperation(InsertOperationType.InsertVersion, 1, 2, newVideo));

    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    AssertFiles(fixture.WorkFolder, "1-1.mp4", "1-2.mp4", "1-3.mp4", "1-4.mp4", "2-1.mp4");
    AssertContent(fixture.WorkFolder, "1-1.mp4", "old 1-1");
    AssertContent(fixture.WorkFolder, "1-2.mp4", "new");
    AssertContent(fixture.WorkFolder, "1-3.mp4", "old 1-2");
    AssertContent(fixture.WorkFolder, "1-4.mp4", "old 1-3");
    AssertContent(fixture.WorkFolder, "2-1.mp4", "old 2-1");
}

static void TestPlaceIntoEmptyCell()
{
    using var fixture = TestFixture.Create(
        ("1-1.mp4", "old 1-1"),
        ("2-1.mp4", "old 2-1"));

    var newVideo = fixture.CreateIncoming("new.mp4", "new");
    var result = ExecutePlan(fixture.WorkFolder, new InsertOperation(InsertOperationType.PlaceIntoEmptyCell, 2, 2, newVideo));

    AssertTrue(result.Success, string.Join(Environment.NewLine, result.Errors));
    AssertFiles(fixture.WorkFolder, "1-1.mp4", "2-1.mp4", "2-2.mp4");
    AssertContent(fixture.WorkFolder, "1-1.mp4", "old 1-1");
    AssertContent(fixture.WorkFolder, "2-1.mp4", "old 2-1");
    AssertContent(fixture.WorkFolder, "2-2.mp4", "new");
}

static ExecutionResult ExecutePlan(string folderPath, InsertOperation operation)
{
    var scanner = new VideoScanner();
    var scan = scanner.Scan(folderPath);
    var planner = new RenamePlanner();
    var plan = planner.CreatePlan(folderPath, scan.Videos, operation);

    AssertTrue(plan.CanExecute, string.Join(Environment.NewLine, plan.Errors));
    return new SafeRenameExecutor().Execute(plan);
}

static void AssertFiles(string folderPath, params string[] expectedNames)
{
    var actual = Directory.EnumerateFiles(folderPath)
        .Select(Path.GetFileName)
        .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
        .ToArray();
    var expected = expectedNames.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray();

    if (!actual.SequenceEqual(expected, StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException(
            $"文件不匹配。Expected: {string.Join(", ", expected)}; Actual: {string.Join(", ", actual)}");
    }
}

static void AssertContent(string folderPath, string fileName, string expected)
{
    var actual = File.ReadAllText(Path.Combine(folderPath, fileName));
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"{fileName} 内容不匹配。Expected: {expected}; Actual: {actual}");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal sealed class TestFixture : IDisposable
{
    private TestFixture(string rootPath)
    {
        RootPath = rootPath;
        WorkFolder = Path.Combine(rootPath, "work");
        IncomingFolder = Path.Combine(rootPath, "incoming");
        Directory.CreateDirectory(WorkFolder);
        Directory.CreateDirectory(IncomingFolder);
    }

    public string RootPath { get; }

    public string WorkFolder { get; }

    public string IncomingFolder { get; }

    public static TestFixture Create(params (string Name, string Content)[] files)
    {
        var fixture = new TestFixture(Path.Combine(Path.GetTempPath(), "ReSeqTests", Guid.NewGuid().ToString("N")));
        foreach (var (name, content) in files)
        {
            File.WriteAllText(Path.Combine(fixture.WorkFolder, name), content);
        }

        return fixture;
    }

    public string CreateIncoming(string name, string content)
    {
        var path = Path.Combine(IncomingFolder, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(RootPath))
        {
            Directory.Delete(RootPath, recursive: true);
        }
    }
}
