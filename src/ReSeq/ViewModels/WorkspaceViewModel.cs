using System.Collections.ObjectModel;
using ReSeq.Core.Models;

namespace ReSeq.ViewModels;

public sealed class WorkspaceViewModel : ViewModelBase
{
    private readonly ViewModelFactory _factory;
    private bool _hasFolder;
    private bool _hasVideos;
    private string _emptyTitle = "选择一个视频文件夹开始";
    private string _emptyActionText = "选择";

    public WorkspaceViewModel(ViewModelFactory factory)
    {
        _factory = factory;
        FirstVideoTarget = _factory.CreateFirstVideoTarget();
    }

    public ObservableCollection<int> VersionHeaders { get; } = [];

    public ObservableCollection<GridRowViewModel> Rows { get; } = [];

    public bool HasFolder
    {
        get => _hasFolder;
        private set => SetProperty(ref _hasFolder, value);
    }

    public bool HasVideos
    {
        get => _hasVideos;
        private set => SetProperty(ref _hasVideos, value);
    }

    public bool ShowEmptyState => !HasFolder || !HasVideos;

    public string EmptyTitle
    {
        get => _emptyTitle;
        private set => SetProperty(ref _emptyTitle, value);
    }

    public string EmptyActionText
    {
        get => _emptyActionText;
        private set => SetProperty(ref _emptyActionText, value);
    }

    public DropTargetViewModel FirstVideoTarget { get; }

    public void ClearNoFolder()
    {
        HasFolder = false;
        HasVideos = false;
        VersionHeaders.Clear();
        Rows.Clear();
        EmptyTitle = "选择一个视频文件夹开始";
        EmptyActionText = "选择";
        OnPropertyChanged(nameof(ShowEmptyState));
    }

    public void Build(IReadOnlyList<VideoItem> videos)
    {
        HasFolder = true;
        VersionHeaders.Clear();
        Rows.Clear();

        if (videos.Count == 0)
        {
            HasVideos = false;
            EmptyTitle = "拖入第一个视频";
            EmptyActionText = "选择";
            OnPropertyChanged(nameof(ShowEmptyState));
            return;
        }

        HasVideos = true;
        // [DECISION] 额外保留一行一列空目标，让用户不用精确找边缘也能拖入新镜头或新版本。
        var maxX = Math.Max(1, videos.Max(item => item.X)) + 1;
        var maxY = Math.Max(1, videos.Max(item => item.Y)) + 1;
        var lookup = videos
            .GroupBy(item => (item.X, item.Y))
            .ToDictionary(group => group.Key, group => _factory.CreateVideoTile(group.First()));

        for (var y = 1; y <= maxY; y++)
        {
            VersionHeaders.Add(y);
        }

        for (var x = 1; x <= maxX; x++)
        {
            var cells = new List<GridCellViewModel>();
            for (var y = 1; y <= maxY; y++)
            {
                lookup.TryGetValue((x, y), out var video);
                cells.Add(_factory.CreateGridCell(x, y, video));
            }

            Rows.Add(_factory.CreateGridRow(x, cells));
        }

        OnPropertyChanged(nameof(ShowEmptyState));
    }
}
