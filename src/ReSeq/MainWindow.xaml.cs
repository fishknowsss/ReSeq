using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private const double TileWidth = 184;
    private const double TileHeight = 150;

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
            "确认执行预览中的重命名？",
            "执行重命名",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (answer != MessageBoxResult.OK)
        {
            return;
        }

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
        AddLog("暂无可撤销操作；请根据日志核对结果");
    }

    private void RefreshCurrentFolder()
    {
        ClearPreview();
        ResetDropHighlight();

        if (string.IsNullOrWhiteSpace(_currentFolder))
        {
            FolderPathText.Text = "未选择文件夹";
            DropHintText.Text = "先选择文件夹";
            AddLog("请先选择文件夹");
            BuildWorkspace();
            return;
        }

        try
        {
            _scanResult = _scanner.Scan(_currentFolder);
            FolderPathText.Text = _currentFolder;
            DropHintText.Text = "拖入一个视频到网格中";

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

            BuildWorkspace();
        }
        catch (Exception ex)
        {
            _scanResult = null;
            AddLog($"扫描失败：{ex.Message}");
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

        WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(74) });
        for (var y = 1; y <= maxY; y++)
        {
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
            WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(TileWidth + 16) });
        }

        WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });
        for (var x = 1; x <= maxX; x++)
        {
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
            WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(TileHeight + 16) });
        }

        AddHeaderCell("", 0, 0);
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
        WorkspaceGrid.Children.Add(new TextBlock
        {
            Text = text,
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(67, 82, 100)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
    }

    private void AddEmptyStartPanel()
    {
        WorkspaceGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        WorkspaceGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var target = new DropTarget(DropTargetKind.EmptyCell, 1, 1, null);
        var border = CreateDropBorder(target);
        border.MinWidth = 520;
        border.MinHeight = 300;
        border.Background = Brushes.White;
        border.BorderBrush = new SolidColorBrush(Color.FromRgb(216, 222, 232));
        border.BorderThickness = new Thickness(1);
        border.Child = new TextBlock
        {
            Text = "拖入一个视频，命名为 1-1",
            FontSize = 18,
            Foreground = new SolidColorBrush(Color.FromRgb(67, 82, 100)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        WorkspaceGrid.Children.Add(border);
    }

    private void AddHeaderCell(string text, int row, int column)
    {
        var block = new TextBlock
        {
            Text = text,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 36, 42)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        Grid.SetRow(block, row);
        Grid.SetColumn(block, column);
        WorkspaceGrid.Children.Add(block);
    }

    private void AddRowHeader(int x, int row)
    {
        var border = new Border
        {
            Width = 64,
            Height = 42,
            Background = Brushes.Transparent,
            Child = new TextBlock
            {
                Text = $"X={x}",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 36, 42)),
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
            Height = 2,
            Margin = new Thickness(4, 5, 4, 5),
            Background = Brushes.Transparent
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
            Width = 2,
            Margin = new Thickness(5, 8, 5, 8),
            Background = Brushes.Transparent
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
        border.Background = Brushes.White;
        border.BorderBrush = new SolidColorBrush(Color.FromRgb(210, 217, 226));
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(8);
        border.Padding = new Thickness(8);
        border.Child = CreateVideoCard(tile);
        Grid.SetRow(border, row);
        Grid.SetColumn(border, column);
        WorkspaceGrid.Children.Add(border);

        _ = LoadThumbnailAsync(tile);
    }

    private UIElement CreateVideoCard(VideoTileViewModel tile)
    {
        var panel = new StackPanel();
        var image = new Image
        {
            Width = 168,
            Height = 94,
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

        panel.Children.Add(new Border
        {
            Width = 168,
            Height = 94,
            Background = new SolidColorBrush(Color.FromRgb(232, 237, 244)),
            ClipToBounds = true,
            Child = image
        });
        panel.Children.Add(new TextBlock
        {
            Text = tile.FileName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Color.FromRgb(32, 36, 42)),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(0, 8, 0, 2)
        });
        panel.Children.Add(new TextBlock
        {
            Text = tile.NumberText,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(67, 82, 100))
        });
        panel.Children.Add(new TextBlock
        {
            Text = tile.ExtensionText,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.FromRgb(67, 82, 100))
        });
        return panel;
    }

    private void AddEmptyCell(int x, int y, int row, int column)
    {
        var target = new DropTarget(DropTargetKind.EmptyCell, x, y, null);
        var border = CreateDropBorder(target);
        border.Width = TileWidth;
        border.Height = TileHeight;
        border.Background = new SolidColorBrush(Color.FromRgb(251, 252, 253));
        border.BorderBrush = new SolidColorBrush(Color.FromRgb(226, 231, 238));
        border.BorderThickness = new Thickness(1);
        border.CornerRadius = new CornerRadius(8);
        border.Child = new TextBlock
        {
            Text = "空",
            FontSize = 15,
            Foreground = new SolidColorBrush(Color.FromRgb(122, 132, 148)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
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
            DropHintText.Text = error;
            e.Handled = true;
            return;
        }

        HighlightBorder(border, target);
        DropHintText.Text = GetDropHint(target);
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
            DropHintText.Text = error;
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
        var menu = new ContextMenu();
        var before = new MenuItem { Header = "放到前面" };
        var after = new MenuItem { Header = "放到后面" };
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
            _previewItems.Add($"{operation.OldName} -> {operation.NewName}");
        }

        if (_previewItems.Count == 0 && plan.Errors.Count == 0)
        {
            _previewItems.Add("没有需要重命名的文件");
        }

        ExecuteButton.IsEnabled = plan.CanExecute;
        AddLog(plan.CanExecute ? "已生成预览，请确认后执行" : "预览不可执行");
    }

    private void ClearPreview()
    {
        _pendingPlan = null;
        _previewItems.Clear();
        ExecuteButton.IsEnabled = false;
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

        var blue = new SolidColorBrush(Color.FromRgb(47, 111, 237));
        var paleBlue = new SolidColorBrush(Color.FromArgb(40, 47, 111, 237));

        if (target.Kind == DropTargetKind.ShotRow && border.Child is Border horizontalLine)
        {
            horizontalLine.Background = blue;
            border.Background = paleBlue;
        }
        else if (target.Kind == DropTargetKind.Version && border.Child is Border verticalLine)
        {
            verticalLine.Background = blue;
            border.Background = paleBlue;
        }
        else
        {
            border.Background = paleBlue;
            border.BorderBrush = blue;
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
            DropTargetKind.ExistingVideo => "选择放到前面或后面",
            _ => "拖入一个视频到网格中"
        };
    }

    private void AddLog(string message)
    {
        var line = $"{DateTime.Now:HH:mm:ss}  {message}";
        _logItems.Add(line);
        LogList.ScrollIntoView(line);
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
