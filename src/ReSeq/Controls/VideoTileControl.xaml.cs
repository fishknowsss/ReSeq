using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Extensions.DependencyInjection;
using ReSeq.Services;
using ReSeq.ViewModels;

namespace ReSeq.Controls;

public partial class VideoTileControl : UserControl
{
    public const string DragFormat = "ReSeq.VideoTile";

    public static readonly RoutedEvent DeleteRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(DeleteRequested),
        RoutingStrategy.Bubble,
        typeof(EventHandler<VideoTileRequestedEventArgs>),
        typeof(VideoTileControl));

    private Point _dragStartPoint;
    private int _loadVersion;

    public VideoTileControl()
    {
        InitializeComponent();
    }

    public event EventHandler<VideoTileRequestedEventArgs> DeleteRequested
    {
        add => AddHandler(DeleteRequestedEvent, value);
        remove => RemoveHandler(DeleteRequestedEvent, value);
    }

    private async void Root_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        var version = ++_loadVersion;
        ThumbnailImage.Source = null;
        ThumbnailImage.Opacity = 0;
        Skeleton.Visibility = Visibility.Visible;

        if (e.NewValue is not VideoTileViewModel video)
        {
            return;
        }

        try
        {
            var service = App.Services.GetRequiredService<ShellThumbnailService>();
            var source = await service.GetThumbnailAsync(video.FilePath, 180, 102);
            if (version != _loadVersion ||
                DataContext is not VideoTileViewModel current ||
                !string.Equals(current.FilePath, video.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            ThumbnailImage.Source = source;
            ThumbnailImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160)));
            Skeleton.Visibility = Visibility.Collapsed;
        }
        catch
        {
            Skeleton.Visibility = Visibility.Collapsed;
        }
    }

    private void Root_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
    }

    private void Root_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || DataContext is not VideoTileViewModel video)
        {
            return;
        }

        var position = e.GetPosition(this);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(DragFormat, video);
        DragDrop.DoDragDrop(this, data, DragDropEffects.Move);
    }

    private void DeleteMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is VideoTileViewModel video)
        {
            RaiseEvent(new VideoTileRequestedEventArgs(DeleteRequestedEvent, video));
        }
    }
}

public sealed class VideoTileRequestedEventArgs : RoutedEventArgs
{
    public VideoTileRequestedEventArgs(RoutedEvent routedEvent, VideoTileViewModel video)
        : base(routedEvent)
    {
        Video = video;
    }

    public VideoTileViewModel Video { get; }
}
