using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using ReSeq.Core.Models;

namespace ReSeq.ViewModels;

public sealed class VideoTileViewModel : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;

    public VideoTileViewModel(VideoItem item, ImageSource defaultThumbnail)
    {
        Item = item;
        _thumbnail = defaultThumbnail;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public VideoItem Item { get; }

    public string FileName => Item.OriginalFileName;

    public string NumberText => $"X={Item.X}, Y={Item.Y}";

    public string ExtensionText => Item.Extension.ToLowerInvariant();

    public ImageSource? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (Equals(_thumbnail, value))
            {
                return;
            }

            _thumbnail = value;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
