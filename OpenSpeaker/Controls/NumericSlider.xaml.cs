using System.Windows;
using WpfBinding = System.Windows.Data.Binding;
using WpfBindingMode = System.Windows.Data.BindingMode;
using WpfControls = System.Windows.Controls;
using WpfInput = System.Windows.Input;
namespace OpenSpeaker.Controls;

public partial class NumericSlider : WpfControls.UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericSlider),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(100.0));

    public static readonly DependencyProperty DefaultValueProperty =
        DependencyProperty.Register(nameof(DefaultValue), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(0.0));

    public static readonly DependencyProperty SliderWidthProperty =
        DependencyProperty.Register(nameof(SliderWidth), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(160.0));

    public static readonly DependencyProperty SuffixProperty =
        DependencyProperty.Register(nameof(Suffix), typeof(string), typeof(NumericSlider),
            new PropertyMetadata(string.Empty, OnValueChanged));

    public static readonly DependencyProperty SmallChangeProperty =
        DependencyProperty.Register(nameof(SmallChange), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(1.0));

    public static readonly DependencyProperty LargeChangeProperty =
        DependencyProperty.Register(nameof(LargeChange), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(10.0));

    public double Value        { get => (double)GetValue(ValueProperty);        set => SetValue(ValueProperty, value); }
    public double Minimum      { get => (double)GetValue(MinimumProperty);      set => SetValue(MinimumProperty, value); }
    public double Maximum      { get => (double)GetValue(MaximumProperty);      set => SetValue(MaximumProperty, value); }
    public double DefaultValue { get => (double)GetValue(DefaultValueProperty); set => SetValue(DefaultValueProperty, value); }
    public double SliderWidth  { get => (double)GetValue(SliderWidthProperty);  set => SetValue(SliderWidthProperty, value); }
    public string Suffix       { get => (string)GetValue(SuffixProperty);       set => SetValue(SuffixProperty, value); }
    public double SmallChange  { get => (double)GetValue(SmallChangeProperty);  set => SetValue(SmallChangeProperty, value); }
    public double LargeChange  { get => (double)GetValue(LargeChangeProperty);  set => SetValue(LargeChangeProperty, value); }

    private bool _suppressLostFocus;

    public NumericSlider()
    {
        InitializeComponent();

        PART_Slider.SetBinding(WpfControls.Slider.ValueProperty,           new WpfBinding(nameof(Value))       { Source = this, Mode = WpfBindingMode.TwoWay });
        PART_Slider.SetBinding(WpfControls.Slider.MinimumProperty,         new WpfBinding(nameof(Minimum))     { Source = this });
        PART_Slider.SetBinding(WpfControls.Slider.MaximumProperty,         new WpfBinding(nameof(Maximum))     { Source = this });
        PART_Slider.SetBinding(WpfControls.Slider.SmallChangeProperty,     new WpfBinding(nameof(SmallChange)) { Source = this });
        PART_Slider.SetBinding(WpfControls.Slider.LargeChangeProperty,     new WpfBinding(nameof(LargeChange)) { Source = this });
        PART_Slider.SetBinding(WpfControls.Slider.TickFrequencyProperty,   new WpfBinding(nameof(SmallChange)) { Source = this });
        PART_Slider.SetBinding(FrameworkElement.WidthProperty,             new WpfBinding(nameof(SliderWidth)) { Source = this });
        PART_Slider.IsSnapToTickEnabled = true;

        Loaded += (_, _) => UpdateDisplay();
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((NumericSlider)d).UpdateDisplay();

    private void UpdateDisplay()
    {
        var v = Value;
        PART_Display.Text = (v == Math.Truncate(v) ? ((int)v).ToString() : v.ToString("G")) + Suffix;
    }

    private void OnDisplayClick(object sender, WpfInput.MouseButtonEventArgs e)
    {
        PART_Display.Visibility = Visibility.Collapsed;
        PART_Input.Visibility   = Visibility.Visible;
        PART_Input.Text = Value == Math.Truncate(Value) ? ((int)Value).ToString() : Value.ToString("G");
        PART_Input.SelectAll();
        PART_Input.Focus();
    }

    private void OnDisplayRightClick(object sender, WpfInput.MouseButtonEventArgs e)
    {
        Value = DefaultValue;
        e.Handled = true;
    }

    private void OnInputLostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressLostFocus) { _suppressLostFocus = false; return; }
        CommitInput();
    }

    private void OnInputKeyDown(object sender, WpfInput.KeyEventArgs e)
    {
        if (e.Key == WpfInput.Key.Return)
        {
            _suppressLostFocus = true;
            CommitInput();
            e.Handled = true;
        }
        else if (e.Key == WpfInput.Key.Escape)
        {
            _suppressLostFocus = true;
            CancelInput();
            e.Handled = true;
        }
    }

    private void CommitInput()
    {
        if (double.TryParse(PART_Input.Text, out var v))
            Value = Math.Clamp(v, Minimum, Maximum);
        CancelInput();
    }

    private void CancelInput()
    {
        PART_Input.Visibility   = Visibility.Collapsed;
        PART_Display.Visibility = Visibility.Visible;
        UpdateDisplay();
    }
}
