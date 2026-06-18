using System.ComponentModel;
using System.Runtime.CompilerServices;
namespace OpenSpeaker.Localization;

public sealed class UiController : INotifyPropertyChanged
{
    public static UiController Instance { get; } = new();

    private bool _showTooltips = true;
    public bool ShowTooltips
    {
        get => _showTooltips;
        set { _showTooltips = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
