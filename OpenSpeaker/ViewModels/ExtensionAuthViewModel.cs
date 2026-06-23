using System.Collections.ObjectModel;
using OpenSpeaker.Extensions;
namespace OpenSpeaker.ViewModels;

public class ExtensionAuthField : BaseViewModel
{
    private string _value = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Key { get; init; } = string.Empty;
    public string Value { get => _value; set => SetField(ref _value, value); }
}

public class ExtensionAuthViewModel : BaseViewModel
{
    private readonly Action<Dictionary<string, string>> _onSave;
    private readonly Action _onCancel;

    public string EngineName { get; }
    public ObservableCollection<ExtensionAuthField> Fields { get; } = new();

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }

    public ExtensionAuthViewModel(
        string engineName,
        IReadOnlyList<ExtAuthField> fields,
        Dictionary<string, string> currentValues,
        Action<Dictionary<string, string>> onSave,
        Action onCancel)
    {
        EngineName = engineName;
        _onSave = onSave;
        _onCancel = onCancel;

        foreach (var f in fields)
        {
            currentValues.TryGetValue(f.Key, out var existing);
            Fields.Add(new ExtensionAuthField
            {
                Key = f.Key,
                Label = f.Label,
                Value = existing ?? string.Empty
            });
        }

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(_onCancel);
    }

    private void Save()
    {
        var values = Fields.ToDictionary(f => f.Key, f => f.Value);
        _onSave(values);
    }
}
