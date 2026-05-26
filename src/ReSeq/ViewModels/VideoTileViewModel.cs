using ReSeq.Core.Models;

namespace ReSeq.ViewModels;

public sealed class VideoTileViewModel : ViewModelBase
{
    public VideoTileViewModel(VideoItem item)
    {
        Item = item;
    }

    public VideoItem Item { get; }

    public string FilePath => Item.FilePath;

    public string FileName => Item.OriginalFileName;

    public string Number => Item.Number;

    public string NumberText => $"X={Item.X}, Y={Item.Y}";

    public string ExtensionText => Item.Extension.TrimStart('.').ToUpperInvariant();
}
