using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Settings;

namespace GodSaveTheScreen.App.Editor;

/// <summary>Umwandlung zwischen Objekten und den gespeicherten Stilen (Standardwerte, Vorlagen).</summary>
public static class StyleConversions
{
    public static BlurStyle ToStyle(this BlurAnnotation blur) => new()
    {
        Mode = blur.Mode,
        Strength = blur.Strength,
        Shape = blur.Shape,
        FeatherEdge = blur.FeatherEdge,
        RedactColor = blur.RedactColor,
    };

    public static void Apply(this BlurAnnotation blur, BlurStyle style)
    {
        blur.Mode = style.Mode;
        blur.Strength = style.Strength;
        blur.Shape = style.Shape;
        blur.FeatherEdge = style.FeatherEdge;
        blur.RedactColor = style.RedactColor;
    }

    public static MagnifierStyle ToStyle(this MagnifierAnnotation magnifier) => new()
    {
        Zoom = magnifier.Zoom,
        Shape = magnifier.Shape,
        Border = magnifier.Border,
        ShowConnector = magnifier.ShowConnector,
        ShowSourceFrame = magnifier.ShowSourceFrame,
        BaseImageOnly = magnifier.BaseImageOnly,
        SmoothScaling = magnifier.SmoothScaling,
    };

    public static void Apply(this MagnifierAnnotation magnifier, MagnifierStyle style)
    {
        magnifier.Zoom = Math.Clamp(style.Zoom, MagnifierAnnotation.MinZoom, MagnifierAnnotation.MaxZoom);
        magnifier.Shape = style.Shape;
        magnifier.Border = style.Border;
        magnifier.ShowConnector = style.ShowConnector;
        magnifier.ShowSourceFrame = style.ShowSourceFrame;
        magnifier.BaseImageOnly = style.BaseImageOnly;
        magnifier.SmoothScaling = style.SmoothScaling;
    }

    public static TextStyle ToStyle(this TextAnnotation text) => new()
    {
        FontFamilyName = text.FontFamilyName,
        FontWeight = text.FontWeight,
        FontStyle = text.FontStyle,
        FontStretch = text.FontStretch,
        FontSize = text.FontSize,
        Fill = text.Fill,
        Stroke = text.Stroke,
        OutlineBlur = text.OutlineBlur,
        TextAlignment = text.TextAlignment,
        Background = text.Background,
        BackgroundPadding = text.BackgroundPadding,
        BackgroundCornerRadius = text.BackgroundCornerRadius,
        Decorations = text.Decorations,
        Shadow = text.Shadow,
    };

    public static MarkerStyle ToStyle(this MarkerAnnotation marker) => new()
    {
        Shape = marker.Shape,
        Size = marker.Size,
        Fill = marker.Fill,
        Stroke = marker.Stroke,
        TextColor = marker.TextColor,
        FontFamilyName = marker.FontFamilyName,
        Shadow = marker.Shadow,
    };

    /// <summary>Formstil eines Objekts; nicht zutreffende Felder (z. B. Eckenradius bei Ellipsen) kommen aus <paramref name="fallback"/>.</summary>
    public static ShapeStyle ToStyle(this ShapeAnnotation shape, ShapeStyle fallback) => fallback with
    {
        Stroke = shape.Stroke,
        Fill = shape.Fill,
        Opacity = shape.Opacity,
        CornerRadius = shape is RectangleAnnotation rectangle ? rectangle.CornerRadius : fallback.CornerRadius,
        StartCap = shape is LineAnnotation line ? line.StartCap : fallback.StartCap,
        EndCap = shape is LineAnnotation line2 ? line2.EndCap : fallback.EndCap,
        Outline = shape is LineAnnotation line3 ? line3.Outline : fallback.Outline,
        OutlineBlur = shape is LineAnnotation line4 ? line4.OutlineBlur : fallback.OutlineBlur,
        Shadow = shape is StrokeAnnotation ? fallback.Shadow : shape.Shadow,
    };

    public static void Apply(this ShapeAnnotation shape, ShapeStyle style)
    {
        shape.Stroke = style.Stroke;
        shape.Opacity = style.Opacity;
        if (shape is not StrokeAnnotation)
        {
            shape.Shadow = style.Shadow ?? ShadowStyle.None;
        }

        if (shape is not StrokeAnnotation && shape is not LineAnnotation)
        {
            shape.Fill = style.Fill;
        }

        switch (shape)
        {
            case RectangleAnnotation rectangle:
                rectangle.CornerRadius = style.CornerRadius;
                break;
            case LineAnnotation line:
                line.StartCap = style.StartCap;
                line.EndCap = style.EndCap;
                line.Outline = style.Outline ?? LineAnnotation.NoOutline;
                line.OutlineBlur = style.OutlineBlur;
                break;
        }
    }
}
