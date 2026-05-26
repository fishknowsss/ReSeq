using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Microsoft.Win32;
using ReSeq.Controls;
using ReSeq.Services;
using ReSeq.ViewModels;

namespace ReSeq;

public partial class MainWindow : Window
{
    private readonly ShellThumbnailService _thumbnailService;
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow(MainWindowViewModel viewModel, ShellThumbnailService thumbnailService)
    {
        _thumbnailService = thumbnailService;
        InitializeComponent();
        DataContext = viewModel;
        AddHandler(VideoTileControl.DeleteRequestedEvent, new EventHandler<VideoTileRequestedEventArgs>(VideoTile_DeleteRequested));
    }

    private async void ChooseFolderButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAsync(ChooseFolderAsync);
    }

    private async void EmptyState_ChooseRequested(object sender, RoutedEventArgs e)
    {
        await RunUiAsync(ChooseFolderAsync);
    }

    private async Task ChooseFolderAsync()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择视频文件夹",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            _thumbnailService.ClearCache();
            await ViewModel.LoadFolderAsync(dialog.FolderName);
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAsync(async () =>
        {
            _thumbnailService.ClearCache();
            await ViewModel.RefreshAsync();
        });
    }

    private async void ExecuteButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiAsync(async () =>
        {
            _thumbnailService.ClearCache();
            await ViewModel.ExecutePendingPlanAsync();
            _thumbnailService.ClearCache();
        });
    }

    private void ClearPreviewButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearPreview();
    }

    private void ClearLogsButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ClearLogs();
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        try
        {
            var target = FindDropTarget(e.OriginalSource as DependencyObject);
            var draggedVideo = TryGetDraggedVideo(e);
            var message = string.Empty;

            if (draggedVideo is not null)
            {
                if (ViewModel.CanAcceptInternalDrop(draggedVideo, target, out message))
                {
                    e.Effects = DragDropEffects.Move;
                    ViewModel.UpdateDropHint(target!);
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    ViewModel.ShowDropMessage(message);
                }

                e.Handled = true;
                return;
            }

            var folders = TryGetDirectories(e);
            if (folders.Count > 0)
            {
                if (ViewModel.CanAcceptFolderDrop(folders, out _, out message))
                {
                    e.Effects = DragDropEffects.Copy;
                    ViewModel.ShowDropMessage("松开后打开该文件夹");
                }
                else
                {
                    e.Effects = DragDropEffects.None;
                    ViewModel.ShowDropMessage(message);
                }

                e.Handled = true;
                return;
            }

            var files = TryGetFiles(e);
            if (target is null || !ViewModel.CanAcceptDrop(files, out _, out message))
            {
                e.Effects = DragDropEffects.None;
                if (!string.IsNullOrWhiteSpace(message))
                {
                    ViewModel.ShowDropMessage(message);
                }
                else
                {
                    ViewModel.ResetDropHint();
                }

                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Copy;
            ViewModel.UpdateDropHint(target);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            ViewModel.ReportError("拖拽处理失败", ex);
        }
    }

    private void Window_DragLeave(object sender, DragEventArgs e)
    {
        ViewModel.ResetDropHint();
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        await RunUiAsync(async () =>
        {
            var target = FindDropTarget(e.OriginalSource as DependencyObject);
            var draggedVideo = TryGetDraggedVideo(e);
            ViewModel.ResetDropHint();

            if (draggedVideo is not null)
            {
                if (!ViewModel.CanAcceptInternalDrop(draggedVideo, target, out var internalMessage))
                {
                    ViewModel.CreateRejectedDropPreview(internalMessage);
                    return;
                }

                if (target!.Kind == DropTargetKind.ExistingVideo)
                {
                    ShowExistingVideoMenu(draggedVideo, target);
                    return;
                }

                ViewModel.CreateMovePreview(draggedVideo, target);
                return;
            }

            var folders = TryGetDirectories(e);
            if (ViewModel.CanAcceptFolderDrop(folders, out var folder, out _))
            {
                _thumbnailService.ClearCache();
                await ViewModel.LoadFolderAsync(folder);
                return;
            }

            var files = TryGetFiles(e);
            if (target is null)
            {
                return;
            }

            if (!ViewModel.CanAcceptDrop(files, out var file, out var message))
            {
                ViewModel.CreateRejectedDropPreview(message);
                return;
            }

            if (target.Kind == DropTargetKind.ExistingVideo)
            {
                ShowExistingVideoMenu(file, target);
                return;
            }

            ViewModel.CreatePreview(target, file);
        });
    }

    private void VideoTile_DeleteRequested(object? sender, VideoTileRequestedEventArgs e)
    {
        try
        {
            ViewModel.CreateDeletePreview(e.Video);
            e.Handled = true;
        }
        catch (Exception ex)
        {
            ViewModel.ReportError("删除预览失败", ex);
            e.Handled = true;
        }
    }

    private async Task RunUiAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ViewModel.ReportError("操作失败", ex);
        }
    }

    private void ShowExistingVideoMenu(string filePath, DropTargetViewModel target)
    {
        var menu = new ContextMenu();

        var before = new MenuItem { Header = "放前面" };
        before.Click += (_, _) =>
        {
            ViewModel.CreatePreview(ViewModel.CreateVersionDropTarget(target.X, target.Y), filePath);
        };

        var after = new MenuItem { Header = "放后面" };
        after.Click += (_, _) =>
        {
            ViewModel.CreatePreview(ViewModel.CreateVersionDropTarget(target.X, target.Y + 1), filePath);
        };

        menu.Items.Add(before);
        menu.Items.Add(after);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "取消" });
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void ShowExistingVideoMenu(VideoTileViewModel video, DropTargetViewModel target)
    {
        var menu = new ContextMenu();

        var before = new MenuItem { Header = "放前面" };
        before.Click += (_, _) =>
        {
            ViewModel.CreateMovePreview(video, ViewModel.CreateVersionDropTarget(target.X, target.Y));
        };

        var after = new MenuItem { Header = "放后面" };
        after.Click += (_, _) =>
        {
            ViewModel.CreateMovePreview(video, ViewModel.CreateVersionDropTarget(target.X, target.Y + 1));
        };

        menu.Items.Add(before);
        menu.Items.Add(after);
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem { Header = "取消" });
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private static VideoTileViewModel? TryGetDraggedVideo(DragEventArgs e)
    {
        return e.Data.GetDataPresent(VideoTileControl.DragFormat)
            ? e.Data.GetData(VideoTileControl.DragFormat) as VideoTileViewModel
            : null;
    }

    private static IReadOnlyList<string> TryGetFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return Array.Empty<string>();
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files)
        {
            return Array.Empty<string>();
        }

        return files.Where(File.Exists).ToArray();
    }

    private static IReadOnlyList<string> TryGetDirectories(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return Array.Empty<string>();
        }

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return Array.Empty<string>();
        }

        return paths.Where(Directory.Exists).ToArray();
    }

    private static DropTargetViewModel? FindDropTarget(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: DropTargetViewModel target })
            {
                return target;
            }

            source = GetParent(source);
        }

        return null;
    }

    private static DependencyObject? GetParent(DependencyObject source)
    {
        if (source is ContextMenu contextMenu)
        {
            return contextMenu.PlacementTarget;
        }

        if (source is FrameworkElement frameworkElement && frameworkElement.Parent is not null)
        {
            return frameworkElement.Parent;
        }

        if (source is FrameworkContentElement contentElement && contentElement.Parent is DependencyObject contentParent)
        {
            return contentParent;
        }

        return source is System.Windows.Media.Visual or System.Windows.Media.Media3D.Visual3D
            ? System.Windows.Media.VisualTreeHelper.GetParent(source)
            : null;
    }
}
