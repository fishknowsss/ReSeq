namespace ReSeq.ViewModels;

public sealed class GridCellViewModel : ViewModelBase
{
    public GridCellViewModel(
        int x,
        int y,
        VideoTileViewModel? video,
        DropTargetViewModel cellTarget,
        DropTargetViewModel versionTarget)
    {
        X = x;
        Y = y;
        Video = video;
        CellTarget = cellTarget;
        VersionTarget = versionTarget;
    }

    public int X { get; }

    public int Y { get; }

    public VideoTileViewModel? Video { get; }

    public bool HasVideo => Video is not null;

    public DropTargetViewModel CellTarget { get; }

    public DropTargetViewModel VersionTarget { get; }
}
