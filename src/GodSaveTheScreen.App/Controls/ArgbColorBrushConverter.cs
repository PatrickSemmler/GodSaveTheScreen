using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.App.Controls;

/// <summary><see cref="ArgbColor"/> → Pinsel für Farbfelder (z. B. Kurzfassung im Abschnittskopf); <c>null</c> (gemischt) bleibt leer.</summary>
public sealed class ArgbColorBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is ArgbColor c ? new SolidColorBrush(Color.FromArgb(c.A, c.R, c.G, c.B)) : null;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
