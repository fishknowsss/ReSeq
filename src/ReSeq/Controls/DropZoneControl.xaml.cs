using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ReSeq.ViewModels;

namespace ReSeq.Controls;

public partial class DropZoneControl : UserControl
{
    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(DropTargetViewModel),
        typeof(DropZoneControl),
        new PropertyMetadata(null));

    public static readonly DependencyProperty LineWidthProperty = DependencyProperty.Register(
        nameof(LineWidth),
        typeof(double),
        typeof(DropZoneControl),
        new PropertyMetadata(120d));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight),
        typeof(double),
        typeof(DropZoneControl),
        new PropertyMetadata(2d));

    public DropZoneControl()
    {
        InitializeComponent();
    }

    public DropTargetViewModel? Target
    {
        get => (DropTargetViewModel?)GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    public double LineWidth
    {
        get => (double)GetValue(LineWidthProperty);
        set => SetValue(LineWidthProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    private void Zone_DragEnter(object sender, DragEventArgs e)
    {
        Line.Background = (Brush)FindResource("AccentBrush");
        Line.Opacity = 1;
    }

    private void Zone_DragLeave(object sender, DragEventArgs e)
    {
        Line.Background = Brushes.Transparent;
        Line.Opacity = 0;
    }

    private void Zone_Drop(object sender, DragEventArgs e)
    {
        Line.Background = Brushes.Transparent;
        Line.Opacity = 0;
    }
}
