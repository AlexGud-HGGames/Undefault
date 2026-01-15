using System.Collections.ObjectModel;

namespace UI.ViewModels;

public class LogsViewModel : ViewModelBase
{
    public LogsViewModel()
    {
        Entries = new ObservableCollection<string>();
    }

    public ObservableCollection<string> Entries { get; }
}
