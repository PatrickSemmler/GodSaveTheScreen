using System.IO;
using System.Xml.Linq;
using GodSaveTheScreen.App.Hosting;
using GodSaveTheScreen.Core.Capture;
using GodSaveTheScreen.Core.Imaging;
using GodSaveTheScreen.Rendering;

namespace GodSaveTheScreen.App.Tests;

public sealed class CommandLineParserTests
{
    [Theory]
    [InlineData("rect", CaptureMode.Rectangle)]
    [InlineData("window", CaptureMode.Window)]
    [InlineData("full", CaptureMode.FullScreen)]
    [InlineData("FREEFORM", CaptureMode.Freeform)]
    public void Parse_CaptureModus_WirdErkannt(string value, CaptureMode expected)
    {
        CommandLineParser.Parse(["--capture", value]).Capture.ShouldBe(expected);
    }

    [Fact]
    public void Parse_DelayMitPunkt_WirdInvariantGelesen()
    {
        CommandLineParser.Parse(["--delay", "2.5"]).DelaySeconds.ShouldBe(2.5);
    }

    [Fact]
    public void Parse_DateipfadeUndOptionen_WerdenGetrennt()
    {
        var options = CommandLineParser.Parse([@"C:\a b.png", "--capture", "rect", "bild2.png"]);

        options.Files.ShouldBe([@"C:\a b.png", "bild2.png"]);
        options.Capture.ShouldBe(CaptureMode.Rectangle);
        options.Errors.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--capture")]
    [InlineData("--capture", "unsinn")]
    [InlineData("--delay", "abc")]
    [InlineData("--delay", "-1")]
    [InlineData("--unbekannt")]
    public void Parse_UngueltigeEingabe_ErzeugtFehlerOhneAbsturz(params string[] args)
    {
        CommandLineParser.Parse(args).Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void Parse_LeereArgumente_LiefertLeereOptionen()
    {
        var options = CommandLineParser.Parse([]);

        options.Capture.ShouldBeNull();
        options.Files.ShouldBeEmpty();
    }
}

public sealed class LocalizationTests
{
    private static HashSet<string> Keys(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "GodSaveTheScreen.slnx")))
        {
            dir = dir.Parent;
        }

        dir.ShouldNotBeNull("Solution-Verzeichnis nicht gefunden");
        var path = Path.Combine(dir.FullName, "src", "GodSaveTheScreen.App", "Resources", fileName);
        return XDocument.Load(path).Root!.Elements("data").Select(e => (string)e.Attribute("name")!).ToHashSet();
    }

    [Fact]
    public void Resx_JederSchluesselExistiertInEnglischUndDeutsch()
    {
        var english = Keys("Strings.resx");
        var german = Keys("Strings.de.resx");

        english.Except(german).ShouldBeEmpty("fehlt in Strings.de.resx");
        german.Except(english).ShouldBeEmpty("fehlt in Strings.resx");
    }

    [Fact]
    public void Resx_AlleSchluesselHabenWerte()
    {
        Keys("Strings.resx").ShouldNotBeEmpty();
    }
}

public sealed class PixelBufferBitmapTests
{
    [StaFact]
    public void ToBitmapSource_FromBitmapSource_Roundtrip()
    {
        var buffer = new PixelBuffer(3, 2);
        for (var i = 0; i < buffer.Pixels.Length; i += 4)
        {
            buffer.Pixels[i] = (byte)(i + 1);
            buffer.Pixels[i + 1] = (byte)(i + 2);
            buffer.Pixels[i + 2] = (byte)(i + 3);
            buffer.Pixels[i + 3] = 255;
        }

        var bitmap = buffer.ToBitmapSource();
        var back = PixelBufferExtensions.FromBitmapSource(bitmap);

        bitmap.IsFrozen.ShouldBeTrue();
        back.Width.ShouldBe(3);
        back.Height.ShouldBe(2);
        back.Pixels.ShouldBe(buffer.Pixels);
    }

    [StaFact]
    public void EncodePng_Roundtrip_BehaeltPixel()
    {
        var buffer = new PixelBuffer(2, 2);
        buffer.Pixels[0] = 10;
        buffer.Pixels[1] = 20;
        buffer.Pixels[2] = 30;
        buffer.Pixels[3] = 255;

        var png = buffer.EncodePng();
        using var stream = new MemoryStream(png);
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
            stream, System.Windows.Media.Imaging.BitmapCreateOptions.None, System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var back = PixelBufferExtensions.FromBitmapSource(decoder.Frames[0]);

        back.Pixels[..4].ShouldBe([10, 20, 30, 255]);
    }

    [StaFact]
    public void ToBitmapSource_LeererPuffer_WirftArgumentException()
    {
        Should.Throw<ArgumentException>(() => new PixelBuffer(0, 0).ToBitmapSource());
    }
}
