using System.Collections.ObjectModel;
using ReSeq.Core.Models;

namespace ReSeq.ViewModels;

public sealed class RenamePlanViewModel : ViewModelBase
{
    private readonly ViewModelFactory _factory;
    private string _statusText = "无计划";
    private string _errorText = string.Empty;

    public RenamePlanViewModel(ViewModelFactory factory)
    {
        _factory = factory;
    }

    public ObservableCollection<RenameOperationViewModel> Operations { get; } = [];

    public RenamePlan? Plan { get; private set; }

    public bool CanExecute => Plan?.CanExecute == true;

    public bool HasOperations => Operations.Count > 0;

    public bool HasContent => HasOperations || HasError;

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set => SetProperty(ref _errorText, value);
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    public void SetPlan(RenamePlan plan)
    {
        Plan = plan;
        Operations.Clear();
        foreach (var operation in plan.Operations)
        {
            Operations.Add(_factory.CreateRenameOperation(operation));
        }

        ErrorText = string.Empty;
        StatusText = $"{Operations.Count} 项";
        NotifyState();
    }

    public void SetError(string message)
    {
        Plan = null;
        Operations.Clear();
        ErrorText = message;
        StatusText = "不可执行";
        NotifyState();
    }

    public void Clear()
    {
        Plan = null;
        Operations.Clear();
        ErrorText = string.Empty;
        StatusText = "无计划";
        NotifyState();
    }

    private void NotifyState()
    {
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(HasOperations));
        OnPropertyChanged(nameof(HasError));
        OnPropertyChanged(nameof(HasContent));
    }
}
