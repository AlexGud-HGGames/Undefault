namespace UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel(StatusViewModel status, SettingsViewModel settings, LogsViewModel logs)
    {
        Status = status;
        Settings = settings;
        Logs = logs;
    }

    public StatusViewModel Status { get; }

    public SettingsViewModel Settings { get; }

    public LogsViewModel Logs { get; }
}
