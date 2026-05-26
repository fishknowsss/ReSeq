namespace ReSeq.ViewModels;

public enum DropTargetKind
{
    ShotRow,
    Version,
    EmptyCell,
    ExistingVideo
}

public sealed class DropTargetViewModel : ViewModelBase
{
    public DropTargetViewModel(DropTargetKind kind, int x, int y, VideoTileViewModel? video = null)
    {
        Kind = kind;
        X = x;
        Y = y;
        Video = video;
    }

    public DropTargetKind Kind { get; }

    public int X { get; }

    public int Y { get; }

    public VideoTileViewModel? Video { get; }

    public string Hint => Kind switch
    {
        DropTargetKind.ShotRow => $"插入新镜头到 X={X} 前",
        DropTargetKind.Version => $"插入为 X={X} 的第 {Y} 个版本",
        DropTargetKind.EmptyCell => $"命名为 {X}-{Y}",
        DropTargetKind.ExistingVideo => "选择放到前面或后面",
        _ => "拖入一个视频到网格中"
    };

}
