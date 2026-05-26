namespace ReSeq.ViewModels;

public enum LogLevel
{
    Info,
    Success,
    Warning,
    Error
}

public sealed class LogEntryViewModel : ViewModelBase
{
    public LogEntryViewModel(LogLevel level, string message)
    {
        Level = level;
        Message = message;
        Timestamp = DateTime.Now;
    }

    public LogLevel Level { get; }

    public string Message { get; }

    public DateTime Timestamp { get; }

    public string TimeText => Timestamp.ToString("HH:mm:ss");

    public string Icon => Level switch
    {
        LogLevel.Success => "✓",
        LogLevel.Warning => "⚠",
        LogLevel.Error => "✕",
        _ => "i"
    };

    public string LevelKey => Level.ToString();
}
