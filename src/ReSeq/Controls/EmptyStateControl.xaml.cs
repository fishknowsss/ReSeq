using System.Windows;
using System.Windows.Controls;

namespace ReSeq.Controls;

public partial class EmptyStateControl : UserControl
{
    public static readonly RoutedEvent ChooseRequestedEvent = EventManager.RegisterRoutedEvent(
        nameof(ChooseRequested),
        RoutingStrategy.Bubble,
        typeof(RoutedEventHandler),
        typeof(EmptyStateControl));

    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title),
        typeof(string),
        typeof(EmptyStateControl),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionTextProperty = DependencyProperty.Register(
        nameof(ActionText),
        typeof(string),
        typeof(EmptyStateControl),
        new PropertyMetadata("选择"));

    public static readonly DependencyProperty TargetProperty = DependencyProperty.Register(
        nameof(Target),
        typeof(object),
        typeof(EmptyStateControl),
        new PropertyMetadata(null));

    public EmptyStateControl()
    {
        InitializeComponent();
    }

    public event RoutedEventHandler ChooseRequested
    {
        add => AddHandler(ChooseRequestedEvent, value);
        remove => RemoveHandler(ChooseRequestedEvent, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public object? Target
    {
        get => GetValue(TargetProperty);
        set => SetValue(TargetProperty, value);
    }

    private void ChooseButton_Click(object sender, RoutedEventArgs e)
    {
        RaiseEvent(new RoutedEventArgs(ChooseRequestedEvent));
    }
}
