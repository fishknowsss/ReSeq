using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Win32;
using ReSeq.Core.Models;
using ReSeq.Core.Services;
using ReSeq.Services;
using ReSeq.ViewModels;
using MessageBox = System.Windows.MessageBox;
using WpfBrush = System.Windows.Media.Brush;
using WpfDragEventArgs = System.Windows.DragEventArgs;

namespace ReSeq;

public partial class MainWindow : Window
{
    private const double TileWidth = 204;
    private const double TileHeight = 172;
    private const double ThumbnailWidth = 188;
    private const double ThumbnailHeight = 106;

    private static readonly SolidColorBrush InkBrush = Brush("#17202B");
    private static readonly SolidColorBrush MutedBrush = Brush("#596779");
    private static readonly SolidColorBrush SubtleBrush = Brush("#7B8797");
    private static readonly SolidColorBrush LineBrush = Brush("#D7DEE8");
    private static readonly SolidColorBrush SoftLineBrush = Brush("#E7ECF3");
    private static readonly SolidColorBrush PanelBrush = Brush("#FFFFFF");
    private static readonly SolidColorBrush EmptyBrush = Brush("#F8FAFC");
    private static readonly SolidColorBrush AccentBrush = Brush("#2563EB");
    private static readonly SolidColorBrush AccentSoftBrush = Brush("#E7F0FF");
    private static readonly SolidColorBrush GreenBrush = Brush("#16A34A");

    private readonly VideoScanner _scanner = new();
    private readonly RenamePlanner _planner = new();
    private readonly SafeRenameExecutor _executor = new();
    private readonly ShellThumbnailService _thumbnailService = new();
    private readonly ObservableCollection<string> _previewItems = [];
    private readonly ObservableCollection<string> _logItems = [];

    private string? _currentFolder;
    private ScanResult? _scanResult;
    private RenamePlan? _pendingPlan;
    private Border? _highlightedBorder;
    private WpfBrush? _highlightedBackground;
    private WpfBrush? _highlightedBorderBrush;
    private Thickness _highlightedThickness;

    public MainWindow()
    {
        InitializeComponent();
        PreviewList.ItemsSource = _previewItems;
        LogList.ItemsSource = _logItems;
        ResetSummary();
        BuildWorkspace();
    }

    private void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择视频文件夹"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _currentFolder = dialog.FolderName;
            RefreshCurrentFolder();
        }
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshCurrentFolder();
    }

    private void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        if (_pendingPlan is null)
        {
            AddLog("没有待执行的预览");
            return;
        }

        var answer = MessageBox.Show(
            this,
            "确认执行右侧预览中的重命名？",
            "执行重命名",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.OK)
        {
            return;
        }

        SetStatus("正在执行重命名");
        var result = _executor.Execute(_pendingPlan);
        foreach (var message in result.Messages)
        {
            AddLog(message);
        }

        foreach (var error in result.Errors)
        {
            AddLog(error);
        }

        AddLog(result.Success ? "重命名完成" : "重命名未完成");
        ClearPreview();
        RefreshCurrentFolder();
    }

    private void UndoButton_Click(object sender, RoutedEventArgs e)
    {
        AddLog("暂无可撤销操作，请根据日志核对结果");
    }

    private void RefreshCurrentFolder()
    {
        ClearPreview();
        ResetDropHighlight();

        if (string.IsNullOrWhiteSpace(_currentFolder))
        {
            FolderPathText.Text = "未选择文件夹";
            DropHintText.Text = "选择文件夹后，将视频拖到网格位置";
            AddLog("请先选择文件夹");
            SetStatus("先选择文件夹");
            ResetSummary();
            BuildWorkspace();
            return;
        }

        try
        {
            _scanResult = _scanner.Scan(_currentFolder);
            FolderPathText.Text = _currentFolder;
            DropHintText.Text = "拖入一个视频到网格中的目标位置";

            var shotCount = _scanResult.Videos.Select(item => item.X).Distinct().Count();
            var versionCount = _scanResult.Videos.Select(item => item.Y).Distinct().Count();
            var issueCount = _scanResult.InvalidFiles.Count + _scanResult.DuplicateGroups.Count + _scanResult.TempFiles.Count;
            UpdateSummary(_scanResult.Videos.Count, shotCount, versionCount, issueCount);

            AddLog($"扫描完成：{_scanResult.Videos.Count} 个有效视频");
            foreach (var invalid in _scanResult.InvalidFiles)
            {
                AddLog($"非法文件：{Path.GetFileName(invalid.FilePath)}，{invalid.Reason}");
            }

            foreach (var duplicate in _scanResult.DuplicateGroups)
            {
                AddLog($"重复编号：{duplicate.Number}，请先手动处理");
            }

            foreach (var tempFile in _scanResult.TempFiles)
            {
                AddLog($"发现临时文件：{Path.GetFileName(tempFile)}");
            }

            if (_scanResult.Videos.Count == 0)
            {
                AddLog("没有符合规则的视频，可拖入一个视频生成 1-1");
            }

            SetStatus(issueCount == 0 ? "就绪" : "存在需要处理的问题");
            BuildWorkspace();
        }
        catch (Exception ex)
        {
            _scanResult = null;
            AddLog($"扫描失败：{ex.Message}");
            SetStatus("扫描失败");
            ResetSummary();
            BuildWorkspace();
        }
    }

    private void BuildWorkspace()
    {
        WorkspaceGrid.Children.Clear();
        WorkspaceGrid.RowDefinitions.Clear();
        WorkspaceGrid.ColumnDefinitions.Clear();

        if (_currentFolder is null)
        {
            AddMessagePanel("选择一个视频文件夹开始");
            return;
        }

        IReadOnlyList<VideoItem> videos = _scanResult?.Videos ?? Array.Empty<VideoItem>();
        if (videos.Count == 0)
        {
            AddEmptyStartPanel();
            return;
        }

        var maxX = Math.Max(1, videos.Max(item => item.X)) + 1;
        var maxY = Math.Max(1, videos.Max(item => item.Y)) + 1;
        var lookup = videos
            .GroupBy(item => (item.X, item.Y))
            .ToDictionary(group => group.Key, group => group.First());

        WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
        for (var y = 1; y <= maxY; y++)
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TileWidth + 16) });
        }

        WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
        for (var x = 1; x <= maxX; x++)
        {
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(18) });
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TileHeight + 16) });
        }

        AddCornerHeader();
        for (var y = 1; y <= maxY; y++)
        {
            AddHeaderCell($"Y={y}", 0, CellColumn(y));
        }

        for (var x = 1; x <= maxX; x++)
        {
            var separatorRow = SeparatorRow(x);
            var dataRow = DataRow(x);
            AddHorizontalInsertLine(x, separatorRow);
            AddRowHeader(x, dataRow);

            for (var y = 1; y <= maxY; y++)
            {
                AddVerticalInsertLine(x, y, dataRow, SeparatorColumn(y));

                if (lookup.TryGetValue((x, y), out var item))
                {
                    AddVideoCell(item, dataRow, CellColumn(y));
                }
                else
                {
                    AddEmptyCell(x, y, dataRow, CellColumn(y));
                }
            }
        }
    }

    private void AddMessagePanel(string text)
    {
        WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        WorkspaceGrid.Children.Add(new Border
        {
            Width = 520,
            Height = 260,
            Background = PanelBrush,
            BorderBrush = LineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(18),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 18,
                Foreground = MutedBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        });
    }

    private void AddEmptyStartPanel()
    {
        WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var target = new DropTarget(DropTargetKind.EmptyCell, 1, 1, null);
        var border = CreateDropBorder(target);
        border.Width = 560;
        border.Height = 320;
        border.Background = PanelBrush;
        border.BorderBrush = LineBrush;
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(20);
        border.Effect = new DropShadowEffect
        {
            BlurRadius = 24,
            ShadowDepth = 8,
            Opacity = 0.08,
            Color = Color.FromRgb(23, 32, 43)
        };

        var panel = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(new TextBlock
        {
            Text = "拖入一个视频",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = InkBrush,
            HorizontalAlignment = HorizontalAlignment.Center
        });
        panel.Children.Add(new TextBlock
        {
            Text = "将命名为 1-1",
            FontSize = 15,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center
        });
        border.Child = panel;
        WorkspaceGrid.Children.Add(border);
    }

    private void AddCornerHeader()
    {
        var block = new TextBlock
        {
            Text = "镜头",
            FontSize = 13,
            Foreground = SubtleBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(block, 0);
        Grid.SetColumn(block, 0);
        WorkspaceGrid.Children.Add(block);
    }

    private void AddHeaderCell(string text, int row, int column)
    {
        var border = new Border
        {
            Height = 30,
            MinWidth = 78,
            Background = PanelBrush,
            BorderBrush = SoftLineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Child = new TextBlock
            {
                Text = text,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        WorkspaceGrid.Children.Add(border);
    }

    private void AddRowHeader(int x, int row)
    {
        var border = new Border
        {
            Width = 62,
            Height = 40,
            Background = PanelBrush,
            BorderBrush = SoftLineBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(999),
            Child = new TextBlock
            {
                Text = $"X={x}",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = InkBrush,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);
        WorkspaceGrid.Children.Add(border);
    }

    private void AddHorizontalInsertLine(int targetX, int row)
    {
        var target = new DropTarget(DropTargetKind.ShotRow, targetX, 1, null);
        var border = CreateDropBorder(target);
        border.Background = Brushes.Transparent;
        border.BorderThickness = new Thickness(0);
        border.Child = new Border
        {
            Height = 3,
            Margin = new Thickness(8, 7, 8, 7),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(3)
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, 0);
        Grid.SetColumnSpan(border, WorkspaceGrid.ColumnDefinitions.Count);
        WorkspaceGrid.Children.Add(border);
    }

    private void AddVerticalInsertLine(int targetX, int targetY, int row, int column)
    {
        var target = new DropTarget(DropTargetKind.Version, targetX, targetY, null);
        var border = CreateDropBorder(target);
        border.Background = Brushes.Transparent;
        border.BorderThickness = new Thickness(0);
        border.Child = new Border
        {
            Width = 3,
            Margin = new Thickness(7, 8, 7, 8),
            Background = Brushes.Transparent,
            CornerRadius = new CornerRadius(3)
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        WorkspaceGrid.Children.Add(border);
    }

    private void AddVideoCell(VideoItem item, int row, int column)
    {
        var tile = new VideoTileViewModel(item, _thumbnailService.DefaultThumbnail);
        var target = new DropTarget(DropTargetKind.ExistingVideo, item.X, item.Y, item);
        var border = CreateDropBorder(target);
        border.Width = TileWidth;
        border.Height = TileHeight;
        border.Background = PanelBrush;
        border.BorderBrush = SoftLineBrush;
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(14);
        border.Padding = new Thickness(8);
        border.Effect = new DropShadowEffect
        {
            BlurRadius = 18,
            ShadowDepth = 6,
            Opacity = 0.06,
            Color = Color.FromRgb(23, 32, 43)
        };
        border.Child = CreateVideoCard(tile);
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        WorkspaceGrid.Children.Add(border);

        _ = LoadThumbnailAsync(tile);
    }

    private UIElement CreateVideoCard(VideoTileViewModel tile)
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(ThumbnailHeight) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var image = new Image
        {
            Width = ThumbnailWidth,
            Height = ThumbnailHeight,
            Stretch = Stretch.UniformToFill,
            Source = tile.Thumbnail
        };

        tile.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(VideoTileViewModel.Thumbnail))
            {
                image.Source = tile.Thumbnail;
            }
        };

        var thumbHost = new Border
        {
            Width = ThumbnailWidth,
            Height = ThumbnailHeight,
            Background = Brush("#E8EEF5"),
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            Child = image
        };
        Grid.SetRow(thumbHost, 0);
        root.Children.Add(thumbHost);

        var badge = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = Brush("#17202B"),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(8, 3, 8, 4),
            Margin = new Thickness(8),
            Child = new TextBlock
            {
                Text = tile.Item.Number,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            }
        };
        Grid.SetRow(badge, 0);
        root.Children.Add(badge);

        var meta = new Grid
        {
            Margin = new Thickness(2, 8, 2, 0)
        };
        meta.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        meta.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        meta.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        meta.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var fileName = new TextBlock
        {
            Text = tile.FileName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = InkBrush,
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        Grid.SetRow(fileName, 0);
        Grid.SetColumn(fileName, 0);
        meta.Children.Add(fileName);

        var extension = new Border
        {
            Background = Brush("#EEF2F6"),
            CornerRadius = new CornerRadius(999),
            Padding = new Thickness(7, 2, 7, 3),
            Margin = new Thickness(8, 0, 0, 0),
            Child = new TextBlock
            {
                Text = tile.ExtensionText.TrimStart('.').ToUpperInvariant(),
                Foreground = MutedBrush,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            }
        };
        Grid.SetRow(extension, 0);
        Grid.SetColumn(extension, 1);
        meta.Children.Add(extension);

        var number = new TextBlock
        {
            Text = tile.NumberText,
            FontSize = 12,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 4, 0, 0)
        };
        Grid.SetRow(number, 1);
        Grid.SetColumn(number, 0);
        Grid.SetColumnSpan(number, 2);
        meta.Children.Add(number);

        Grid.SetRow(meta, 1);
        root.Children.Add(meta);
        return root;
    }

    private void AddEmptyCell(int x, int y, int row, int column)
    {
        var target = new DropTarget(DropTargetKind.EmptyCell, x, y, null);
        var border = CreateDropBorder(target);
        border.Width = TileWidth;
        border.Height = TileHeight;
        border.Background = EmptyBrush;
        border.BorderBrush = SoftLineBrush;
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(14);
        border.Child = new Border
        {
            Margin = new Thickness(12),
            BorderBrush = Brush("#DDE5EF"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Opacity = 0.55
        };
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        WorkspaceGrid.Children.Add(border);
    }

    private Border CreateDropBorder(DropTarget target)
    {
        var border = new Border
        {
            Tag = target,
            AllowDrop = true
        };
        border.DragOver += DropTarget_DragOver;
        border.DragLeave += DropTarget_DragLeave;
        border.Drop += DropTarget_Drop;
        return border;
    }

    private async Task LoadThumbnailAsync(VideoTileViewModel tile)
    {
        tile.Thumbnail = await _thumbnailService.GetThumbnailAsync(tile.Item.FilePath);
    }

    private void DropTarget_DragOver(object sender, WpfDragEventArgs e)
    {
        if (sender is not Border border || border.Tag is not DropTarget target)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (!CanAcceptDrop(e, out _, out var error))
        {
            e.Effects = DragDropEffects.None;
            SetDropHint(error, isError: true);
            e.Handled = true;
            return;
        }

        HighlightBorder(border, target);
        SetDropHint(GetDropHint(target));
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    private void DropTarget_DragLeave(object sender, WpfDragEventArgs e)
    {
        ResetDropHighlight();
    }

    private void DropTarget_Drop(object sender, WpfDragEventArgs e)
    {
        ResetDropHighlight();

        if (sender is not Border border || border.Tag is not DropTarget target)
        {
            return;
        }

        if (!CanAcceptDrop(e, out var filePath, out var error))
        {
            AddLog(error);
            return;
        }

        if (target.Kind == DropTargetKind.ExistingVideo)
        {
            ShowExistingVideoMenu(border, target, filePath);
            return;
        }

        CreatePreview(target, filePath);
    }

    private void WorkspaceGrid_DragOver(object sender, WpfDragEventArgs e)
    {
        if (!CanAcceptDrop(e, out _, out var error))
        {
            e.Effects = DragDropEffects.None;
            SetDropHint(error, isError: true);
            e.Handled = true;
        }
    }

    private void WorkspaceGrid_Drop(object sender, WpfDragEventArgs e)
    {
        if (_scanResult?.Videos.Count == 0 && CanAcceptDrop(e, out var filePath, out _))
        {
            CreatePreview(new DropTarget(DropTargetKind.EmptyCell, 1, 1, null), filePath);
        }
    }

    private bool CanAcceptDrop(WpfDragEventArgs e, out string filePath, out string error)
    {
        filePath = string.Empty;
        error = string.Empty;

        if (_currentFolder is null)
        {
            error = "先选择文件夹";
            return false;
        }

        if (_scanResult?.HasBlockingIssues == true)
        {
            error = "请先处理重复编号或临时文件";
            return false;
        }

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            error = "请拖入一个视频文件";
            return false;
        }

        var files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
        if (files is null || files.Length == 0)
        {
            error = "请拖入一个视频文件";
            return false;
        }

        if (files.Length > 1)
        {
            error = "暂不支持批量拖入";
            return false;
        }

        filePath = files[0];
        if (!File.Exists(filePath))
        {
            error = "拖入文件不存在";
            return false;
        }

        if (!VideoScanner.IsSupportedVideo(filePath))
        {
            error = "只支持 mp4、mov、avi、mkv、wmv";
            return false;
        }

        return true;
    }

    private void ShowExistingVideoMenu(Border border, DropTarget target, string filePath)
    {
        var menu = new ContextMenu
        {
            FontSize = 14
        };
        var before = new MenuItem { Header = "放前面" };
        var after = new MenuItem { Header = "放后面" };
        var cancel = new MenuItem { Header = "取消" };

        before.Click += (_, _) => CreatePreview(new DropTarget(DropTargetKind.Version, target.X, target.Y, null), filePath);
        after.Click += (_, _) => CreatePreview(new DropTarget(DropTargetKind.Version, target.X, target.Y + 1, null), filePath);
        cancel.Click += (_, _) => AddLog("已取消拖入");

        menu.Items.Add(before);
        menu.Items.Add(after);
        menu.Items.Add(cancel);
        border.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void CreatePreview(DropTarget target, string filePath)
    {
        if (_currentFolder is null)
        {
            return;
        }

        var operationType = target.Kind switch
        {
            DropTargetKind.ShotRow => InsertOperationType.InsertShotRow,
            DropTargetKind.Version => InsertOperationType.InsertVersion,
            _ => InsertOperationType.PlaceIntoEmptyCell
        };

        var operation = new InsertOperation(operationType, target.X, target.Y, filePath);
        var plan = _planner.CreatePlan(_currentFolder, _scanResult?.Videos ?? Array.Empty<VideoItem>(), operation);
        SetPreview(plan);
    }

    private void SetPreview(RenamePlan plan)
    {
        _pendingPlan = plan;
        _previewItems.Clear();

        foreach (var error in plan.Errors)
        {
            AddLog($"预览失败：{error}");
        }

        foreach (var warning in plan.Warnings)
        {
            AddLog(warning);
        }

        foreach (var operation in plan.Operations)
        {
            _previewItems.Add($"{operation.OldName}  ->  {operation.NewName}");
        }

        if (_previewItems.Count == 0 && plan.Errors.Count == 0)
        {
            _previewItems.Add("没有需要重命名的文件");
        }

        ExecuteButton.IsEnabled = plan.CanExecute;
        PlanStatusText.Text = plan.CanExecute ? $"{plan.Operations.Count} 项" : "不可执行";
        SetStatus(plan.CanExecute ? "已生成预览，请确认后执行" : "预览不可执行");
        AddLog(plan.CanExecute ? "已生成预览，请确认后执行" : "预览不可执行");
    }

    private void ClearPreview()
    {
        _pendingPlan = null;
        _previewItems.Clear();
        ExecuteButton.IsEnabled = false;
        PlanStatusText.Text = "无计划";
    }

    private void HighlightBorder(Border border, DropTarget target)
    {
        if (_highlightedBorder != border)
        {
            ResetDropHighlight();
            _highlightedBorder = border;
            _highlightedBackground = border.Background;
            _highlightedBorderBrush = border.BorderBrush;
            _highlightedThickness = border.BorderThickness;
        }

        if (target.Kind == DropTargetKind.ShotRow && border.Child is Border horizontalLine)
        {
            horizontalLine.Background = AccentBrush;
            border.Background = AccentSoftBrush;
        }
        else if (target.Kind == DropTargetKind.Version && border.Child is Border verticalLine)
        {
            verticalLine.Background = AccentBrush;
            border.Background = AccentSoftBrush;
        }
        else
        {
            border.Background = AccentSoftBrush;
            border.BorderBrush = AccentBrush;
            border.BorderThickness = new Thickness(2);
        }
    }

    private void ResetDropHighlight()
    {
        if (_highlightedBorder is null)
        {
            return;
        }

        if (_highlightedBorder.Tag is DropTarget { Kind: DropTargetKind.ShotRow or DropTargetKind.Version } &&
            _highlightedBorder.Child is Border line)
        {
            line.Background = Brushes.Transparent;
        }

        _highlightedBorder.Background = _highlightedBackground;
        _highlightedBorder.BorderBrush = _highlightedBorderBrush;
        _highlightedBorder.BorderThickness = _highlightedThickness;
        _highlightedBorder = null;
    }

    private string GetDropHint(DropTarget target)
    {
        return target.Kind switch
        {
            DropTargetKind.ShotRow => $"插入新镜头到 X={target.X} 之前",
            DropTargetKind.Version => $"插入为 X={target.X}, Y={target.Y} 的新版本",
            DropTargetKind.EmptyCell => $"命名为 {target.X}-{target.Y}",
            DropTargetKind.ExistingVideo => "选择放前面或放后面",
            _ => "拖入一个视频到网格中"
        };
    }

    private void SetDropHint(string message, bool isError = false)
    {
        DropHintText.Text = message;
        DropHintText.Foreground = isError ? Brush("#B42318") : InkBrush;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void AddLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        _logItems.Add(line);
        LogStatusText.Text = $"{_logItems.Count} 条";
        LogList.ScrollIntoView(line);
    }

    private void UpdateSummary(int videoCount, int shotCount, int versionCount, int issueCount)
    {
        VideoCountText.Text = $"视频 {videoCount}";
        ShotCountText.Text = $"镜头 {shotCount}";
        VersionCountText.Text = $"版本 {versionCount}";
        IssueCountText.Text = $"问题 {issueCount}";
        IssueCountText.Foreground = issueCount > 0 ? Brush("#B42318") : MutedBrush;
    }

    private void ResetSummary()
    {
        UpdateSummary(0, 0, 0, 0);
        PlanStatusText.Text = "无计划";
        LogStatusText.Text = "就绪";
    }

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static int SeparatorColumn(int y) => 1 + (y - 1) * 2;

    private static int CellColumn(int y) => SeparatorColumn(y) + 1;

    private static int SeparatorRow(int x) => 1 + (x - 1) * 2;

    private static int DataRow(int x) => SeparatorRow(x) + 1;

    private sealed record DropTarget(DropTargetKind Kind, int X, int Y, VideoItem? Video);

    private enum DropTargetKind
    {
        ShotRow,
        Version,
        EmptyCell,
        ExistingVideo
    }
}
