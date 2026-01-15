namespace UI.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public MainWindowViewModel()
    {
        Status = new StatusViewModel();
        Settings = new SettingsViewModel();
        Logs = new LogsViewModel();
    }

    public StatusViewModel Status { get; }

    public SettingsViewModel Settings { get; }

    public LogsViewModel Logs { get; }
}
