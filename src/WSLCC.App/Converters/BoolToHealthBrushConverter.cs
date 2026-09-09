using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;

namespace WSLCC.App.Converters;

public sealed class BoolToHealthBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        var ok = value is true;
        var key = ok ? "SystemFillColorSuccessBrush" : "SystemFillColorCautionBrush";
        return Application.Current.Resources[key]
            ?? Application.Current.Resources["AccentTextFillColorPrimaryBrush"];
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => throw new NotSupportedException();
}