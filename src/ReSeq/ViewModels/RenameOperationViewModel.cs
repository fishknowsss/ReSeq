using ReSeq.Core.Models;

namespace ReSeq.ViewModels;

public sealed class RenameOperationViewModel : ViewModelBase
{
    public RenameOperationViewModel(RenameOperation operation)
    {
        Operation = operation;
    }

    public RenameOperation Operation { get; }

    public string OldName => Operation.OldName;

    public string NewName => Operation.NewName;

    public string KindText => Operation.OperationType switch
    {
        RenameOperationType.AddNewVideo => "新增",
        RenameOperationType.MoveExisting => "移动",
        RenameOperationType.DeleteExisting => "删除",
        _ => "重编号"
    };

    public string KindKey => Operation.OperationType switch
    {
        RenameOperationType.AddNewVideo => "Add",
        RenameOperationType.MoveExisting => "Move",
        RenameOperationType.DeleteExisting => "Delete",
        _ => "Renumber"
    };
}
