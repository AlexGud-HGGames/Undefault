using System.Collections.ObjectModel;
using Avalonia.Threading;
using Core.Models;
using Core.Services;
using UI.Models;
using UI.Services;

namespace UI.ViewModels;

public class LogsViewModel : ViewModelBase
{
    private const int MaxEntries = 200;

    public LogsViewModel(IAppStateService appStateService)
    {
        Logs = new ObservableCollection<LogEntry>();

        appStateService.Events.Subscribe(normalizedEvent =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                Logs.Add(ToLogEntry(normalizedEvent));
                while (Logs.Count > MaxEntries)
                {
                    Logs.RemoveAt(0);
                }
            });
        });
    }

    public ObservableCollection<LogEntry> Logs { get; }

    private static LogEntry ToLogEntry(NormalizedEvent normalizedEvent)
    {
        var message = string.IsNullOrWhiteSpace(normalizedEvent.Detail)
            ? normalizedEvent.Type.ToString()
            : $"{normalizedEvent.Type}: {normalizedEvent.Detail}";

        return new LogEntry(normalizedEvent.Type, normalizedEvent.Timestamp, message);
    }
}
