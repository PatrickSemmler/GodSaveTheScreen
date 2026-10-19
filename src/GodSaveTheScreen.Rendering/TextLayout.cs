using System.Globalization;
using System.Windows;
using System.Windows.Media;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;

namespace GodSaveTheScreen.Rendering;

/// <summary>
/// Text über <see cref="FormattedText"/> mit <c>PixelsPerDip = 1</c> (Bildpixel-Treue: 24 px Schrift sind im Export 24 px hoch).
/// Stellt auch die Layoutgröße für <see cref="TextAnnotation.Bounds"/> bereit.
/// </summary>
public static class TextLayout
{
    private const double PixelsPerDip = 1.0;

    /// <summary>Verbindet die Messung mit dem Core-Modell (mehrfach aufrufbar).</summary>
    public static void Install() => TextAnnotation.MeasureProvider = Measure;

    public static Typeface CreateTypeface(string familyName, int weight, FontStyleKind style, int stretch) => new(
        new FontFamily(string.IsNullOrWhiteSpace(familyName) ? "Segoe UI" : familyName),
        style switch
        {
            FontStyleKind.Italic => FontStyles.Italic,
            FontStyleKind.Oblique => FontStyles.Oblique,
            _ => FontStyles.Normal,
        },
        FontWeight.FromOpenTypeWeight(Math.Clamp(weight, 1, 999)),
        FontStretch.FromOpenTypeStretch(Math.Clamp(stretch, 1, 9)),
        new FontFamily("Segoe UI"));

    public static FormattedText Create(TextAnnotation annotation, Brush? brush)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        var text = annotation.Text.Length == 0 ? " " : annotation.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var typeface = CreateTypeface(annotation.FontFamilyName, annotation.FontWeight, annotation.FontStyle, annotation.FontStretch);
        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            Math.Max(1, annotation.FontSize),
            brush ?? Brushes.Black,
            PixelsPerDip)
        {
            TextAlignment = annotation.TextAlignment switch
            {
                TextAlignmentKind.Center => TextAlignment.Center,
                TextAlignmentKind.Right => TextAlignment.Right,
                _ => TextAlignment.Left,
            },
        };

        var decorations = new TextDecorationCollection();
        if (annotation.Decorations.HasFlag(TextDecorationKind.Underline))
        {
            decorations.Add(TextDecorations.Underline[0]);
        }

        if (annotation.Decorations.HasFlag(TextDecorationKind.Strikethrough))
        {
            decorations.Add(TextDecorations.Strikethrough[0]);
        }

        if (decorations.Count > 0)
        {
            formatted.SetTextDecorations(decorations);
        }

        if (annotation.WrapWidth is > 0 and var wrap)
        {
            formatted.MaxTextWidth = wrap;
        }
        else if (annotation.TextAlignment != TextAlignmentKind.Left)
        {
            // Ohne Umbruch richtet sich die Ausrichtung nach der breitesten Zeile.
            formatted.MaxTextWidth = Math.Max(1, formatted.WidthIncludingTrailingWhitespace);
        }

        return formatted;
    }

    public static SizeD Measure(TextAnnotation annotation)
    {
        var formatted = Create(annotation, null);
        var width = annotation.WrapWidth is > 0 and var wrap
            ? wrap
            : annotation.Text.Length == 0 ? annotation.FontSize * 0.5 : formatted.WidthIncludingTrailingWhitespace;
        return new SizeD(Math.Max(1, width), Math.Max(1, formatted.Height));
    }
}
