using System.Globalization;
using System.Windows.Data;
namespace OpenSpeaker.ThingsIDKWhereToPut;

public class MinThumbViewportConverter : IMultiValueConverter
{
    public double MinThumbLength { get; set; } = 30.0;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not [double viewportSize, double maximum, double minimum, double trackLength])
            return 0.0;

        double range = Math.Max(0, maximum - minimum);
        if (range == 0 || trackLength <= MinThumbLength) return viewportSize;

        double naturalThumb = trackLength * viewportSize / (range + viewportSize);
        if (naturalThumb >= MinThumbLength) return viewportSize;

        return MinThumbLength * range / (trackLength - MinThumbLength);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
