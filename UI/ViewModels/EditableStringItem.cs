using System;
using System.Windows.Input;

namespace UI.ViewModels;

public sealed class EditableStringItem : ViewModelBase
{
    private string _value;

    public EditableStringItem(string value, Action<EditableStringItem> onRemove)
    {
        _value = value;
        RemoveCommand = new DelegateCommand(() => onRemove(this));
    }

    public string Value
    {
        get => _value;
        set => SetField(ref _value, value);
    }

    public ICommand RemoveCommand { get; }
}
