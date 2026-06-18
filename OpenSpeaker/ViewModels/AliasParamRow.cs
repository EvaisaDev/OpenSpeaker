using System.Globalization;
using OpenSpeaker.TTS;
namespace OpenSpeaker.ViewModels;

public class AliasParamRow : BaseViewModel
{
    public EngineParameterDef Def { get; init; } = null!;

    private string _value = string.Empty;
    public string Value
    {
        get => _value;
        set
        {
            SetField(ref _value, value);
            OnPropertyChanged(nameof(SliderValue));
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public bool IsSlider => Def.Type == EngineParameterType.Slider;
    public bool IsComboBox => Def.Type == EngineParameterType.ComboBox;

    public double SliderValue
    {
        get => double.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        set => Value = value.ToString("G4", CultureInfo.InvariantCulture);
    }

    public string DisplayValue
    {
        get
        {
            if (!double.TryParse(_value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                return _value;
            return Def.Step >= 1 ? v.ToString("0") : v.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
