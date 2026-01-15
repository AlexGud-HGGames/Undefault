using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia.Controls;
using UI.Models;
using UI.ViewModels;

namespace UI.Views;

public partial class LogsView : UserControl
{
    private ObservableCollection<LogEntry>? _logs;
    private ListBox? _listBox;

    public LogsView()
    {
        InitializeComponent();
        _listBox = this.FindControl<ListBox>("LogsList");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_logs is not null)
        {
            _logs.CollectionChanged -= OnLogsChanged;
            _logs = null;
        }

        if (DataContext is LogsViewModel viewModel)
        {
            _logs = viewModel.Logs;
            _logs.CollectionChanged += OnLogsChanged;
        }
    }

    private void OnLogsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_listBox is null || _logs is null || _logs.Count == 0)
        {
            return;
        }

        var last = _logs[^1];
        _listBox.ScrollIntoView(last);
    }
}
