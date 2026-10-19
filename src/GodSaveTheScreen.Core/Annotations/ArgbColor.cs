using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GodSaveTheScreen.Core.Annotations;

/// <summary>Farbe als ARGB-Bytes (nicht vormultipliziert), unabhängig von WPF.</summary>
[JsonConverter(typeof(ArgbColorJsonConverter))]
public readonly record struct ArgbColor(byte A, byte R, byte G, byte B)
{
    public static ArgbColor Transparent => new(0, 0, 0, 0);

    public static ArgbColor Black => new(255, 0, 0, 0);

    public static ArgbColor White => new(255, 255, 255, 255);

    public static ArgbColor Red => new(255, 255, 0, 0);

    public static ArgbColor FromRgb(byte r, byte g, byte b) => new(255, r, g, b);

    public bool IsTransparent => A == 0;

    public ArgbColor WithAlpha(byte alpha) => this with { A = alpha };

    /// <summary>Format <c>#AARRGGBB</c>.</summary>
    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"#{A:X2}{R:X2}{G:X2}{B:X2}");

    /// <summary>Akzeptiert <c>#AARRGGBB</c>, <c>#RRGGBB</c>, <c>#ARGB</c> und <c>#RGB</c> (mit oder ohne <c>#</c>).</summary>
    public static bool TryParse(string? text, out ArgbColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var hex = text.Trim().TrimStart('#');
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length is not (6 or 8) || !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }

        color = hex.Length == 8
            ? new ArgbColor((byte)(value >> 24), (byte)(value >> 16), (byte)(value >> 8), (byte)value)
            : new ArgbColor(255, (byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public static ArgbColor Parse(string text) =>
        TryParse(text, out var color) ? color : throw new FormatException($"Ungültige Farbe: {text}");
}

public sealed class ArgbColorJsonConverter : JsonConverter<ArgbColor>
{
    public override ArgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        ArgbColor.TryParse(reader.GetString(), out var color) ? color : throw new JsonException("Ungültige Farbe.");

    public override void Write(Utf8JsonWriter writer, ArgbColor value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}
