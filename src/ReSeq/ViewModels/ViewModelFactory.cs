using ReSeq.Core.Models;

namespace ReSeq.ViewModels;

public sealed class ViewModelFactory
{
    public VideoTileViewModel CreateVideoTile(VideoItem item)
    {
        return new VideoTileViewModel(item);
    }

    public GridCellViewModel CreateGridCell(int x, int y, VideoTileViewModel? video)
    {
        var cellTarget = new DropTargetViewModel(video is null ? DropTargetKind.EmptyCell : DropTargetKind.ExistingVideo, x, y, video);
        var versionTarget = new DropTargetViewModel(DropTargetKind.Version, x, y);
        return new GridCellViewModel(x, y, video, cellTarget, versionTarget);
    }

    public GridRowViewModel CreateGridRow(int x, IEnumerable<GridCellViewModel> cells)
    {
        var insertBeforeTarget = new DropTargetViewModel(DropTargetKind.ShotRow, x, 1);
        return new GridRowViewModel(x, cells, insertBeforeTarget);
    }

    public DropTargetViewModel CreateFirstVideoTarget()
    {
        return new DropTargetViewModel(DropTargetKind.EmptyCell, 1, 1);
    }

    public DropTargetViewModel CreateVersionTarget(int x, int y)
    {
        return new DropTargetViewModel(DropTargetKind.Version, x, y);
    }

    public RenameOperationViewModel CreateRenameOperation(RenameOperation operation)
    {
        return new RenameOperationViewModel(operation);
    }

    public LogEntryViewModel CreateLogEntry(LogLevel level, string message)
    {
        return new LogEntryViewModel(level, message);
    }
}
