using System.Text.Json;
using System.Text.Json.Serialization;
using GodSaveTheScreen.Core.Annotations;

namespace GodSaveTheScreen.Core.Geometry;

/// <summary>Kompakte JSON-Arrays für Geometrietypen im Projektformat: <c>[x, y]</c>, <c>[x, y, b, h]</c>, <c>[x, y, druck]</c>.</summary>
internal static class GeometryJson
{
    public static double[] ReadNumbers(ref Utf8JsonReader reader, int min, int max)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Zahlenliste erwartet.");
        }

        var values = new List<double>(max);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            values.Add(reader.GetDouble());
        }

        if (values.Count < min || values.Count > max)
        {
            throw new JsonException($"Erwartet {min}–{max} Zahlen, gefunden {values.Count}.");
        }

        return [.. values];
    }
}

internal sealed class PointDJsonConverter : JsonConverter<PointD>
{
    public override PointD Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var v = GeometryJson.ReadNumbers(ref reader, 2, 2);
        return new PointD(v[0], v[1]);
    }

    public override void Write(Utf8JsonWriter writer, PointD value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}

internal sealed class RectDJsonConverter : JsonConverter<RectD>
{
    public override RectD Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var v = GeometryJson.ReadNumbers(ref reader, 4, 4);
        return new RectD(v[0], v[1], v[2], v[3]);
    }

    public override void Write(Utf8JsonWriter writer, RectD value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Width);
        writer.WriteNumberValue(value.Height);
        writer.WriteEndArray();
    }
}

internal sealed class PixelRectJsonConverter : JsonConverter<PixelRect>
{
    public override PixelRect Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var v = GeometryJson.ReadNumbers(ref reader, 4, 4);
        return new PixelRect((int)v[0], (int)v[1], (int)v[2], (int)v[3]);
    }

    public override void Write(Utf8JsonWriter writer, PixelRect value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Width);
        writer.WriteNumberValue(value.Height);
        writer.WriteEndArray();
    }
}

internal sealed class StrokePointJsonConverter : JsonConverter<StrokePoint>
{
    public override StrokePoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var v = GeometryJson.ReadNumbers(ref reader, 2, 3);
        return new StrokePoint(v[0], v[1], v.Length > 2 ? v[2] : 1);
    }

    public override void Write(Utf8JsonWriter writer, StrokePoint value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Pressure);
        writer.WriteEndArray();
    }
}
