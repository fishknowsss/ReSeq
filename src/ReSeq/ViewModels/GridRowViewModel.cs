using System.Collections.ObjectModel;

namespace ReSeq.ViewModels;

public sealed class GridRowViewModel : ViewModelBase
{
    public GridRowViewModel(
        int x,
        IEnumerable<GridCellViewModel> cells,
        DropTargetViewModel insertBeforeTarget)
    {
        X = x;
        InsertBeforeTarget = insertBeforeTarget;
        Cells = new ObservableCollection<GridCellViewModel>(cells);
    }

    public int X { get; }

    public string Header => $"镜头 {X}";

    public DropTargetViewModel InsertBeforeTarget { get; }

    public ObservableCollection<GridCellViewModel> Cells { get; }
}
