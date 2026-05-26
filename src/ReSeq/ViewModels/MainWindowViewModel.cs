using System.Collections.ObjectModel;
using System.IO;
using ReSeq.Core.Models;
using ReSeq.Core.Services;

namespace ReSeq.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private readonly VideoScanner _scanner;
    private readonly RenamePlanner _planner;
    private readonly SafeRenameExecutor _executor;
    private readonly ViewModelFactory _factory;

    private string? _currentFolderPath;
    private IReadOnlyList<VideoItem> _videos = Array.Empty<VideoItem>();
    private string _folderDisplayText = "选择视频文件夹开始";
    private string _dropHint = "选择文件夹后，将视频拖到网格位置";
    private string _statusText = "就绪";
    private string _progressText = string.Empty;
    private bool _isBusy;
    private int _videoCount;
    private int _shotCount;
    private int _versionCount;
    private int _issueCount;
    private string _issueSummary = string.Empty;

    public MainWindowViewModel(
        VideoScanner scanner,
        RenamePlanner planner,
        SafeRenameExecutor executor,
        ViewModelFactory factory,
        WorkspaceViewModel workspace,
        RenamePlanViewModel pendingPlan)
    {
        _scanner = scanner;
        _planner = planner;
        _executor = executor;
        _factory = factory;
        Workspace = workspace;
        PendingPlan = pendingPlan;
    }

    public WorkspaceViewModel Workspace { get; }

    public RenamePlanViewModel PendingPlan { get; }

    public ObservableCollection<LogEntryViewModel> Logs { get; } = [];

    public string? CurrentFolderPath
    {
        get => _currentFolderPath;
        private set
        {
            if (SetProperty(ref _currentFolderPath, value))
            {
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
    }

    public string FolderDisplayText
    {
        get => _folderDisplayText;
        private set => SetProperty(ref _folderDisplayText, value);
    }

    public string DropHint
    {
        get => _dropHint;
        private set => SetProperty(ref _dropHint, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ProgressText
    {
        get => _progressText;
        private set => SetProperty(ref _progressText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanExecutePlan));
                OnPropertyChanged(nameof(CanRefresh));
            }
        }
    }

    public int VideoCount
    {
        get => _videoCount;
        private set => SetProperty(ref _videoCount, value);
    }

    public int ShotCount
    {
        get => _shotCount;
        private set => SetProperty(ref _shotCount, value);
    }

    public int VersionCount
    {
        get => _versionCount;
        private set => SetProperty(ref _versionCount, value);
    }

    public int IssueCount
    {
        get => _issueCount;
        private set
        {
            if (SetProperty(ref _issueCount, value))
            {
                OnPropertyChanged(nameof(HasIssues));
            }
        }
    }

    public bool HasIssues => IssueCount > 0;

    public string IssueSummary
    {
        get => _issueSummary;
        private set
        {
            if (SetProperty(ref _issueSummary, value))
            {
                OnPropertyChanged(nameof(HasIssueSummary));
            }
        }
    }

    public bool HasIssueSummary => !string.IsNullOrWhiteSpace(IssueSummary);

    public bool CanExecutePlan => PendingPlan.CanExecute && !IsBusy;

    public bool CanRefresh => !string.IsNullOrWhiteSpace(CurrentFolderPath) && !IsBusy;

    public bool HasLogs => Logs.Count > 0;

    public async Task LoadFolderAsync(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            ReportError("文件夹路径为空");
            return;
        }

        CurrentFolderPath = folderPath;
        FolderDisplayText = folderPath;
        await RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        PendingPlan.Clear();
        OnPropertyChanged(nameof(CanExecutePlan));

        if (string.IsNullOrWhiteSpace(CurrentFolderPath))
        {
            ResetNoFolder();
            AddLog(LogLevel.Info, "请先选择文件夹");
            return;
        }

        IsBusy = true;
        StatusText = "扫描中";
        try
        {
            await Task.Yield();
            var scanResult = _scanner.Scan(CurrentFolderPath);
            if (!scanResult.IsSuccess || scanResult.Value is null)
            {
                Workspace.ClearNoFolder();
                AddLog(LogLevel.Error, scanResult.Messages.FirstOrDefault() ?? "扫描失败");
                StatusText = "扫描失败";
                return;
            }

            ApplyScanResult(scanResult.Value);
        }
        catch (Exception ex)
        {
            Workspace.ClearNoFolder();
            AddLog(LogLevel.Error, $"扫描失败：{ex.Message}");
            StatusText = "扫描失败";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool CanAcceptDrop(IReadOnlyList<string> files, out string filePath, out string message)
    {
        filePath = string.Empty;
        message = string.Empty;

        if (string.IsNullOrWhiteSpace(CurrentFolderPath))
        {
            message = "先选择文件夹";
            return false;
        }

        if (HasIssueSummary)
        {
            message = "请先处理重复编号或临时文件";
            return false;
        }

        if (files.Count == 0)
        {
            message = "请拖入一个视频文件";
            return false;
        }

        if (files.Count > 1)
        {
            message = "暂不支持批量拖入";
            return false;
        }

        filePath = files[0];
        if (!VideoScanner.IsSupportedVideo(filePath))
        {
            message = "只支持 mp4、mov、avi、mkv、wmv";
            return false;
        }

        return true;
    }

    public bool CanAcceptFolderDrop(IReadOnlyList<string> folders, out string folderPath, out string message)
    {
        folderPath = string.Empty;
        message = string.Empty;

        if (folders.Count == 0)
        {
            message = "拖入视频文件夹开始";
            return false;
        }

        if (folders.Count > 1)
        {
            message = "一次只支持一个文件夹";
            return false;
        }

        folderPath = folders[0];
        return true;
    }

    public bool CanAcceptInternalDrop(VideoTileViewModel? video, DropTargetViewModel? target, out string message)
    {
        message = string.Empty;
        if (video is null || target is null)
        {
            message = "请选择目标位置";
            return false;
        }

        if (string.IsNullOrWhiteSpace(CurrentFolderPath))
        {
            message = "先选择文件夹";
            return false;
        }

        if (HasIssueSummary)
        {
            message = "请先处理重复编号或临时文件";
            return false;
        }

        return true;
    }

    public void UpdateDropHint(DropTargetViewModel target)
    {
        DropHint = target.Hint;
    }

    public void ResetDropHint()
    {
        DropHint = CurrentFolderPath is null ? "选择文件夹后，将视频拖到网格位置" : "拖入一个视频到网格中的目标位置";
    }

    public void ShowDropMessage(string message)
    {
        DropHint = message;
    }

    public void CreateRejectedDropPreview(string message)
    {
        PendingPlan.SetError(message);
        AddLog(LogLevel.Warning, message);
        StatusText = "拖入失败";
        OnPropertyChanged(nameof(CanExecutePlan));
    }

    public void CreatePreview(DropTargetViewModel target, string filePath)
    {
        if (CurrentFolderPath is null)
        {
            return;
        }

        var operationType = target.Kind switch
        {
            DropTargetKind.ShotRow => InsertOperationType.InsertShotRow,
            DropTargetKind.Version => InsertOperationType.InsertVersion,
            _ => InsertOperationType.PlaceIntoEmptyCell
        };

        var result = _planner.CreatePlan(CurrentFolderPath, _videos, new InsertOperation(operationType, target.X, target.Y, filePath));
        if (!result.IsSuccess || result.Value is null)
        {
            var message = result.Messages.FirstOrDefault() ?? "预览不可执行";
            PendingPlan.SetError(message);
            AddLog(LogLevel.Error, $"预览失败：{message}");
            StatusText = "预览不可执行";
            OnPropertyChanged(nameof(CanExecutePlan));
            return;
        }

        PendingPlan.SetPlan(result.Value);
        AddLog(LogLevel.Info, $"已生成预览：{result.Value.Operations.Count} 项");
        StatusText = "预览已生成";
        OnPropertyChanged(nameof(CanExecutePlan));
    }

    public void CreateMovePreview(VideoTileViewModel video, DropTargetViewModel target)
    {
        if (CurrentFolderPath is null)
        {
            return;
        }

        var operationType = target.Kind switch
        {
            DropTargetKind.ShotRow => InsertOperationType.InsertShotRow,
            DropTargetKind.Version => InsertOperationType.InsertVersion,
            _ => InsertOperationType.PlaceIntoEmptyCell
        };

        var result = _planner.CreateMoveExistingPlan(CurrentFolderPath, _videos, video.Item, operationType, target.X, target.Y);
        ApplyPreviewResult(result, "已生成移动预览");
    }

    public void CreateDeletePreview(VideoTileViewModel video)
    {
        if (CurrentFolderPath is null)
        {
            return;
        }

        var result = _planner.CreateDeletePlan(CurrentFolderPath, _videos, video.Item);
        ApplyPreviewResult(result, "已生成删除预览");
    }

    public DropTargetViewModel CreateVersionDropTarget(int x, int y)
    {
        return _factory.CreateVersionTarget(x, y);
    }

    public void ClearPreview()
    {
        PendingPlan.Clear();
        StatusText = "就绪";
        OnPropertyChanged(nameof(CanExecutePlan));
    }

    private void ApplyPreviewResult(Result<RenamePlan, ReSeqError> result, string successMessage)
    {
        if (!result.IsSuccess || result.Value is null)
        {
            var message = result.Messages.FirstOrDefault() ?? "预览不可执行";
            PendingPlan.SetError(message);
            AddLog(LogLevel.Error, $"预览失败：{message}");
            StatusText = "预览不可执行";
            OnPropertyChanged(nameof(CanExecutePlan));
            return;
        }

        PendingPlan.SetPlan(result.Value);
        AddLog(LogLevel.Info, $"{successMessage}：{result.Value.Operations.Count} 项");
        StatusText = "预览已生成";
        OnPropertyChanged(nameof(CanExecutePlan));
    }

    public void ClearLogs()
    {
        Logs.Clear();
        OnPropertyChanged(nameof(HasLogs));
    }

    public void ReportError(string message, Exception? exception = null)
    {
        var detail = exception is null ? message : $"{message}：{exception.Message}";
        AddLog(LogLevel.Error, detail);
        StatusText = "发生错误";
    }

    public async Task ExecutePendingPlanAsync()
    {
        if (PendingPlan.Plan is null || !PendingPlan.CanExecute)
        {
            AddLog(LogLevel.Warning, "没有可执行的重命名计划");
            return;
        }

        IsBusy = true;
        StatusText = "执行中";
        ProgressText = string.Empty;
        var progress = new Progress<RenameProgress>(item =>
        {
            StatusText = item.Message;
            ProgressText = $"{item.Completed}/{item.Total}";
        });
        try
        {
            await Task.Yield();
            var result = _executor.Execute(PendingPlan.Plan, progress);
            if (result.IsSuccess && result.Value is not null)
            {
                foreach (var message in result.Value.Messages)
                {
                    AddLog(LogLevel.Success, message);
                }

                AddLog(LogLevel.Success, "重命名完成");
                await RefreshAsync();
                return;
            }

            foreach (var message in result.Messages)
            {
                AddLog(LogLevel.Error, message);
            }

            StatusText = "执行失败";
        }
        finally
        {
            ProgressText = string.Empty;
            IsBusy = false;
            OnPropertyChanged(nameof(CanExecutePlan));
        }
    }

    private void ApplyScanResult(ScanResult scan)
    {
        _videos = scan.Videos;
        Workspace.Build(scan.Videos);

        VideoCount = scan.Videos.Count;
        ShotCount = scan.Videos.Select(item => item.X).Distinct().Count();
        VersionCount = scan.Videos.Select(item => item.Y).Distinct().Count();
        IssueCount = scan.Issues.Count;
        IssueSummary = string.Join("；", scan.Issues.Where(issue => issue.IsBlocking).Select(issue => Path.GetFileName(issue.FilePath)).Distinct());

        AddLog(LogLevel.Success, $"扫描完成：{scan.Videos.Count} 个有效视频");
        foreach (var issue in scan.Issues)
        {
            var level = issue.IsBlocking ? LogLevel.Warning : LogLevel.Info;
            AddLog(level, $"{issue.Message}：{Path.GetFileName(issue.FilePath)}");
        }

        if (scan.Videos.Count == 0)
        {
            AddLog(LogLevel.Info, "没有符合规则的视频，可拖入一个视频生成 1-1");
        }

        ResetDropHint();
        StatusText = scan.HasBlockingIssues ? "有问题" : "就绪";
    }

    private void ResetNoFolder()
    {
        CurrentFolderPath = null;
        FolderDisplayText = "选择视频文件夹开始";
        _videos = Array.Empty<VideoItem>();
        Workspace.ClearNoFolder();
        VideoCount = 0;
        ShotCount = 0;
        VersionCount = 0;
        IssueCount = 0;
        IssueSummary = string.Empty;
        DropHint = "选择文件夹后，将视频拖到网格位置";
        StatusText = "就绪";
    }

    private void AddLog(LogLevel level, string message)
    {
        Logs.Add(_factory.CreateLogEntry(level, message));
        OnPropertyChanged(nameof(HasLogs));
    }
}
