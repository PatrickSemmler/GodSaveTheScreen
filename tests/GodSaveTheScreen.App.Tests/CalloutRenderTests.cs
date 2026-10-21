using System.IO;
using GodSaveTheScreen.Core.Annotations;
using GodSaveTheScreen.Core.Geometry;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Core.Undo;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

/// <summary>Darstellung von Sprechblasen: Körper, Spitze, Rahmen, Leuchten, Text.</summary>
public sealed class CalloutRenderTests
{
    private static readonly ArgbColor Red = ArgbColor.FromRgb(0xE5, 0x39, 0x35);

    private static CalloutAnnotation Callout(CalloutShape shape = CalloutShape.Rounded, PointD? tail = null) => new()
    {
        Rect = new RectD(40, 30, 160, 70),
        Tail = tail ?? new PointD(70, 150),
        Shape = shape,
        Background = Red,
        Text = "Sprechblase",
        FontSize = 20,
    };

    private static PixelBuffer Render(int width, int height, params Annotation[] annotations)
    {
        var document = new ImageDocument(PixelTools.Solid(width, height, 255, 255, 255));
        foreach (var annotation in annotations)
        {
            document.Undo.Execute(new AddAnnotationAction(document.Annotations, annotation));
        }

        return new DocumentExporter().Render(document);
    }

    private static bool IsRed((byte R, byte G, byte B, byte A) p) => p.R > 200 && p.G < 90 && p.B < 90;

    [StaFact]
    public void Render_KoerperUndSpitze_SindGefuellt()
    {
        var result = Render(260, 180, Callout());

        IsRed(PixelTools.Get(result, 50, 40)).ShouldBeTrue();

        // Spitze von der Unterkante (y = 100) nach (70, 150): auf halbem Weg gefüllt, daneben weiß.
        IsRed(PixelTools.Get(result, 86, 120)).ShouldBeTrue();
        PixelTools.Get(result, 120, 130).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_SpitzeWandertAufDieSeite_ZuDerSieZeigt()
    {
        // Spitze rechts neben der Blase: Ansatz an der rechten Kante, unterhalb bleibt weiß.
        var result = Render(320, 180, Callout(tail: new PointD(290, 70)));

        IsRed(PixelTools.Get(result, 240, 66)).ShouldBeTrue();
        PixelTools.Get(result, 86, 120).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
    }

    [StaFact]
    public void Render_SpitzeImKoerper_KeineSpitze()
    {
        var callout = Callout(tail: new PointD(100, 60));

        callout.TailGeometry.ShouldBeNull();
        callout.Bounds.ShouldBe(callout.Rect);
    }

    [StaFact]
    public void Render_Rahmen_LiegtAussen()
    {
        var callout = Callout();
        callout.Border = new StrokeStyle(ArgbColor.FromRgb(0, 0, 255), 6);

        var result = Render(260, 180, callout);

        // Kante oben bei y = 30: darüber blau (Rahmen außen), darunter rot (Körper).
        var above = PixelTools.Get(result, 120, 27);
        above.B.ShouldBeGreaterThan((byte)200);
        above.R.ShouldBeLessThan((byte)60);
        IsRed(PixelTools.Get(result, 120, 33)).ShouldBeTrue();
    }

    [StaFact]
    public void Render_Gedreht_KoerperFolgtDerDrehung_SpitzeZeigtAufZielpunkt()
    {
        // Körper 160 × 70 um (120, 95), um 90° gedreht: x 85…155, y 15…175. Spitze nach rechts zu (240, 95).
        var callout = Callout(tail: new PointD(240, 95));
        callout.Rect = new RectD(40, 60, 160, 70);
        callout.Rotation = 90;

        var result = Render(260, 200, callout);

        IsRed(PixelTools.Get(result, 95, 160)).ShouldBeTrue();
        PixelTools.Get(result, 50, 70).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        IsRed(PixelTools.Get(result, 225, 95)).ShouldBeTrue();
    }

    [StaFact]
    public void Render_Leuchten_LaeuftWeichAus()
    {
        var sharp = Callout();
        sharp.Border = new StrokeStyle(ArgbColor.FromRgb(0, 0, 255), 4);
        var glow = Callout();
        glow.Border = new StrokeStyle(ArgbColor.FromRgb(0, 0, 255), 4);
        glow.BorderBlur = 12;

        var sharpResult = Render(260, 180, sharp);
        var glowResult = Render(260, 180, glow);

        // 12 px über der Oberkante: scharf nichts, mit Leuchten leicht blau.
        PixelTools.Get(sharpResult, 120, 20).ShouldBe(((byte)255, (byte)255, (byte)255, (byte)255));
        PixelTools.Get(glowResult, 120, 20).R.ShouldBeLessThan((byte)250);
    }

    [StaFact]
    public void Render_Text_WirdImKoerperGezeichnet()
    {
        var withText = Render(260, 180, Callout());
        var empty = Callout();
        empty.Text = string.Empty;
        var withoutText = Render(260, 180, empty);

        var differences = 0;
        for (var y = 30; y < 100; y++)
        {
            for (var x = 40; x < 200; x++)
            {
                if (PixelTools.Get(withText, x, y) != PixelTools.Get(withoutText, x, y))
                {
                    differences++;
                }
            }
        }

        differences.ShouldBeGreaterThan(50);
    }

    [StaFact]
    public void Render_AlleFormen_OhneFehler()
    {
        foreach (var shape in Enum.GetValues<CalloutShape>())
        {
            foreach (var curved in new[] { false, true })
            {
                var callout = Callout(shape);
                callout.TailCurved = curved;
                callout.Border = new StrokeStyle(ArgbColor.Black, 2);
                var result = Render(260, 180, callout);
                IsRed(PixelTools.Get(result, 120, 42)).ShouldBeTrue($"{shape}, gebogen = {curved}");
            }
        }
    }

    /// <summary>Schreibt Vorschaubilder aller Varianten, wenn <c>GSTS_CALLOUT_PREVIEW</c> auf einen Ordner zeigt (Sichtprüfung).</summary>
    [StaFact]
    public void Vorschau_SchreibtBilder()
    {
        var folder = Environment.GetEnvironmentVariable("GSTS_CALLOUT_PREVIEW");
        if (string.IsNullOrEmpty(folder))
        {
            return;
        }

        Directory.CreateDirectory(folder);
        var tails = new[] { new PointD(70, 160), new PointD(300, 150), new PointD(320, 40), new PointD(150, -60), new PointD(-60, 90), new PointD(215, 115) };
        foreach (var shape in Enum.GetValues<CalloutShape>())
        {
            foreach (var curved in new[] { false, true })
            {
                var items = new List<Annotation>();
                for (var i = 0; i < tails.Length; i++)
                {
                    var dx = (i % 3) * 420;
                    var dy = (i / 3) * 320;
                    var callout = Callout(shape, new PointD(tails[i].X + dx + 60, tails[i].Y + dy + 80));
                    callout.Rect = new RectD(100 + dx, 110 + dy, 160, 70);
                    callout.TailCurved = curved;
                    callout.Border = new StrokeStyle(ArgbColor.FromRgb(0x33, 0x33, 0x33), i % 2 == 0 ? 2 : 4);
                    callout.BorderBlur = i == 4 ? 10 : 0;
                    callout.Shadow = new ShadowStyle(new ArgbColor(0x59, 0, 0, 0), 4, 6);
                    items.Add(callout);
                }

                var result = Render(1300, 680, [.. items]);
                File.WriteAllBytes(Path.Combine(folder, $"callout-{shape}-{(curved ? "curved" : "straight")}.png"), result.EncodePng());
            }
        }
    }
}
